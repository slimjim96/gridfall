using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Gridfall.Core.Path;

namespace Gridfall.Core.Systems;

/// <summary>
/// Phase 1. Drains the command queue in insertion order.
///
/// The only phase that may change the walkable grid, and the only one that may
/// reject player input. A build runs the block check BEFORE mutating anything.
/// May not move entities or deal damage: a command is applied, not simulated.
/// </summary>
internal static class CommandSystem
{
    public static void Run(
        SimState state,
        CommandQueue queue,
        MapDef map,
        ContentSet content,
        PathSystem path,
        EventLog events,
        int tick,
        SimRandom random)
    {
        for (int i = 0; i < queue.Count; i++)
        {
            ref CommandQueue.Entry e = ref queue[i];
            switch (e.Kind)
            {
                case CommandKind.Build: Build(state, map, content, path, events, tick, e.Cell, e.TowerDefIndex); break;
                case CommandKind.Sell: Sell(state, content, path, events, tick, e.TowerId); break;
                case CommandKind.StartWave: StartWave(state, content, events, tick, random); break;
                case CommandKind.Upgrade: Upgrade(state, content, events, tick, e.TowerId); break;
                case CommandKind.Repair: Repair(state, content, events, tick, e.TowerId); break;
            }
        }
        queue.Clear();
    }

    private static void Build(
        SimState state, MapDef map, ContentSet content, PathSystem path,
        EventLog events, int tick, GridCell cell, ushort defIndex)
    {
        if (!map.InBounds(cell))
        {
            Reject(events, tick, cell, RejectReason.OutOfBounds);
            return;
        }
        if (defIndex >= content.Towers.Length)
        {
            Reject(events, tick, cell, RejectReason.UnknownTower);
            return;
        }

        int index = map.Index(cell);
        if (map.Cells[index] != CellKind.Buildable)
        {
            Reject(events, tick, cell, RejectReason.NotBuildable);
            return;
        }
        if (path.IsBlocked(index))
        {
            Reject(events, tick, cell, RejectReason.Occupied);
            return;
        }

        TowerDef def = content.Tower(defIndex);
        if (state.Gold < def.Cost)
        {
            Reject(events, tick, cell, RejectReason.InsufficientGold);
            return;
        }
        if (state.TowerCount >= SimState.MaxTowers)
        {
            Reject(events, tick, cell, RejectReason.CapacityExceeded);
            return;
        }

        // The pillar-critical check: refuse before the grid changes, so a refused
        // build leaves the board byte-identical.
        if (!path.WouldRemainConnected(index))
        {
            Reject(events, tick, cell, RejectReason.WouldSealLane);
            return;
        }

        int id = state.AddTower(defIndex, index, def.Hp);
        state.Gold -= def.Cost;
        path.SetBlocked(index, true);   // sets the dirty flag; phase 2 consumes it

        events.Add(new SimEvent(tick, EventKind.BuildPlaced, id, defIndex, cell));
        events.Add(new SimEvent(tick, EventKind.GoldChanged, state.Gold, -def.Cost));
    }

    private static void Sell(
        SimState state, ContentSet content, PathSystem path,
        EventLog events, int tick, int towerId)
    {
        int slot = state.SlotOfTower(towerId);
        if (slot < 0) return;   // already gone; selling twice is not an error

        int cellIndex = state.TowerCellIndex[slot];
        TowerDef def = content.Tower(state.TowerDefIndex[slot]);

        // Half of EVERYTHING spent, upgrades included -- a flat base refund would
        // make upgrade-then-sell a money printer -- and then scaled by how much of
        // the tower is left, so a wreck cannot be cashed out at full price.
        int refund = def.SalvageValueAt(state.TowerLevel[slot], state.TowerHp[slot]);

        state.RemoveTowerBySlot(slot);
        state.Gold += refund;
        path.SetBlocked(cellIndex, false);

        events.Add(new SimEvent(tick, EventKind.TowerSold, towerId, refund));
        events.Add(new SimEvent(tick, EventKind.GoldChanged, state.Gold, refund));
    }

    /// <summary>
    /// Raise a tower one level. No block check: an upgrade occupies the same cell
    /// and changes no route, so it cannot seal a lane and never dirties the grid.
    /// </summary>
    private static void Upgrade(
        SimState state, ContentSet content, EventLog events, int tick, int towerId)
    {
        int slot = state.SlotOfTower(towerId);
        if (slot < 0)
        {
            events.Add(new SimEvent(tick, EventKind.UpgradeRejected, (int)RejectReason.NoSuchTower, towerId));
            return;
        }

        TowerDef def = content.Tower(state.TowerDefIndex[slot]);
        int level = state.TowerLevel[slot];

        if (level >= def.MaxLevel)
        {
            events.Add(new SimEvent(tick, EventKind.UpgradeRejected, (int)RejectReason.AlreadyMaxLevel, towerId));
            return;
        }

        int cost = def.Upgrades[level - 1].Cost;
        if (state.Gold < cost)
        {
            events.Add(new SimEvent(tick, EventKind.UpgradeRejected, (int)RejectReason.InsufficientGold, towerId));
            return;
        }

        state.Gold -= cost;
        state.TowerLevel[slot] = (byte)(level + 1);

        events.Add(new SimEvent(tick, EventKind.TowerUpgraded, towerId, level + 1));
        events.Add(new SimEvent(tick, EventKind.GoldChanged, state.Gold, -cost));
    }

