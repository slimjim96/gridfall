using Gridfall.Core.Content;
using Gridfall.Core.Math;
using Gridfall.Core.Path;

namespace Gridfall.Core;

/// <summary>
/// All mutable game state, as parallel arrays. There is no Visitor class with an
/// Update() method: an entity is an integer id, and its data patience in arrays
/// indexed by slot (engine guide 04).
///
/// Three reasons for this shape: iteration order is explicit, nothing allocates
/// in the tick loop, and the hash is a loop over arrays.
/// </summary>
public sealed class SimState
{
    public const int MaxVisitors = 512;
    public const int MaxStations = 128;
    public const int MaxProjectiles = 1024;
    public const int MaxWaveEntries = 16;

    // ---- visitors -----------------------------------------------------------
    public int VisitorCount;
    public readonly int[] VisitorId = new int[MaxVisitors];
    public readonly ushort[] VisitorDefIndex = new ushort[MaxVisitors];
    public readonly int[] VisitorCellIndex = new int[MaxVisitors];
    /// <summary>Progress across the current cell, in [0,1) along VisitorHeading.</summary>
    public readonly Fix32[] VisitorProgress = new Fix32[MaxVisitors];
    public readonly byte[] VisitorHeading = new byte[MaxVisitors];
    public readonly int[] VisitorAppetite = new int[MaxVisitors];
    /// <summary>Ticks until this visitor may attack a station again.</summary>
    public readonly int[] VisitorAttackCooldown = new int[MaxVisitors];

    /// <summary>Live visitor ids, ascending. Iterate this, never raw slot order.</summary>
    private readonly int[] _visitorIdOrder = new int[MaxVisitors];
    private int[] _slotOfVisitorId = new int[4096];

    // ---- stations -----------------------------------------------------------
    public int StationCount;
    public readonly int[] StationId = new int[MaxStations];
    public readonly ushort[] StationDefIndex = new ushort[MaxStations];
    public readonly int[] StationCellIndex = new int[MaxStations];
    public readonly int[] StationCooldown = new int[MaxStations];
    /// <summary>1-based. Hashed and snapshotted like every other piece of state.</summary>
    public readonly byte[] StationLevel = new byte[MaxStations];
    /// <summary>Structure health. Stations are destructible (ADR-0006).</summary>
    public readonly int[] StationStock = new int[MaxStations];
    private readonly int[] _stationIdOrder = new int[MaxStations];
    private int[] _slotOfStationId = new int[1024];

    // ---- projectiles ------------------------------------------------------
    public int ProjectileCount;
    public readonly int[] ProjectileId = new int[MaxProjectiles];
    public readonly int[] ProjectileTargetVisitorId = new int[MaxProjectiles];
    public readonly FixVec2[] ProjectilePos = new FixVec2[MaxProjectiles];
    public readonly Fix32[] ProjectileSpeed = new Fix32[MaxProjectiles];
    public readonly int[] ProjectileServing = new int[MaxProjectiles];

    // ---- globals ----------------------------------------------------------
    public int Gold;
    public int Patience;
    public int WaveIndex;          // 0 == no wave has started
    public bool WaveActive;

    /// <summary>
    /// Ticks until the next wave starts on its own. 0 = no timer running.
    ///
    /// This is what turns the gap between waves from dead time into a resource.
    /// Hashed and snapshotted like everything else -- it decides when spawning
    /// begins, so it is simulation state, not presentation.
    /// </summary>
    public int PrepTicksRemaining;
    public int NextEntityId = 1;

    /// <summary>Per active-wave-entry spawn progress. State, therefore hashed.</summary>
    public readonly int[] WaveEntrySpawned = new int[MaxWaveEntries];
    public readonly int[] WaveEntryNextTick = new int[MaxWaveEntries];

    // ---- id / slot --------------------------------------------------------
    //
    // An id is stable for an entity's life. A slot is where it currently sits and
    // changes on death (swap-remove). Anything that must be deterministic
    // iterates by id.

    public int VisitorSlotByOrder(int k) => _slotOfVisitorId[_visitorIdOrder[k]];
    public int StationSlotByOrder(int k) => _slotOfStationId[_stationIdOrder[k]];

    public int SlotOfVisitor(int id) => id > 0 && id < _slotOfVisitorId.Length ? _slotOfVisitorId[id] : -1;
    public int SlotOfStation(int id) => id > 0 && id < _slotOfStationId.Length ? _slotOfStationId[id] : -1;

    public int AddVisitor(ushort defIndex, int cellIndex, byte heading, int hp)
    {
        if (VisitorCount >= MaxVisitors) return -1;
        int slot = VisitorCount++;
        int id = NextEntityId++;

        VisitorId[slot] = id;
        VisitorDefIndex[slot] = defIndex;
        VisitorCellIndex[slot] = cellIndex;
        VisitorProgress[slot] = Fix32.Zero;
        VisitorHeading[slot] = heading;
        VisitorAppetite[slot] = hp;
        VisitorAttackCooldown[slot] = 0;

        EnsureSlotMap(ref _slotOfVisitorId, id);
        _slotOfVisitorId[id] = slot;
        _visitorIdOrder[VisitorCount - 1] = id; // ids ascend, so appending keeps it sorted
        return id;
    }

