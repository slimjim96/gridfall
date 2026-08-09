using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Gridfall.Core.Math;
using Gridfall.Core.Path;

namespace Gridfall.Core.Systems;

/// <summary>
/// Phase 3. Spawns visitors whose spawn tick has arrived.
///
/// Runs before movement on purpose: newly spawned visitors move on their birth
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
            VisitorDef def = content.Visitor(entry.VisitorIndex);

            byte heading = path.FlowAt(cellIndex);
            if (heading == PathSystem.GoalMarker || heading == PathSystem.Unreachable)
                heading = Directions.North;

            // Long math rather than Fix32 multiply: a tough visitor late in a long
            // wave table can exceed Fix32's +/-32767 range, and silently wrapping
            // a visitor's health is the kind of bug that shows up as "wave 30 is
            // trivial" three months later.
            int hp = (int)(((long)def.Appetite * wave.AppetiteScale.Raw) >> Fix32.FractionalBits);
            if (hp < 1) hp = 1;

            int id = state.AddVisitor(entry.VisitorIndex, cellIndex, heading, hp);
            if (id < 0)
            {
                events.Add(new SimEvent(tick, EventKind.CapacityExceeded, 0, 0, spawnCell));
                state.WaveEntryNextTick[i] = tick + entry.SpacingTicks;
                continue;
            }

            state.WaveEntrySpawned[i]++;
            state.WaveEntryNextTick[i] = tick + entry.SpacingTicks;
            events.Add(new SimEvent(tick, EventKind.VisitorSpawned, id, entry.VisitorIndex, spawnCell));
        }
    }

    /// <summary>True when every entry has spawned its full count and no visitors remain.</summary>
    public static bool WaveComplete(SimState state, ContentSet content)
    {
        if (!state.WaveActive) return false;
        if (state.VisitorCount > 0) return false;

        WaveDef wave = content.Waves[state.WaveIndex - 1];
        int entryCount = System.Math.Min(wave.Entries.Length, SimState.MaxWaveEntries);
        for (int i = 0; i < entryCount; i++)
            if (state.WaveEntrySpawned[i] < wave.Entries[i].Count) return false;

        return true;
    }
}
