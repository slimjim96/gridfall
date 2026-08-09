using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Gridfall.Core.Math;
using Gridfall.Core.Path;

namespace Gridfall.Core.Systems;

/// <summary>
/// Phase 5. Each station picks a target and fires if off cooldown.
///
/// Firing creates a projectile; it does NOT deal serving. Serving happens in
/// phase 7, from a buffer, which is what makes simultaneous kills deterministic.
///
/// Targets are re-acquired every tick, so no station holds a cross-tick entity
/// reference. Selection compares squared Fix32 distances -- never a square root
/// per visitor per station per tick -- and breaks exact ties by lowest entity id.
/// </summary>
internal static class TargetingSystem
{
    public static void Run(
        SimState state, MapDef map, ContentSet content, PathSystem path,
        EventLog events, int tick)
    {
        for (int k = 0; k < state.StationCount; k++)
        {
            int slot = state.StationSlotByOrder(k);

            if (state.StationCooldown[slot] > 0)
            {
                state.StationCooldown[slot]--;
                continue;
            }

            StationDef def = content.Station(state.StationDefIndex[slot]);
            int level = state.StationLevel[slot];
            int cellIndex = state.StationCellIndex[slot];
            var stationPos = new FixVec2(
                Fix32.FromInt(cellIndex % map.Width),
                Fix32.FromInt(cellIndex / map.Width));

            int targetSlot = Acquire(state, map, path, def, stationPos, def.RangeSquaredAt(level));
            if (targetSlot < 0) continue;

            state.StationCooldown[slot] = def.CooldownTicks;
            int projectileId = state.AddProjectile(
                state.VisitorId[targetSlot], stationPos, def.ProjectileSpeed, def.ServingAt(level));

            if (projectileId < 0)
            {
                events.Add(new SimEvent(tick, EventKind.CapacityExceeded, state.StationId[slot]));
                continue;
            }

            events.Add(new SimEvent(tick, EventKind.StationFired,
                state.StationId[slot], state.VisitorId[targetSlot]));
        }
    }

    private static int Acquire(
        SimState state, MapDef map, PathSystem path, StationDef def, FixVec2 stationPos, Fix32 rangeSquared)
    {
        int best = -1;
        ushort bestDistToGoal = ushort.MaxValue;
        Fix32 bestRange = Fix32.MaxValue;
        int bestAppetite = int.MaxValue;

        // Ascending entity id, so an exact tie always resolves the same way.
        for (int k = 0; k < state.VisitorCount; k++)
        {
            int slot = state.VisitorSlotByOrder(k);
            FixVec2 pos = MovementSystem.PositionOf(state, map, slot);
            Fix32 d2 = FixVec2.DistanceSquared(stationPos, pos);
            if (d2 > rangeSquared) continue;

            switch (def.Targeting)
            {
                case TargetRule.FurthestAlongPath:
                {
                    ushort toGoal = path.DistanceAt(state.VisitorCellIndex[slot]);
                    if (best < 0 || toGoal < bestDistToGoal)
                    {
                        best = slot;
                        bestDistToGoal = toGoal;
                    }
                    break;
                }
                case TargetRule.Nearest:
                    if (best < 0 || d2 < bestRange) { best = slot; bestRange = d2; }
                    break;
                case TargetRule.LowestAppetite:
                    if (best < 0 || state.VisitorAppetite[slot] < bestAppetite) { best = slot; bestAppetite = state.VisitorAppetite[slot]; }
                    break;
            }
        }
        return best;
    }
}
