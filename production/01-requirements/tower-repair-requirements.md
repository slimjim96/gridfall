# Tower Repair — Requirements

**Slug:** `tower-repair` · **Status:** done · **Owner:** design-lead

## In One Sentence

You can spend gold restoring a damaged tower's structure health, instead of only ever watching it die
and rebuilding it.

## Why now

[`tower-combat`](../06-release/tower-combat-v1.md) shipped destructible towers and closed the invariant
that had held for seven balance passes: towers built and towers standing are no longer the same number.
It left one question open in its own verify report — *"whether 'protect your towers' reads as a real
decision, or just as attrition."*

Today it is attrition. A sapper chews on a tower, the tower darkens, and the player has exactly one
response available: let it die and pay full price for a new one. A cost the player cannot act on is not
a decision, it is a tax. Repair is the verb that makes the damage state actionable.

## Pillar Check

| Pillar | | Note |
|---|---|---|
| 1 · The maze is the game | Neutral | Repairing in place changes no route, occupies the same cell, and cannot seal a lane. Same shape as upgrade. |
| 2 · Legible at a glance | **Supports** | Tower damage is already visible (darkening, shipped in `tower-combat`). Repair gives that existing visible state something to do. A state you can see but not act on is half a mechanic. |
| 3 · Deterministic, therefore fair | Neutral | A phase-1 command like the other four. No new randomness, no new clock. |
| 4 · Every loss is explainable | **Supports** | "I couldn't afford to repair the choke tower" is pointable in a way "the difficulty ramped" is not. |
| 5 · Small numbers, big decisions | **Supports** | Adds no tower and no enemy. It adds a decision *between pieces that already exist* — gold spent repairing is gold not spent building or upgrading. That is the pillar's preferred kind of addition. |

Nothing fights. This is the rare feature that is orthogonal to all five.

## TD Checklist

| Question | Answer |
|---|---|
| **Player fantasy** | Holding a line under pressure. Choosing which position is worth saving when you cannot save all of them. |
| **Pathing** | None. A repaired tower occupies the cell it already occupied; the walkable grid never changes and phase 2 is never dirtied. |
| **Economy** | A third gold sink, and the first one whose size is **set by the enemy rather than by the player**. Build and upgrade are both spends the player initiates at a price they choose. Repair is a bill that arrives. |
| **Wave pressure** | Reduces it, deliberately — but only for a player who pays. It converts sapper pressure from a fixed loss into a gold-denominated one. |
| **Failure state** | Running out of gold mid-wave with three damaged towers and having to pick one. Or over-repairing a doomed position and having nothing left to build with. |

## Constraints

1. **Repair must not turn destruction off.** This is the load-bearing constraint. If repair is cheap
   and instant, no tower ever dies, and `tower-combat`'s result — built ≠ standing — silently reverts.
   The previous slice learned this exact lesson in the other direction (`arrow hp 1300` hit both balance
   targets while quietly disabling the feature). **Towers must still be destroyed at a meaningful rate
   after this slice lands.**
2. **Repair must be worth doing.** Its mirror. If repairing to full costs more than selling and
   rebuilding to the same level, no player will ever repair, and the mechanic is decorative. The cost
   curve sits strictly below the sell-and-rebuild alternative.
3. Repair cost is **data**, not code — a knob on the tower def, tuned by `content-data`. Design names
   it; design does not set it.
4. Cost scales with **damage taken**, not a flat fee. A flat fee makes the optimal play "wait until
   1 HP", which is a reflex, not a decision.
5. Repairing an undamaged tower is refused, not silently charged.
6. Repair is instant and applies in phase 1, like every other command. Repair-over-time is a new system
   and a new phase for a decision that does not need one.
7. Tower health is already player-visible; repair introduces no state that is not.

## Acceptance Criteria

1. A damaged tower can be repaired, costing gold and raising its structure health.
2. Repair never raises health above the tower's maximum for its level.
3. Repairing an undamaged tower is refused, visibly, and no gold is spent.
4. Repair with insufficient gold is refused, and no gold is spent.
5. Repair cost scales with health missing: a barely-damaged tower costs strictly less than a
   nearly-destroyed one.
6. Repairing a tower to full always costs strictly less than selling it and rebuilding it to the same
   level. Checkable arithmetically against `SellValueAt`, with no human judgment.
7. A repaired tower keeps its upgrade level. Repair restores health, never anything else.
8. Repair changes no route: the walkable grid and the path version are untouched by a repair.
9. Two runs with identical inputs still produce identical state hashes.
10. `Restore(Snapshot())` round-trips a mid-repair fixture.
11. **Towers are still destroyed after this slice.** In the balance sim, towers standing at end of run
    remains strictly below towers built. If they converge back to equal, this slice has undone the
    previous one and fails regardless of its other numbers.
12. The repair affordance is discoverable on the board without a wiki — the player can tell a damaged
    tower is repairable and at what price.

## Open Questions

None blocking.

Deliberately deferred: **auto-repair between waves**. It is the obvious convenience and it deletes the
decision — the interesting case is repairing *during* a wave, with gold you wanted for something else.
Revisit only if playtesting says the manual click is tedious rather than tense.