    public void RemoveVisitorBySlot(int slot)
    {
        int id = VisitorId[slot];
        int last = VisitorCount - 1;

        if (slot != last)
        {
            VisitorId[slot] = VisitorId[last];
            VisitorDefIndex[slot] = VisitorDefIndex[last];
            VisitorCellIndex[slot] = VisitorCellIndex[last];
            VisitorProgress[slot] = VisitorProgress[last];
            VisitorHeading[slot] = VisitorHeading[last];
            VisitorAppetite[slot] = VisitorAppetite[last];
            VisitorAttackCooldown[slot] = VisitorAttackCooldown[last];
            _slotOfVisitorId[VisitorId[slot]] = slot;
        }
        VisitorCount--;
        _slotOfVisitorId[id] = -1;
        RemoveFromIdOrder(_visitorIdOrder, VisitorCount + 1, id);
    }

    public int AddStation(ushort defIndex, int cellIndex, int hp)
    {
        if (StationCount >= MaxStations) return -1;
        int slot = StationCount++;
        int id = NextEntityId++;

        StationId[slot] = id;
        StationDefIndex[slot] = defIndex;
        StationCellIndex[slot] = cellIndex;
        StationCooldown[slot] = 0;
        StationLevel[slot] = 1;
        StationStock[slot] = hp;

        EnsureSlotMap(ref _slotOfStationId, id);
        _slotOfStationId[id] = slot;
        _stationIdOrder[StationCount - 1] = id;
        return id;
    }

    public void RemoveStationBySlot(int slot)
    {
        int id = StationId[slot];
        int last = StationCount - 1;

        if (slot != last)
        {
            StationId[slot] = StationId[last];
            StationDefIndex[slot] = StationDefIndex[last];
            StationCellIndex[slot] = StationCellIndex[last];
            StationCooldown[slot] = StationCooldown[last];
            StationLevel[slot] = StationLevel[last];
            StationStock[slot] = StationStock[last];
            _slotOfStationId[StationId[slot]] = slot;
        }
        StationCount--;
        _slotOfStationId[id] = -1;
        RemoveFromIdOrder(_stationIdOrder, StationCount + 1, id);
    }

    public int AddProjectile(int targetVisitorId, FixVec2 pos, Fix32 speed, int serving)
    {
        if (ProjectileCount >= MaxProjectiles) return -1;
        int slot = ProjectileCount++;
        int id = NextEntityId++;
        ProjectileId[slot] = id;
        ProjectileTargetVisitorId[slot] = targetVisitorId;
        ProjectilePos[slot] = pos;
        ProjectileSpeed[slot] = speed;
        ProjectileServing[slot] = serving;
        return id;
    }

    public void RemoveProjectileBySlot(int slot)
    {
        int last = ProjectileCount - 1;
        if (slot != last)
        {
            ProjectileId[slot] = ProjectileId[last];
            ProjectileTargetVisitorId[slot] = ProjectileTargetVisitorId[last];
            ProjectilePos[slot] = ProjectilePos[last];
            ProjectileSpeed[slot] = ProjectileSpeed[last];
            ProjectileServing[slot] = ProjectileServing[last];
        }
        ProjectileCount--;
    }

    private static void RemoveFromIdOrder(int[] order, int count, int id)
    {
        for (int i = 0; i < count; i++)
        {
            if (order[i] != id) continue;
            Array.Copy(order, i + 1, order, i, count - i - 1);
            return;
        }
    }

    private static void EnsureSlotMap(ref int[] map, int id)
    {
        if (id < map.Length) return;
        int size = map.Length;
        while (size <= id) size *= 2;
        var grown = new int[size];
        Array.Copy(map, grown, map.Length);
        map = grown;
    }

    // ---- hash -------------------------------------------------------------

