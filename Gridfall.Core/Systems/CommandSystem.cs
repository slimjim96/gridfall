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
        int tick)
    {
        for (int i = 0; i < queue.Count; i++)
        {
            ref CommandQueue.Entry e = ref queue[i];
            switch (e.Kind)
            {
                case CommandKind.Build: Build(state, map, content, path, events, tick, e.Cell, e.TowerDefIndex); break;
                case CommandKind.Sell: Sell(state, content, path, events, tick, e.TowerId); break;
                case CommandKind.StartWave: StartWave(state, content, events, tick); break;
                case CommandKind.Upgrade: Upgrade(state, content, events, tick, e.TowerId); break;
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

        // Half of EVERYTHING spent, upgrades included. A flat base refund would
        // make upgrade-then-sell a money printer.
        int refund = def.SellValueAt(state.TowerLevel[slot]);

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

    private static void StartWave(SimState state, ContentSet content, EventLog events, int tick)
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
            state.WaveEntryNextTick[i] = tick + wave.Entries[i].DelayTicks;

        events.Add(new SimEvent(tick, EventKind.WaveStarted, wave.Index));
    }

    private static void Reject(EventLog events, int tick, GridCell cell, RejectReason reason)
        => events.Add(new SimEvent(tick, EventKind.BuildRejected, (int)reason, 0, cell));
}
