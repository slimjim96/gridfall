# Tower Upgrades — Requirements

**Slug:** `tower-upgrades` · **Status:** done · **Owner:** design-lead

## In One Sentence

You can spend gold making a tower you already own stronger, instead of only ever placing another one.

## Why now

Three balance passes converged on the same cause. There is **no gold sink that scales**: the only
purchase is a new tower, the board saturates, and late gold runs to 1,090+ with nothing to buy. Adding
HP to enemies moved the difficulty around but could not fix it —
[the HP scaling pass](../../content-data/docs/reports/2026-08-06-crossroads-hp-scaling-balance.md).

## Pillar Check

| Pillar | | Note |
|---|---|---|
| 1 · The maze is the game | **Neutral** | Upgrading in place changes no route. That is the point: it is an axis orthogonal to mazing, so it adds a decision without diluting the central one. |
| 2 · Legible at a glance | **Supports (with work)** | A tower's level must be visible on the board, not in a tooltip. |
| 3 · Deterministic, therefore fair | Neutral | New state; must be hashed and snapshotted like everything else. |
| 4 · Every loss is explainable | **Supports** | "That leaked because my choke tower was still level 1" is a readable reason. |
| 5 · Small numbers, big decisions | **Supports** | This is the pillar. Upgrades must be a genuine *choice against* building, not a strictly-better button. |

## TD Checklist

| Question | Answer |
|---|---|
| **Player fantasy** | Committing to a position. Turning a good spot into a great one instead of spreading thin. |
| **Pathing** | None. An upgraded tower occupies the same cell and blocks the same way. |
| **Economy** | This *is* the economy change. It gives gold somewhere to go that does not need free board space. |
| **Wave pressure** | Lets a defence keep scaling after the board fills, which is the thing that currently cannot happen. |
| **Failure state** | Over-investing in one tower and leaving a lane thin; or hoarding for an upgrade and being caught mid-wave. |

## Constraints

1. Upgrading must never be *strictly* better than building. If it dominates, the choice is fake and
   pillar 5 is violated.
2. A tower's level is player-visible on the board.
3. Level is simulation state: hashed, snapshotted, and covered by a test.
4. Upgrade costs and effects are **data**, in the tower JSON. No behaviour in code that a number could
   express.
5. Selling an upgraded tower must not be an exploit — refund cannot exceed what was spent.

## Acceptance Criteria

1. A tower with an upgrade available can be upgraded, costing gold and raising its level.
2. Upgrading raises the tower's damage, and range if the data says so.
3. A tower at maximum level cannot be upgraded further, and the attempt is refused visibly.
4. An upgrade with insufficient gold is refused, and no gold is spent.
5. Selling refunds a value that accounts for upgrades and never exceeds total spend.
6. Tower level is visible on the board without clicking anything.
7. Two runs with identical inputs still produce identical state hashes.
8. Mutating tower level changes the state hash.
9. `Restore(Snapshot())` preserves tower levels.
10. With upgrades available, the balance sim's late-game idle gold falls substantially from the ~1,090
    measured before this slice.

## Open Questions

None blocking. Whether upgrades should have *branching* paths is deliberately deferred — see the design
spec.
