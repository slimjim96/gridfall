using Gridfall.Core.Content;
using Gridfall.Core.Math;

namespace Gridfall.Core.Systems;

/// <summary>
/// Phase 5, after tower targeting. Creeps that can attack pick a tower in range
/// and hit it, without stopping to do so (ADR-0006).
///
/// This exists to break the invariant seven balance passes ran into: total
/// defence tracks cumulative income, which holds only because towers are
/// permanent. If gold spent can be lost, income stops compounding into
/// permanent power.
///
/// Damage is buffered and applied in phase 7, exactly like creep damage --
/// which is why two creeps destroying one tower on the same tick is
/// deterministic for the same reason simultaneous kills are.
/// </summary>
internal static class EnemyAttackSystem
{
    public static void Run(SimState state, MapDef map, ContentSet content, DamageBuffer pendingTowerDamage)
    {
        // Ascending entity id, so an exact tie always resolves the same way.
        for (int k = 0; k < state.CreepCount; k++)
        {
            int slot = state.CreepSlotByOrder(k);

            if (state.CreepAttackCooldown[slot] > 0)
            {
                state.CreepAttackCooldown[slot]--;
                continue;
            }

            EnemyDef def = content.Enemy(state.CreepDefIndex[slot]);
            if (!def.AttacksTowers) continue;

            FixVec2 pos = MovementSystem.PositionOf(state, map, slot);
            int target = Acquire(state, map, def, pos);
            if (target < 0) continue;

            state.CreepAttackCooldown[slot] = def.AttackCooldownTicks;
            pendingTowerDamage.Add(state.TowerId[target], def.AttackDamage, DamageSource.EnemyAttack);
        }
    }

    /// <summary>
    /// Nearest tower in range, ties broken by lowest entity id.
    ///
    /// Nearest rather than weakest or most valuable: a creep walking past does
    /// not survey the board, and "it hit the thing next to it" is a readable
    /// reason for a loss (pillar 4).
    /// </summary>
    private static int Acquire(SimState state, MapDef map, EnemyDef def, FixVec2 from)
    {
        int best = -1;
        Fix32 bestDistance = Fix32.MaxValue;

        for (int k = 0; k < state.TowerCount; k++)
        {
            int slot = state.TowerSlotByOrder(k);
            int cellIndex = state.TowerCellIndex[slot];
            var towerPos = new FixVec2(
                Fix32.FromInt(cellIndex % map.Width),
                Fix32.FromInt(cellIndex / map.Width));

            Fix32 d2 = FixVec2.DistanceSquared(from, towerPos);
            if (d2 > def.AttackRangeSquared) continue;
            if (best >= 0 && d2 >= bestDistance) continue;

            bestDistance = d2;
            best = slot;
        }

        return best;
    }
}
