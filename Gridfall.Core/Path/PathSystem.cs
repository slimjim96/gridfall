using Gridfall.Core.Content;

namespace Gridfall.Core.Path;

/// <summary>
/// One flow field for the whole board, rebuilt by reverse BFS from the goal on
/// any tick where the grid changed. Cost is O(cells), independent of creep count
/// -- 300 creeps cost the same as one. See ADR-0003 and engine guide 06.
///
/// Nothing here allocates after construction.
/// </summary>
public sealed class PathSystem
{
    public const byte Unreachable = 15;
    public const byte GoalMarker = 8;
    public const byte BlockedCost = 255;
    public const ushort NoDistance = ushort.MaxValue;

    private readonly MapDef _map;
    private readonly int _cellCount;

    private readonly byte[] _cost;
    private readonly byte[] _flow;
    private readonly ushort[] _dist;

    // Scratch buffers for the block check and the drag preview. Preallocated;
    // the two can never run in the same tick (preview is a view-side query
    // between ticks, the check is phase 1), so sharing them is safe.
    private readonly byte[] _scratchCost;
    private readonly byte[] _scratchFlow;
    private readonly ushort[] _scratchDist;

    private readonly int[] _queue;

    private bool _dirty;
    private ushort _version;

    public PathSystem(MapDef map)
    {
        _map = map;
        _cellCount = map.Width * map.Height;

        _cost = new byte[_cellCount];
        _flow = new byte[_cellCount];
        _dist = new ushort[_cellCount];
        _scratchCost = new byte[_cellCount];
        _scratchFlow = new byte[_cellCount];
        _scratchDist = new ushort[_cellCount];
        _queue = new int[_cellCount];

        for (int i = 0; i < _cellCount; i++)
            _cost[i] = map.Cells[i] == CellKind.Blocked ? BlockedCost : (byte)1;

        _dirty = true;
    }

    /// <summary>Increments on every recompute. Hashed -- it is how the harness
    /// proves a rebuild happened when one should have, and did not when it should not.</summary>
    public ushort Version => _version;

    public bool IsDirty => _dirty;

    /// <summary>Set by CommandSystem in phase 1. Nothing else may set it.</summary>
    internal void MarkDirty() => _dirty = true;

    public byte FlowAt(int cellIndex) => _flow[cellIndex];
    public byte FlowAt(GridCell c) => _flow[_map.Index(c)];
    public ushort DistanceAt(int cellIndex) => _dist[cellIndex];
    public ushort DistanceAt(GridCell c) => _dist[_map.Index(c)];
    public bool IsReachable(GridCell c) => _dist[_map.Index(c)] != NoDistance;
    public bool IsBlocked(int cellIndex) => _cost[cellIndex] == BlockedCost;

    internal ReadOnlySpan<byte> CostSpan => _cost;

    /// <summary>Phase 2. Does nothing at all on a tick where the grid did not change.</summary>
    internal bool RecomputeIfDirty()
    {
        if (!_dirty) return false;
        BuildInto(_cost, _flow, _dist);
        _dirty = false;
        _version++;
        return true;
    }

    internal void ForceRebuild()
    {
        BuildInto(_cost, _flow, _dist);
        _dirty = false;
        _version++;
    }

    /// <summary>
    /// Restores the cost grid and the version counter together.
    ///
    /// The version is hashed, so a restore that rebuilds the field with
    /// ForceRebuild would land on a different version than the snapshot had and
    /// diverge on the very first hash after the round trip. Found by
    /// SnapshotRoundTrip_MatchesRunningStraightThrough.
    /// </summary>
    internal void RestoreFrom(ReadOnlySpan<byte> cost, ushort version)
    {
        cost.CopyTo(_cost);
        BuildInto(_cost, _flow, _dist);
        _dirty = false;
        _version = version;
    }

    /// <summary>Blocks a cell for tower placement. Callers must have run the block check first.</summary>
    internal void SetBlocked(int cellIndex, bool blocked)
    {
        byte want = blocked
            ? BlockedCost
            : (_map.Cells[cellIndex] == CellKind.Blocked ? BlockedCost : (byte)1);
        if (_cost[cellIndex] == want) return;
        _cost[cellIndex] = want;
        _dirty = true;
    }

