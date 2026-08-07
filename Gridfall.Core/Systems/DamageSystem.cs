using Gridfall.Core.Content;
using Gridfall.Core.Events;

namespace Gridfall.Core.Systems;

/// <summary>
/// Phase 7. Applies the whole pending-damage buffer in entity id order, then
/// resolves deaths, then leaks.
///
/// Deaths are resolved AFTER all damage is applied. Two towers that both land a
/// killing blow on the same creep on the same tick produce one death and one
/// bounty -- if damage were applied inline in phase 5, the answer would depend
/// on tower iteration order.
/// </summary>
internal static class DamageSystem
{
    /// <param name="leakedCreepIds">Recorded by MovementSystem in phase 4.</param>
    /// <param name="deadDefIndices">Filled for phase 8: one entry per death.</param>
    /// <param name="leakedDefIndices">Filled for phase 8: one entry per leak.</param>
    public static void Run(
        SimState state,
        ContentSet content,
        DamageBuffer pending,
        EventLog events,
        int tick,
        List<int> leakedCreepIds,
        List<int> scratchDeadIds,
        List<int> deadDefIndices,
        List<int> leakedDefIndices)
    {
        scratchDeadIds.Clear();
        deadDefIndices.Clear();
        leakedDefIndices.Clear();

        pending.SortByCreepId();
        for (int i = 0; i < pending.Count; i++)
        {
            ref DamageBuffer.Record r = ref pending[i];
            int slot = state.SlotOfCreep(r.CreepId);
            if (slot < 0) continue;
            if (state.CreepHp[slot] <= 0) continue;   // already lethal this tick

            // Per RECORD, not per tick total. Two 12-damage hits against armour 8
            // deal 4 + 4, never 24 - 8 = 16 -- per-hit is what makes rapid-fire
            // towers weak against armour, which is the entire design.
            //
            // Floored at 1: an enemy immune to a tower is a soft-lock waiting to
            // happen, and "my towers do nothing" is not a readable failure.
            int armour = content.Enemy(state.CreepDefIndex[slot]).Armour;
            int amount = System.Math.Max(1, r.Amount - armour);

            state.CreepHp[slot] -= amount;
            events.Add(new SimEvent(tick, EventKind.CreepDamaged, r.CreepId, amount));

            if (state.CreepHp[slot] <= 0) scratchDeadIds.Add(r.CreepId);
        }
        pending.Clear();

        // Deaths, in ascending id order (inherited from the buffer's sort).
        foreach (int id in scratchDeadIds)
        {
            int slot = state.SlotOfCreep(id);
            if (slot < 0) continue;
            int defIndex = state.CreepDefIndex[slot];
            events.Add(new SimEvent(tick, EventKind.CreepDied, id, defIndex));
            deadDefIndices.Add(defIndex);
            state.RemoveCreepBySlot(slot);
        }

        // Leaks last, so a creep that leaked and was killed on the same tick
        // resolves exactly once -- as a death.
        foreach (int id in leakedCreepIds)
        {
            int slot = state.SlotOfCreep(id);
            if (slot < 0) continue;
            int defIndex = state.CreepDefIndex[slot];
            events.Add(new SimEvent(tick, EventKind.CreepLeaked, id, defIndex));
            leakedDefIndices.Add(defIndex);
            state.RemoveCreepBySlot(slot);
        }
        leakedCreepIds.Clear();
    }
}
