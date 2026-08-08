using Gridfall.Core.Content;
using Gridfall.Core.Math;
using Gridfall.Core.Path;

namespace Gridfall.Core;

/// <summary>
/// All mutable game state, as parallel arrays. There is no Creep class with an
/// Update() method: an entity is an integer id, and its data lives in arrays
/// indexed by slot (engine guide 04).
///
/// Three reasons for this shape: iteration order is explicit, nothing allocates
/// in the tick loop, and the hash is a loop over arrays.
/// </summary>
public sealed class SimState
{
    public const int MaxCreeps = 512;
    public const int MaxTowers = 128;
    public const int MaxProjectiles = 1024;
    public const int MaxWaveEntries = 16;

    // ---- creeps -----------------------------------------------------------
    public int CreepCount;
    public readonly int[] CreepId = new int[MaxCreeps];
    public readonly ushort[] CreepDefIndex = new ushort[MaxCreeps];
    public readonly int[] CreepCellIndex = new int[MaxCreeps];
    /// <summary>Progress across the current cell, in [0,1) along CreepHeading.</summary>
    public readonly Fix32[] CreepProgress = new Fix32[MaxCreeps];
    public readonly byte[] CreepHeading = new byte[MaxCreeps];
    public readonly int[] CreepHp = new int[MaxCreeps];
    /// <summary>Ticks until this creep may attack a tower again.</summary>
    public readonly int[] CreepAttackCooldown = new int[MaxCreeps];

    /// <summary>Live creep ids, ascending. Iterate this, never raw slot order.</summary>
    private readonly int[] _creepIdOrder = new int[MaxCreeps];
    private int[] _slotOfCreepId = new int[4096];

    // ---- towers -----------------------------------------------------------
    public int TowerCount;
    public readonly int[] TowerId = new int[MaxTowers];
    public readonly ushort[] TowerDefIndex = new ushort[MaxTowers];
    public readonly int[] TowerCellIndex = new int[MaxTowers];
    public readonly int[] TowerCooldown = new int[MaxTowers];
    /// <summary>1-based. Hashed and snapshotted like every other piece of state.</summary>
    public readonly byte[] TowerLevel = new byte[MaxTowers];
    /// <summary>Structure health. Towers are destructible (ADR-0006).</summary>
    public readonly int[] TowerHp = new int[MaxTowers];
    private readonly int[] _towerIdOrder = new int[MaxTowers];
    private int[] _slotOfTowerId = new int[1024];

    // ---- projectiles ------------------------------------------------------
    public int ProjectileCount;
    public readonly int[] ProjectileId = new int[MaxProjectiles];
    public readonly int[] ProjectileTargetCreepId = new int[MaxProjectiles];
    public readonly FixVec2[] ProjectilePos = new FixVec2[MaxProjectiles];
    public readonly Fix32[] ProjectileSpeed = new Fix32[MaxProjectiles];
    public readonly int[] ProjectileDamage = new int[MaxProjectiles];

    // ---- globals ----------------------------------------------------------
    public int Gold;
    public int Lives;
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

    public int CreepSlotByOrder(int k) => _slotOfCreepId[_creepIdOrder[k]];
    public int TowerSlotByOrder(int k) => _slotOfTowerId[_towerIdOrder[k]];

    public int SlotOfCreep(int id) => id > 0 && id < _slotOfCreepId.Length ? _slotOfCreepId[id] : -1;
    public int SlotOfTower(int id) => id > 0 && id < _slotOfTowerId.Length ? _slotOfTowerId[id] : -1;

    public int AddCreep(ushort defIndex, int cellIndex, byte heading, int hp)
    {
        if (CreepCount >= MaxCreeps) return -1;
        int slot = CreepCount++;
        int id = NextEntityId++;

        CreepId[slot] = id;
        CreepDefIndex[slot] = defIndex;
        CreepCellIndex[slot] = cellIndex;
        CreepProgress[slot] = Fix32.Zero;
        CreepHeading[slot] = heading;
        CreepHp[slot] = hp;
        CreepAttackCooldown[slot] = 0;

        EnsureSlotMap(ref _slotOfCreepId, id);
        _slotOfCreepId[id] = slot;
        _creepIdOrder[CreepCount - 1] = id; // ids ascend, so appending keeps it sorted
        return id;
    }

    public void RemoveCreepBySlot(int slot)
    {
        int id = CreepId[slot];
        int last = CreepCount - 1;

        if (slot != last)
        {
            CreepId[slot] = CreepId[last];
            CreepDefIndex[slot] = CreepDefIndex[last];
            CreepCellIndex[slot] = CreepCellIndex[last];
            CreepProgress[slot] = CreepProgress[last];
            CreepHeading[slot] = CreepHeading[last];
            CreepHp[slot] = CreepHp[last];
            CreepAttackCooldown[slot] = CreepAttackCooldown[last];
            _slotOfCreepId[CreepId[slot]] = slot;
        }
        CreepCount--;
        _slotOfCreepId[id] = -1;
        RemoveFromIdOrder(_creepIdOrder, CreepCount + 1, id);
    }

    public int AddTower(ushort defIndex, int cellIndex, int hp)
    {
        if (TowerCount >= MaxTowers) return -1;
        int slot = TowerCount++;
        int id = NextEntityId++;

        TowerId[slot] = id;
        TowerDefIndex[slot] = defIndex;
        TowerCellIndex[slot] = cellIndex;
        TowerCooldown[slot] = 0;
        TowerLevel[slot] = 1;
        TowerHp[slot] = hp;

        EnsureSlotMap(ref _slotOfTowerId, id);
        _slotOfTowerId[id] = slot;
        _towerIdOrder[TowerCount - 1] = id;
        return id;
    }