    /// <summary>
    /// Restore a damaged tower to full structure health for gold. Only between
    /// waves.
    ///
    /// The between-waves restriction is the whole mechanic, and it was measured
    /// rather than argued. Repair available DURING a wave drove towers lost per
    /// run to exactly zero at every legal price -- because tower destruction is
    /// driven by throughput, and an unlimited-rate counter beats a throughput
    /// threat at any cost the player can afford. Restricted to between waves it
    /// reduces losses (9.9 -> 5.8 per run) instead of erasing them, which is what
    /// a counter-mechanic is supposed to do. See the balance report.
    ///
    /// The rate limit costs no new state: WaveActive is already hashed.
    ///
    /// No block check, for the same reason Upgrade has none: a repaired tower
    /// occupies the cell it already occupied, so the walkable grid never changes
    /// and phase 2 is never dirtied.
    ///
    /// Every rejection path returns before any mutation, so a refused repair
    /// leaves the state byte-identical -- the same discipline Build's seal check
    /// follows.
    /// </summary>
    private static void Repair(
        SimState state, ContentSet content, EventLog events, int tick, int towerId)
    {
        int slot = state.SlotOfTower(towerId);
        if (slot < 0) return;   // already destroyed; repairing a corpse is not an error

        if (state.WaveActive)
        {
            events.Add(new SimEvent(tick, EventKind.RepairRejected, (int)RejectReason.WaveInProgress, towerId));
            return;
        }

        TowerDef def = content.Tower(state.TowerDefIndex[slot]);

        // <= 0 rather than == 0: nothing pushes HP above max today, but a guard
        // that caught only the exact case would turn a future overshoot into a
        // negative cost, which is free gold.
        int missing = def.Hp - state.TowerHp[slot];
        if (missing <= 0)
        {
            events.Add(new SimEvent(tick, EventKind.RepairRejected, (int)RejectReason.NotDamaged, towerId));
            return;
        }

        int cost = def.RepairCostFor(state.TowerLevel[slot], missing);
        if (state.Gold < cost)
        {
            events.Add(new SimEvent(tick, EventKind.RepairRejected, (int)RejectReason.InsufficientGold, towerId));
            return;
        }

        state.Gold -= cost;
        state.TowerHp[slot] = def.Hp;
        // Level and cooldown are read, never written. Repair restores health and
        // only health -- a repaired tower does not get a free shot.

        events.Add(new SimEvent(tick, EventKind.TowerRepaired, towerId, missing));
        events.Add(new SimEvent(tick, EventKind.GoldChanged, state.Gold, -cost));
    }

    private static void StartWave(SimState state, ContentSet content, EventLog events, int tick, SimRandom random)
    {
        if (state.WaveActive) return;
        if (state.WaveIndex >= content.Waves.Length) return;

        WaveDef wave = content.Waves[state.WaveIndex];
        state.WaveIndex++;
        state.WaveActive = true;

        for (int i = 0; i < SimState.MaxWaveEntries; i++)
        {
            state.WaveEntrySpawned[i] = 0;
            state.WaveEntryNextTick[i] = int.MaxValue;
        }
        for (int i = 0; i < wave.Entries.Length && i < SimState.MaxWaveEntries; i++)
            state.WaveEntryNextTick[i] = tick + wave.Entries[i].DelayTicks + StartJitter(wave, random);

        events.Add(new SimEvent(tick, EventKind.WaveStarted, wave.Index));
    }

    /// <summary>
    /// How much later this group starts than authored, in ticks.
    ///
    /// The ONLY thing wave variance changes. Shifting start offsets reorders the
    /// groups and reshapes the pressure without touching which enemies arrive,
    /// how many, or how fast they follow each other -- so the authored budget is
    /// preserved exactly, which is what keeps a varied wave fair (pillar 4) and
    /// keeps the balance curve meaning something.
    ///
    /// **Zero variance draws nothing.** Not an optimisation: the RNG state is
    /// hashed, so a draw taken while the feature is off would change every
    /// recorded trace for no behaviour.
    /// </summary>
    private static int StartJitter(WaveDef wave, SimRandom random)
    {
        if (wave.VariancePercent <= 0) return 0;

        // 4 seconds at 30 Hz. Enough to reorder groups that were authored to
        // start together, small enough that total wave duration barely moves --
        // the spacing between spawns, which is what sets pressure, is untouched.
        const int MaxJitterTicks = 120;
        return random.NextInt(MaxJitterTicks * wave.VariancePercent / 100 + 1);
    }

    private static void Reject(EventLog events, int tick, GridCell cell, RejectReason reason)
        => events.Add(new SimEvent(tick, EventKind.BuildRejected, (int)reason, 0, cell));
}
