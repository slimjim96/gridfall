using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Gridfall.Core.Path;

namespace Gridfall.Core.Systems;

/// <summary>
/// Phase 7. Applies the whole pending-serving buffer in entity id order, then
/// resolves deaths, then leaks.
///
/// Deaths are resolved AFTER all serving is applied. Two stations that both land a
/// killing blow on the same visitor on the same tick produce one death and one
/// bounty -- if serving were applied inline in phase 5, the answer would depend
/// on station iteration order.
/// </summary>
internal static class ServingSystem
{
    /// <summary>
    /// Station serving, buffered by VisitorAttackSystem in phase 5. Applied in
    /// ascending station id, then destructions resolved -- so two visitors that both
    /// land a killing blow on the same tick destroy it once, for the same reason
    /// simultaneous visitor kills yield one death.
    /// </summary>
    private static void ResolveStationDrain(
        SimState state, ContentSet content, ServingBuffer pending,
        PathSystem path, EventLog events, int tick)
    {
        if (pending.Count == 0) return;

        pending.SortByVisitorId();   // sorts by the target id field, stations here
        var destroyed = new List<int>();

        for (int i = 0; i < pending.Count; i++)
        {
            ref ServingBuffer.Record r = ref pending[i];
            int slot = state.SlotOfStation(r.VisitorId);
            if (slot < 0) continue;
            if (state.StationStock[slot] <= 0) continue;   // already lethal this tick

            state.StationStock[slot] -= r.Amount;
            events.Add(new SimEvent(tick, EventKind.StationDepleted, r.VisitorId, r.Amount));

            if (state.StationStock[slot] <= 0) destroyed.Add(r.VisitorId);
        }
        pending.Clear();

        foreach (int stationId in destroyed)
        {
            int slot = state.SlotOfStation(stationId);
            if (slot < 0) continue;

            int cellIndex = state.StationCellIndex[slot];
            events.Add(new SimEvent(tick, EventKind.StationDestroyed, stationId, state.StationLevel[slot]));

            state.RemoveStationBySlot(slot);

            // Frees the cell, so the route may shorten. Destruction can only ever
            // OPEN a path, never close one, so no block check is needed. Phase 2
            // has already run, so this lands on the next tick (ADR-0006).
            path.SetBlocked(cellIndex, false);
        }
    }

    /// <param name="leakedVisitorIds">Recorded by MovementSystem in phase 4.</param>
    /// <param name="deadDefIndices">Filled for phase 8: one entry per death.</param>
    /// <param name="leakedDefIndices">Filled for phase 8: one entry per leak.</param>
    public static void Run(
        SimState state,
        ContentSet content,
        ServingBuffer pending,
        ServingBuffer pendingStationDrain,
        PathSystem path,
        EventLog events,
        int tick,
        List<int> leakedVisitorIds,
        List<int> scratchDeadIds,
        List<int> deadDefIndices,
        List<int> leakedDefIndices)
    {
        scratchDeadIds.Clear();
        deadDefIndices.Clear();
        leakedDefIndices.Clear();

        ResolveStationDrain(state, content, pendingStationDrain, path, events, tick);

        pending.SortByVisitorId();
        for (int i = 0; i < pending.Count; i++)
        {
            ref ServingBuffer.Record r = ref pending[i];
            int slot = state.SlotOfVisitor(r.VisitorId);
            if (slot < 0) continue;
            if (state.VisitorAppetite[slot] <= 0) continue;   // already lethal this tick

            // Per RECORD, not per tick total. Two 12-serving hits against fussiness 8
            // deal 4 + 4, never 24 - 8 = 16 -- per-hit is what makes rapid-fire
            // stations weak against fussiness, which is the entire design.
            //
            // Floored at 1: an visitor immune to a station is a soft-lock waiting to
            // happen, and "my stations do nothing" is not a readable failure.
            int amount = content.Visitor(state.VisitorDefIndex[slot]).ServingTaken(r.Amount);

            state.VisitorAppetite[slot] -= amount;
            events.Add(new SimEvent(tick, EventKind.VisitorServed, r.VisitorId, amount));

            if (state.VisitorAppetite[slot] <= 0) scratchDeadIds.Add(r.VisitorId);
        }
        pending.Clear();

        // Deaths, in ascending id order (inherited from the buffer's sort).
        foreach (int id in scratchDeadIds)
        {
            int slot = state.SlotOfVisitor(id);
            if (slot < 0) continue;
            int defIndex = state.VisitorDefIndex[slot];
            events.Add(new SimEvent(tick, EventKind.VisitorDied, id, defIndex));
            deadDefIndices.Add(defIndex);
            state.RemoveVisitorBySlot(slot);
        }

        // Leaks last, so a visitor that leaked and was killed on the same tick
        // resolves exactly once -- as a death.
        foreach (int id in leakedVisitorIds)
        {
            int slot = state.SlotOfVisitor(id);
            if (slot < 0) continue;
            int defIndex = state.VisitorDefIndex[slot];
            events.Add(new SimEvent(tick, EventKind.VisitorLeaked, id, defIndex));
            leakedDefIndices.Add(defIndex);
            state.RemoveVisitorBySlot(slot);
        }
        leakedVisitorIds.Clear();
    }
}
