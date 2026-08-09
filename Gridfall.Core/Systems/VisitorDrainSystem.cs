using Gridfall.Core.Content;
using Gridfall.Core.Math;

namespace Gridfall.Core.Systems;

/// <summary>
/// Phase 5, after station targeting. Visitors that can attack pick a station in range
/// and hit it, without stopping to do so (ADR-0006).
///
/// This exists to break the invariant seven balance passes ran into: total
/// defence tracks cumulative income, which holds only because stations are
/// permanent. If gold spent can be lost, income stops compounding into
/// permanent power.
///
/// Serving is buffered and applied in phase 7, exactly like visitor serving --
/// which is why two visitors destroying one station on the same tick is
/// deterministic for the same reason simultaneous kills are.
/// </summary>
internal static class VisitorAttackSystem
{
    public static void Run(SimState state, MapDef map, ContentSet content, ServingBuffer pendingStationDrain)
    {
        // Ascending entity id, so an exact tie always resolves the same way.
        for (int k = 0; k < state.VisitorCount; k++)
        {
            int slot = state.VisitorSlotByOrder(k);

            if (state.VisitorAttackCooldown[slot] > 0)
            {
                state.VisitorAttackCooldown[slot]--;
                continue;
            }

            VisitorDef def = content.Visitor(state.VisitorDefIndex[slot]);
            if (!def.AttacksStations) continue;

            FixVec2 pos = MovementSystem.PositionOf(state, map, slot);
            int target = Acquire(state, map, def, pos);
            if (target < 0) continue;

            state.VisitorAttackCooldown[slot] = def.AttackCooldownTicks;
            pendingStationDrain.Add(state.StationId[target], def.AttackDrain, ServingSource.VisitorAttack);
        }
    }

    /// <summary>
    /// Nearest station in range, ties broken by lowest entity id.
    ///
    /// Nearest rather than weakest or most valuable: a visitor walking past does
    /// not survey the board, and "it hit the thing next to it" is a readable
    /// reason for a loss (pillar 4).
    /// </summary>
    private static int Acquire(SimState state, MapDef map, VisitorDef def, FixVec2 from)
    {
        int best = -1;
        Fix32 bestDistance = Fix32.MaxValue;

        for (int k = 0; k < state.StationCount; k++)
        {
            int slot = state.StationSlotByOrder(k);
            int cellIndex = state.StationCellIndex[slot];
            var stationPos = new FixVec2(
                Fix32.FromInt(cellIndex % map.Width),
                Fix32.FromInt(cellIndex / map.Width));

            Fix32 d2 = FixVec2.DistanceSquared(from, stationPos);
            if (d2 > def.AttackRangeSquared) continue;
            if (best >= 0 && d2 >= bestDistance) continue;

            bestDistance = d2;
            best = slot;
        }

        return best;
    }
}
