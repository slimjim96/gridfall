using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Gridfall.Core.Path;

namespace Gridfall.Core.Systems;

/// <summary>
/// Phase 3. Spawns creeps whose spawn tick has arrived.
///
/// Runs before movement on purpose: newly spawned creeps move on their birth
/// tick, so they do not stutter at the spawn point. Entries are walked in array
/// order, so entry order determines entity id order on ties -- which means
/// reordering a wave table's entries changes the run (engine guide 07).
/// </summary>
internal static class SpawnSystem
{
    public static void Run(
        SimState state, MapDef map, ContentSet content, PathSystem path,
        EventLog events, int tick)
    {
        if (!state.WaveActive) return;

        WaveDef wave = content.Waves[state.WaveIndex - 1];
        int entryCount = System.Math.Min(wave.Entries.Length, SimState.MaxWaveEntries);

        for (int i = 0; i < entryCount; i++)
        {
            WaveEntry entry = wave.Entries[i];
            if (state.WaveEntrySpawned[i] >= entry.Count) continue;
            if (tick < state.WaveEntryNextTick[i]) continue;

            GridCell spawnCell = map.Spawns[System.Math.Min(entry.SpawnIndex, map.Spawns.Length - 1)];
            int cellIndex = map.Index(spawnCell);
            EnemyDef def = content.Enemy(entry.EnemyIndex);

            byte heading = path.FlowAt(cellIndex);
            if (heading == PathSystem.GoalMarker || heading == PathSystem.Unreachable)
                heading = Directions.North;

            int id = state.AddCreep(entry.EnemyIndex, cellIndex, heading, def.Hp);
            if (id < 0)
            {
                events.Add(new SimEvent(tick, EventKind.CapacityExceeded, 0, 0, spawnCell));
                state.WaveEntryNextTick[i] = tick + entry.SpacingTicks;
                continue;
            }

            state.WaveEntrySpawned[i]++;
            state.WaveEntryNextTick[i] = tick + entry.SpacingTicks;
            events.Add(new SimEvent(tick, EventKind.CreepSpawned, id, entry.EnemyIndex, spawnCell));
        }
    }

    /// <summary>True when every entry has spawned its full count and no creeps remain.</summary>
    public static bool WaveComplete(SimState state, ContentSet content)
    {
        if (!state.WaveActive) return false;
        if (state.CreepCount > 0) return false;

        WaveDef wave = content.Waves[state.WaveIndex - 1];
        int entryCount = System.Math.Min(wave.Entries.Length, SimState.MaxWaveEntries);
        for (int i = 0; i < entryCount; i++)
            if (state.WaveEntrySpawned[i] < wave.Entries[i].Count) return false;

        return true;
    }
}
