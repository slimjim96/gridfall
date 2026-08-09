using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Gridfall.Core.Math;
using Gridfall.Core.Path;

namespace Gridfall.Core.Systems;

/// <summary>
/// Phase 4. Each visitor advances along its current heading.
///
/// A visitor reads the flow field ONLY when it crosses into a new cell. Between
/// cells it keeps its heading no matter what phase 2 did -- which is how "no
/// visitor turns mid-cell" holds with no extra state. That is a rule the player
/// learns, not a limitation being hidden.
///
/// Iterates by ascending entity id. Always -- even though movement is
/// independent per visitor today.
/// </summary>
internal static class MovementSystem
{
    public static void Run(
        SimState state, MapDef map, ContentSet content, PathSystem path,
        EventLog events, int tick, List<int> leakedVisitorIds)
    {
        for (int k = 0; k < state.VisitorCount; k++)
        {
            int slot = state.VisitorSlotByOrder(k);
            VisitorDef def = content.Visitor(state.VisitorDefIndex[slot]);

            Fix32 progress = state.VisitorProgress[slot] + def.Speed;

            // A fast visitor can cross more than one cell in a tick; each crossing
            // re-reads the field at the cell it arrives in.
            while (progress >= Fix32.One)
            {
                progress -= Fix32.One;

                int cellIndex = state.VisitorCellIndex[slot];
                byte heading = state.VisitorHeading[slot];
                (int dx, int dy) = Directions.Offsets[heading];
                int x = cellIndex % map.Width + dx;
                int y = cellIndex / map.Width + dy;

                if (!map.InBounds(x, y))
                {
                    // Walked off the board: treat as reaching the goal's edge.
                    leakedVisitorIds.Add(state.VisitorId[slot]);
                    progress = Fix32.Zero;
                    break;
                }

                int next = y * map.Width + x;
                state.VisitorCellIndex[slot] = next;

                if (next == map.Index(map.Goal))
                {
                    leakedVisitorIds.Add(state.VisitorId[slot]);
                    progress = Fix32.Zero;
                    break;
                }

                byte flow = path.FlowAt(next);
                if (flow == PathSystem.Unreachable)
                {
                    // Should be impossible: the block check refuses any build that
                    // would strand a visitor. "Should be impossible" is not "cannot",
                    // so the defined behaviour is stand still and say so -- never
                    // throw, which would take the whole run with it.
                    events.Add(new SimEvent(tick, EventKind.VisitorStranded, state.VisitorId[slot], 0,
                        new GridCell(x, y)));
                    progress = Fix32.Zero;
                    break;
                }
                if (flow != PathSystem.GoalMarker) state.VisitorHeading[slot] = flow;
            }

            state.VisitorProgress[slot] = progress;
        }
    }

    /// <summary>Sub-cell position in cell units, for range checks and the view.</summary>
    public static FixVec2 PositionOf(SimState state, MapDef map, int slot)
    {
        int cellIndex = state.VisitorCellIndex[slot];
        Fix32 x = Fix32.FromInt(cellIndex % map.Width);
        Fix32 y = Fix32.FromInt(cellIndex / map.Width);
        (int dx, int dy) = Directions.Offsets[state.VisitorHeading[slot]];
        Fix32 p = state.VisitorProgress[slot];
        return new FixVec2(x + p * Fix32.FromInt(dx), y + p * Fix32.FromInt(dy));
    }
}
