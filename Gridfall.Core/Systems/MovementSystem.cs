using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Gridfall.Core.Math;
using Gridfall.Core.Path;

namespace Gridfall.Core.Systems;

/// <summary>
/// Phase 4. Each creep advances along its current heading.
///
/// A creep reads the flow field ONLY when it crosses into a new cell. Between
/// cells it keeps its heading no matter what phase 2 did -- which is how "no
/// creep turns mid-cell" holds with no extra state. That is a rule the player
/// learns, not a limitation being hidden.
///
/// Iterates by ascending entity id. Always -- even though movement is
/// independent per creep today.
/// </summary>
internal static class MovementSystem
{
    public static void Run(
        SimState state, MapDef map, ContentSet content, PathSystem path,
        EventLog events, int tick, List<int> leakedCreepIds)
    {
        for (int k = 0; k < state.CreepCount; k++)
        {
            int slot = state.CreepSlotByOrder(k);
            EnemyDef def = content.Enemy(state.CreepDefIndex[slot]);

            Fix32 progress = state.CreepProgress[slot] + def.Speed;

            // A fast creep can cross more than one cell in a tick; each crossing
            // re-reads the field at the cell it arrives in.
            while (progress >= Fix32.One)
            {
                progress -= Fix32.One;

                int cellIndex = state.CreepCellIndex[slot];
                byte heading = state.CreepHeading[slot];
                (int dx, int dy) = Directions.Offsets[heading];
                int x = cellIndex % map.Width + dx;
                int y = cellIndex / map.Width + dy;

                if (!map.InBounds(x, y))
                {
                    // Walked off the board: treat as reaching the goal's edge.
                    leakedCreepIds.Add(state.CreepId[slot]);
                    progress = Fix32.Zero;
                    break;
                }

                int next = y * map.Width + x;
                state.CreepCellIndex[slot] = next;

                if (next == map.Index(map.Goal))
                {
                    leakedCreepIds.Add(state.CreepId[slot]);
                    progress = Fix32.Zero;
                    break;
                }

                byte flow = path.FlowAt(next);
                if (flow == PathSystem.Unreachable)
                {
                    // Should be impossible: the block check refuses any build that
                    // would strand a creep. "Should be impossible" is not "cannot",
                    // so the defined behaviour is stand still and say so -- never
                    // throw, which would take the whole run with it.
                    events.Add(new SimEvent(tick, EventKind.CreepStranded, state.CreepId[slot], 0,
                        new GridCell(x, y)));
                    progress = Fix32.Zero;
                    break;
                }
                if (flow != PathSystem.GoalMarker) state.CreepHeading[slot] = flow;
            }

            state.CreepProgress[slot] = progress;
        }
    }

    /// <summary>Sub-cell position in cell units, for range checks and the view.</summary>
    public static FixVec2 PositionOf(SimState state, MapDef map, int slot)
    {
        int cellIndex = state.CreepCellIndex[slot];
        Fix32 x = Fix32.FromInt(cellIndex % map.Width);
        Fix32 y = Fix32.FromInt(cellIndex / map.Width);
        (int dx, int dy) = Directions.Offsets[state.CreepHeading[slot]];
        Fix32 p = state.CreepProgress[slot];
        return new FixVec2(x + p * Fix32.FromInt(dx), y + p * Fix32.FromInt(dy));
    }
}
