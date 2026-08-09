using Gridfall.Core.Content;
using Gridfall.Core.Math;

namespace Gridfall.Core.Systems;

/// <summary>
/// Phase 6. Advances projectiles and converts arrivals into pending serving.
///
/// Pending serving accumulates in a buffer; it is not applied here. Phase 7
/// applies the whole buffer in id order.
/// </summary>
internal static class ProjectileSystem
{
    public static void Run(SimState state, MapDef map, ServingBuffer pending)
    {
        // Iterate backwards: RemoveProjectileBySlot swap-removes, so walking down
        // means the swapped-in element has already been visited.
        for (int slot = state.ProjectileCount - 1; slot >= 0; slot--)
        {
            int targetId = state.ProjectileTargetVisitorId[slot];
            int targetSlot = state.SlotOfVisitor(targetId);

            if (targetSlot < 0)
            {
                // Target died before the shot landed. The projectile fizzles --
                // it does not re-target, because re-targeting mid-flight would
                // make the outcome depend on removal order.
                state.RemoveProjectileBySlot(slot);
                continue;
            }

            FixVec2 target = MovementSystem.PositionOf(state, map, targetSlot);
            FixVec2 pos = state.ProjectilePos[slot];
            FixVec2 delta = target - pos;
            Fix32 speed = state.ProjectileSpeed[slot];

            if (delta.LengthSquared() <= speed * speed)
            {
                pending.Add(targetId, state.ProjectileServing[slot], ServingSource.Projectile);
                state.RemoveProjectileBySlot(slot);
                continue;
            }

            state.ProjectilePos[slot] = pos + delta.Normalized() * speed;
        }
    }
}
