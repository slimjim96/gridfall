using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Gridfall.Core.Math;
using Gridfall.Core.Path;

namespace Gridfall.Core.Systems;

/// <summary>
/// Phase 5. Each tower picks a target and fires if off cooldown.
///
/// Firing creates a projectile; it does NOT deal damage. Damage happens in
/// phase 7, from a buffer, which is what makes simultaneous kills deterministic.
///
/// Targets are re-acquired every tick, so no tower holds a cross-tick entity
/// reference. Selection compares squared Fix32 distances -- never a square root
/// per creep per tower per tick -- and breaks exact ties by lowest entity id.
/// </summary>
internal static class TargetingSystem
{
    public static void Run(
        SimState state, MapDef map, ContentSet content, PathSystem path,
        EventLog events, int tick)
    {
        for (int k = 0; k < state.TowerCount; k++)
        {
            int slot = state.TowerSlotByOrder(k);

            if (state.TowerCooldown[slot] > 0)
            {
                state.TowerCooldown[slot]--;
                continue;
            }

            TowerDef def = content.Tower(state.TowerDefIndex[slot]);
            int level = state.TowerLevel[slot];
            int cellIndex = state.TowerCellIndex[slot];
            var towerPos = new FixVec2(
                Fix32.FromInt(cellIndex % map.Width),
                Fix32.FromInt(cellIndex / map.Width));

            int targetSlot = Acquire(state, map, path, def, towerPos, def.RangeSquaredAt(level));
            if (targetSlot < 0) continue;

            state.TowerCooldown[slot] = def.CooldownTicks;
            int projectileId = state.AddProjectile(
                state.CreepId[targetSlot], towerPos, def.ProjectileSpeed, def.DamageAt(level));

            if (projectileId < 0)
            {
                events.Add(new SimEvent(tick, EventKind.CapacityExceeded, state.TowerId[slot]));
                continue;
            }

            events.Add(new SimEvent(tick, EventKind.TowerFired,
                state.TowerId[slot], state.CreepId[targetSlot]));
        }
    }

    private static int Acquire(
        SimState state, MapDef map, PathSystem path, TowerDef def, FixVec2 towerPos, Fix32 rangeSquared)
    {
        int best = -1;
        ushort bestDistToGoal = ushort.MaxValue;
        Fix32 bestRange = Fix32.MaxValue;
        int bestHp = int.MaxValue;

        // Ascending entity id, so an exact tie always resolves the same way.
        for (int k = 0; k < state.CreepCount; k++)
        {
            int slot = state.CreepSlotByOrder(k);
            FixVec2 pos = MovementSystem.PositionOf(state, map, slot);
            Fix32 d2 = FixVec2.DistanceSquared(towerPos, pos);
            if (d2 > rangeSquared) continue;

            switch (def.Targeting)
            {
                case TargetRule.FurthestAlongPath:
                {
                    ushort toGoal = path.DistanceAt(state.CreepCellIndex[slot]);
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
                case TargetRule.LowestHp:
                    if (best < 0 || state.CreepHp[slot] < bestHp) { best = slot; bestHp = state.CreepHp[slot]; }
                    break;
            }
        }
        return best;
    }
}
