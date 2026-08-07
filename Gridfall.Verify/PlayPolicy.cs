using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Math;
using Gridfall.Core.Path;

namespace Gridfall.Verify;

/// <summary>
/// A scripted player, so the balance sim measures a game somebody is playing
/// rather than an undefended board.
///
/// "Competent" needs an operational definition or the numbers mean nothing.
/// This one is:
///
///   1. Build whenever gold allows, keeping no reserve -- an idle pile of gold
///      is the thing the economy targets are trying to detect.
///   2. Place where a tower covers the most cells of the CURRENT route, because
///      coverage is what a human eyeballs. Never place where the game would
///      refuse.
///   3. Buy the best damage-per-gold that is affordable right now, with no
///      lookahead and no saving up for something better.
///   4. Start the next wave as soon as the board is clear.
///   5. Never sell, never upgrade, never re-maze deliberately.
///
/// That is a reasonable beginner who understands coverage: clearly better than
/// nothing, clearly worse than a good player. Balance numbers from it are a
/// floor on difficulty, not a verdict -- read them as "even played this way,
/// wave 12 leaks", never as "wave 12 is correctly tuned".
///
/// Deterministic given its seed. The jitter below varies the PLAYER across
/// runs, not the game, which is what makes N runs mean something while nothing
/// in the simulation itself consumes randomness.
/// </summary>
public sealed class PlayPolicy
{
    /// <summary>Ticks between build attempts. A build needs a tick to apply.</summary>
    private const int BuildCooldownTicks = 3;

    /// <summary>Pick uniformly among this many best placements, so runs differ.</summary>
    private const int JitterTopN = 3;

    private readonly Sim _sim;
    private readonly SimRandom _rng;
    private readonly int[] _routeBuffer = new int[4096];
    private readonly List<(int cell, int score)> _candidates = new();

    private int _nextBuildTick;

    public PlayPolicy(Sim sim, uint seed)
    {
        _sim = sim;
        _rng = new SimRandom(seed);
    }

    public int BuildsPlaced { get; private set; }
    public int BuildsRefused { get; private set; }

    /// <summary>Call once per tick, before Sim.Tick().</summary>
    public void Update()
    {
        SimStateView state = _sim.State;

        // Wave management: clear board, no wave running -> send the next one.
        if (!state.WaveActive && state.CreepCount == 0)
        {
            TryBuild();                       // spend before the wave lands
            _sim.Enqueue(new StartWaveCommand());
            return;
        }

        if (_sim.TickCount < _nextBuildTick) return;
        TryBuild();
    }

    private void TryBuild()
    {
        _nextBuildTick = _sim.TickCount + BuildCooldownTicks;

        ushort? choice = BestAffordableTower();
        if (choice is not { } towerIndex) return;

        int cell = BestPlacement(_sim.Content.Tower(towerIndex));
        if (cell < 0) return;

        _sim.Enqueue(new BuildCommand(
            new GridCell(cell % _sim.Map.Width, cell / _sim.Map.Width), towerIndex));
        BuildsPlaced++;
    }

    /// <summary>
    /// Best damage-per-gold that is affordable now. No lookahead: a policy that
    /// saves up would be a different, and much harder to justify, definition of
    /// competent.
    /// </summary>
    private ushort? BestAffordableTower()
    {
        int gold = _sim.State.Gold;
        ushort? best = null;
        long bestValue = 0;

        for (ushort i = 0; i < _sim.Content.Towers.Length; i++)
        {
            TowerDef def = _sim.Content.Tower(i);
            if (def.Cost > gold || def.Damage <= 0 || def.CooldownTicks <= 0) continue;

            // damage per tick per gold, scaled to stay in integers
            long value = (long)def.Damage * 1000 / (def.CooldownTicks * (long)def.Cost);
            if (best is not null && value <= bestValue) continue;

            bestValue = value;
            best = i;
        }
        return best;
    }

    /// <summary>
    /// The legal cell whose tower would cover the most of the current route.
    ///
    /// Coverage counted in cells, not in time-in-range: a good enough proxy for
    /// what a human does by eye, and it does not need a damage model.
    /// </summary>
    private int BestPlacement(TowerDef def)
    {
        MapDef map = _sim.Map;
        PathSystem path = _sim.Path;

        int routeLength = path.TraceRoute(map.Index(map.Spawns[0]), _routeBuffer);
        if (routeLength == 0) return -1;

        _candidates.Clear();
        int bestScore = 0;

        // Ascending cell index, so ties resolve the same way every run.
        for (int cell = 0; cell < map.Cells.Length; cell++)
        {
            if (map.Cells[cell] != CellKind.Buildable) continue;
            if (path.IsBlocked(cell)) continue;

            int score = CoverageScore(cell, def, routeLength, map);
            if (score == 0) continue;

            _candidates.Add((cell, score));
            if (score > bestScore) bestScore = score;
        }

        if (_candidates.Count == 0) return -1;

        // Top-N by score, then jitter among them. Sorting by score descending and
        // cell ascending keeps the candidate list itself deterministic.
        _candidates.Sort((a, b) => a.score != b.score ? b.score - a.score : a.cell - b.cell);

        int window = System.Math.Min(JitterTopN, _candidates.Count);
        for (int attempt = 0; attempt < window; attempt++)
        {
            int pick = _candidates[_rng.NextInt(window)].cell;

            // Ask the game, do not guess: a placement that would seal a lane is
            // refused, and the policy should not be the thing that discovers it.
            if (path.WouldRemainConnected(pick)) return pick;
            BuildsRefused++;
        }

        return -1;
    }

    private int CoverageScore(int cell, TowerDef def, int routeLength, MapDef map)
    {
        var towerPos = new FixVec2(
            Fix32.FromInt(cell % map.Width),
            Fix32.FromInt(cell / map.Width));

        int covered = 0;
        for (int i = 0; i < routeLength; i++)
        {
            int routeCell = _routeBuffer[i];
            var pos = new FixVec2(
                Fix32.FromInt(routeCell % map.Width),
                Fix32.FromInt(routeCell / map.Width));

            if (FixVec2.DistanceSquared(towerPos, pos) <= def.RangeSquared) covered++;
        }
        return covered;
    }
}