    /// <summary>
    /// The determinism primitive. Covers everything in the "is state" column of
    /// engine guide 04. A hash that misses a field is worse than no hash: the
    /// harness reports green while the game diverges.
    ///
    /// Iteration is by id, not slot -- otherwise two identical games hash
    /// differently after a swap-remove.
    /// </summary>
    public ulong Hash(int tickCount, SimRandom random, PathSystem path)
    {
        ulong h = FnvHash.Init();
        h = FnvHash.Combine(h, tickCount);
        h = FnvHash.Combine(h, Gold, Patience, WaveIndex);
        h = FnvHash.Combine(h, PrepTicksRemaining);
        h = FnvHash.Combine(h, WaveActive ? 1 : 0);
        h = FnvHash.Combine(h, NextEntityId);
        h = FnvHash.Combine(h, random.RawState);
        h = FnvHash.Combine(h, (int)random.Draws);
        h = FnvHash.Combine(h, path.Version);
        h = FnvHash.CombineBytes(h, path.CostSpan);

        h = FnvHash.Combine(h, VisitorCount);
        for (int k = 0; k < VisitorCount; k++)
        {
            int s = VisitorSlotByOrder(k);
            h = FnvHash.Combine(h, VisitorId[s], VisitorCellIndex[s], VisitorAppetite[s]);
            h = FnvHash.Combine(h, VisitorProgress[s].Raw, VisitorHeading[s], VisitorDefIndex[s]);
            h = FnvHash.Combine(h, VisitorAttackCooldown[s]);
        }

        h = FnvHash.Combine(h, StationCount);
        for (int k = 0; k < StationCount; k++)
        {
            int s = StationSlotByOrder(k);
            h = FnvHash.Combine(h, StationId[s], StationCellIndex[s], StationCooldown[s]);
            h = FnvHash.Combine(h, StationDefIndex[s], StationLevel[s]);
            h = FnvHash.Combine(h, StationStock[s]);
        }

        // Projectiles are short-lived and never referenced across a removal, so
        // slot order is stable within a tick; hash them in id order anyway so the
        // value does not depend on creation/removal interleaving.
        h = FnvHash.Combine(h, ProjectileCount);
        Span<int> projOrder = stackalloc int[ProjectileCount];
        for (int i = 0; i < ProjectileCount; i++) projOrder[i] = i;
        SortSlotsByProjectileId(projOrder);
        foreach (int s in projOrder)
        {
            h = FnvHash.Combine(h, ProjectileId[s], ProjectileTargetVisitorId[s], ProjectileServing[s]);
            h = FnvHash.Combine(h, ProjectilePos[s].X.Raw, ProjectilePos[s].Y.Raw, ProjectileSpeed[s].Raw);
        }

        h = FnvHash.CombineInts(h, WaveEntrySpawned);
        h = FnvHash.CombineInts(h, WaveEntryNextTick);
        return h;
    }

    private void SortSlotsByProjectileId(Span<int> slots)
    {
        // Insertion sort: tiny n, no allocation, and stable ordering by construction.
        for (int i = 1; i < slots.Length; i++)
        {
            int v = slots[i];
            int j = i - 1;
            while (j >= 0 && ProjectileId[slots[j]] > ProjectileId[v])
            {
                slots[j + 1] = slots[j];
                j--;
            }
            slots[j + 1] = v;
        }
    }

    // ---- snapshot ---------------------------------------------------------

    public void CopyTo(SimState other)
    {
        other.VisitorCount = VisitorCount;
        Array.Copy(VisitorId, other.VisitorId, MaxVisitors);
        Array.Copy(VisitorDefIndex, other.VisitorDefIndex, MaxVisitors);
        Array.Copy(VisitorCellIndex, other.VisitorCellIndex, MaxVisitors);
        Array.Copy(VisitorProgress, other.VisitorProgress, MaxVisitors);
        Array.Copy(VisitorHeading, other.VisitorHeading, MaxVisitors);
        Array.Copy(VisitorAppetite, other.VisitorAppetite, MaxVisitors);
        Array.Copy(VisitorAttackCooldown, other.VisitorAttackCooldown, MaxVisitors);
        Array.Copy(_visitorIdOrder, other._visitorIdOrder, MaxVisitors);

        other.StationCount = StationCount;
        Array.Copy(StationId, other.StationId, MaxStations);
        Array.Copy(StationDefIndex, other.StationDefIndex, MaxStations);
        Array.Copy(StationCellIndex, other.StationCellIndex, MaxStations);
        Array.Copy(StationCooldown, other.StationCooldown, MaxStations);
        Array.Copy(StationLevel, other.StationLevel, MaxStations);
        Array.Copy(StationStock, other.StationStock, MaxStations);
        Array.Copy(_stationIdOrder, other._stationIdOrder, MaxStations);

        other.ProjectileCount = ProjectileCount;
        Array.Copy(ProjectileId, other.ProjectileId, MaxProjectiles);
        Array.Copy(ProjectileTargetVisitorId, other.ProjectileTargetVisitorId, MaxProjectiles);
        Array.Copy(ProjectilePos, other.ProjectilePos, MaxProjectiles);
        Array.Copy(ProjectileSpeed, other.ProjectileSpeed, MaxProjectiles);
        Array.Copy(ProjectileServing, other.ProjectileServing, MaxProjectiles);

        other.Gold = Gold;
        other.Patience = Patience;
        other.WaveIndex = WaveIndex;
        other.PrepTicksRemaining = PrepTicksRemaining;
        other.WaveActive = WaveActive;
        other.NextEntityId = NextEntityId;
        Array.Copy(WaveEntrySpawned, other.WaveEntrySpawned, MaxWaveEntries);
        Array.Copy(WaveEntryNextTick, other.WaveEntryNextTick, MaxWaveEntries);

        other._slotOfVisitorId = (int[])_slotOfVisitorId.Clone();
        other._slotOfStationId = (int[])_slotOfStationId.Clone();
    }
}