    /// <summary>
    /// Would blocking this cell leave every spawn able to reach the goal?
    /// One extra BFS on build attempts only -- not per tick.
    ///
    /// This is the same call the drag preview makes, so the route the player sees
    /// while hovering and the refusal the sim issues on release cannot disagree --
    /// they are literally the same code on the same buffers.
    /// </summary>
    public bool WouldRemainConnected(int cellIndex)
    {
        Array.Copy(_cost, _scratchCost, _cellCount);
        _scratchCost[cellIndex] = BlockedCost;
        BuildInto(_scratchCost, _scratchFlow, _scratchDist);

        foreach (GridCell spawn in _map.Spawns)
            if (_scratchDist[_map.Index(spawn)] == NoDistance) return false;

        return true;
    }

    /// <summary>
    /// The hypothetical field left by the last WouldRemainConnected call. Valid
    /// until the next one.
    ///
    /// Safe to read from the view between ticks: the block check runs in phase 1
    /// and a hover query runs between frames, so the two can never contend for
    /// the scratch buffers.
    /// </summary>
    public byte PreviewFlowAt(int cellIndex) => _scratchFlow[cellIndex];

    public ushort PreviewDistanceAt(int cellIndex) => _scratchDist[cellIndex];

    internal ReadOnlySpan<ushort> ScratchDistances => _scratchDist;

    /// <summary>
    /// Walks the flow field from a cell to the goal, writing the cell indices
    /// into the caller's span and returning how many were written.
    ///
    /// Allocation-free by design: the renderer calls this every time the hover
    /// cell changes. The step cap is the cell count, so a malformed field costs a
    /// bounded walk rather than an infinite loop -- "should be impossible" is not
    /// "cannot", and a hang in the render loop is worse than a short route.
    /// </summary>
    /// <param name="preview">Walk the hypothetical field instead of the live one.</param>
    public int TraceRoute(int startCellIndex, Span<int> into, bool preview = false)
    {
        byte[] flow = preview ? _scratchFlow : _flow;

        int count = 0;
        int cell = startCellIndex;

        for (int step = 0; step < _cellCount && count < into.Length; step++)
        {
            into[count++] = cell;

            byte direction = flow[cell];
            if (direction == GoalMarker || direction == Unreachable) break;

            (int dx, int dy) = Directions.Offsets[direction];
            int nx = cell % _map.Width + dx;
            int ny = cell / _map.Width + dy;
            if (!_map.InBounds(nx, ny)) break;

            cell = ny * _map.Width + nx;
        }

        return count;
    }

    /// <summary>Route length from a spawn to the goal on the live field, in cells.</summary>
    public int RouteLength(GridCell from)
    {
        ushort distance = _dist[_map.Index(from)];
        return distance == NoDistance ? -1 : distance;
    }

    // -----------------------------------------------------------------------

    private void BuildInto(byte[] cost, byte[] flow, ushort[] dist)
    {
        for (int i = 0; i < _cellCount; i++)
        {
            flow[i] = Unreachable;
            dist[i] = NoDistance;
        }

        int goalIndex = _map.Index(_map.Goal);
        int head = 0, tail = 0;
        dist[goalIndex] = 0;
        flow[goalIndex] = GoalMarker;
        _queue[tail++] = goalIndex;

        while (head < tail)
        {
            int cell = _queue[head++];
            int cx = cell % _map.Width;
            int cy = cell / _map.Width;
            ushort next = (ushort)(dist[cell] + 1);

            // N, E, S, W. THE ORDER IS LOAD-BEARING -- it decides which of two
            // equal-cost routes wins. Do not replace it with an enum iteration.
            for (int d = 0; d < 4; d++)
            {
                (int dx, int dy) = Directions.Offsets[d];
                int nx = cx + dx, ny = cy + dy;
                if (!_map.InBounds(nx, ny)) continue;

                int n = ny * _map.Width + nx;
                if (cost[n] == BlockedCost) continue;

                // FIRST ASSIGNMENT WINS. A cell reached again at equal distance is
                // left alone. Overwriting here is still deterministic but makes the
                // chosen route depend on the frontier shape rather than on the rule
                // above, and creeps split across equal-cost routes. It is invisible
                // on symmetric maps -- see _examples/path-recompute/05-report-fail.md.
                if (dist[n] != NoDistance) continue;

                dist[n] = next;
                flow[n] = Directions.Opposite((byte)d); // point back toward the goal
                _queue[tail++] = n;
            }
        }
    }
}