    public void RemoveTowerBySlot(int slot)
    {
        int id = TowerId[slot];
        int last = TowerCount - 1;

        if (slot != last)
        {
            TowerId[slot] = TowerId[last];
            TowerDefIndex[slot] = TowerDefIndex[last];
            TowerCellIndex[slot] = TowerCellIndex[last];
            TowerCooldown[slot] = TowerCooldown[last];
            TowerLevel[slot] = TowerLevel[last];
            TowerHp[slot] = TowerHp[last];
            _slotOfTowerId[TowerId[slot]] = slot;
        }
        TowerCount--;
        _slotOfTowerId[id] = -1;
        RemoveFromIdOrder(_towerIdOrder, TowerCount + 1, id);
    }

    public int AddProjectile(int targetCreepId, FixVec2 pos, Fix32 speed, int damage)
    {
        if (ProjectileCount >= MaxProjectiles) return -1;
        int slot = ProjectileCount++;
        int id = NextEntityId++;
        ProjectileId[slot] = id;
        ProjectileTargetCreepId[slot] = targetCreepId;
        ProjectilePos[slot] = pos;
        ProjectileSpeed[slot] = speed;
        ProjectileDamage[slot] = damage;
        return id;
    }

    public void RemoveProjectileBySlot(int slot)
    {
        int last = ProjectileCount - 1;
        if (slot != last)
        {
            ProjectileId[slot] = ProjectileId[last];
            ProjectileTargetCreepId[slot] = ProjectileTargetCreepId[last];
            ProjectilePos[slot] = ProjectilePos[last];
            ProjectileSpeed[slot] = ProjectileSpeed[last];
            ProjectileDamage[slot] = ProjectileDamage[last];
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
        h = FnvHash.Combine(h, Gold, Lives, WaveIndex);
        h = FnvHash.Combine(h, PrepTicksRemaining);
        h = FnvHash.Combine(h, WaveActive ? 1 : 0);
        h = FnvHash.Combine(h, NextEntityId);
        h = FnvHash.Combine(h, random.RawState);
        h = FnvHash.Combine(h, (int)random.Draws);
        h = FnvHash.Combine(h, path.Version);
        h = FnvHash.CombineBytes(h, path.CostSpan);

        h = FnvHash.Combine(h, CreepCount);
        for (int k = 0; k < CreepCount; k++)
        {
            int s = CreepSlotByOrder(k);
            h = FnvHash.Combine(h, CreepId[s], CreepCellIndex[s], CreepHp[s]);
            h = FnvHash.Combine(h, CreepProgress[s].Raw, CreepHeading[s], CreepDefIndex[s]);
            h = FnvHash.Combine(h, CreepAttackCooldown[s]);
        }

        h = FnvHash.Combine(h, TowerCount);
        for (int k = 0; k < TowerCount; k++)
        {
            int s = TowerSlotByOrder(k);
            h = FnvHash.Combine(h, TowerId[s], TowerCellIndex[s], TowerCooldown[s]);
            h = FnvHash.Combine(h, TowerDefIndex[s], TowerLevel[s]);
            h = FnvHash.Combine(h, TowerHp[s]);
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
            h = FnvHash.Combine(h, ProjectileId[s], ProjectileTargetCreepId[s], ProjectileDamage[s]);
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
        other.CreepCount = CreepCount;
        Array.Copy(CreepId, other.CreepId, MaxCreeps);
        Array.Copy(CreepDefIndex, other.CreepDefIndex, MaxCreeps);
        Array.Copy(CreepCellIndex, other.CreepCellIndex, MaxCreeps);
        Array.Copy(CreepProgress, other.CreepProgress, MaxCreeps);
        Array.Copy(CreepHeading, other.CreepHeading, MaxCreeps);
        Array.Copy(CreepHp, other.CreepHp, MaxCreeps);
        Array.Copy(CreepAttackCooldown, other.CreepAttackCooldown, MaxCreeps);
        Array.Copy(_creepIdOrder, other._creepIdOrder, MaxCreeps);

        other.TowerCount = TowerCount;
        Array.Copy(TowerId, other.TowerId, MaxTowers);
        Array.Copy(TowerDefIndex, other.TowerDefIndex, MaxTowers);
        Array.Copy(TowerCellIndex, other.TowerCellIndex, MaxTowers);
        Array.Copy(TowerCooldown, other.TowerCooldown, MaxTowers);
        Array.Copy(TowerLevel, other.TowerLevel, MaxTowers);
        Array.Copy(TowerHp, other.TowerHp, MaxTowers);
        Array.Copy(_towerIdOrder, other._towerIdOrder, MaxTowers);

        other.ProjectileCount = ProjectileCount;
        Array.Copy(ProjectileId, other.ProjectileId, MaxProjectiles);
        Array.Copy(ProjectileTargetCreepId, other.ProjectileTargetCreepId, MaxProjectiles);
        Array.Copy(ProjectilePos, other.ProjectilePos, MaxProjectiles);
        Array.Copy(ProjectileSpeed, other.ProjectileSpeed, MaxProjectiles);
        Array.Copy(ProjectileDamage, other.ProjectileDamage, MaxProjectiles);

        other.Gold = Gold;
        other.Lives = Lives;
        other.WaveIndex = WaveIndex;
        other.PrepTicksRemaining = PrepTicksRemaining;
        other.WaveActive = WaveActive;
        other.NextEntityId = NextEntityId;
        Array.Copy(WaveEntrySpawned, other.WaveEntrySpawned, MaxWaveEntries);
        Array.Copy(WaveEntryNextTick, other.WaveEntryNextTick, MaxWaveEntries);

        other._slotOfCreepId = (int[])_slotOfCreepId.Clone();
        other._slotOfTowerId = (int[])_slotOfTowerId.Clone();
    }
}
