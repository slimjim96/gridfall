# Salvage Value — Requirements

**Slug:** `salvage-value` · **Status:** done · **Owner:** design-lead

## In One Sentence

Selling a tower refunds half of what is **left** of it, not half of what it cost, so a tower the enemy
has nearly destroyed cannot be cashed out at full price.

## Why now

`tower-repair` restricted repair to between waves, because repair at unlimited rate drove tower
destruction to zero. It left the **selling** path completely unrestricted, and selling is available
mid-wave. Measured, with a policy that cuts doomed towers loose below 25% health:

| | gold destroyed | towers destroyed | towers salvaged | leak | runs lost |
|---|---|---|---|---|---|
| Never sells (shipped) | 868 | **5.8** | 0 | 1.2% | 26.0% |
| Salvages mid-wave | 720 | **0.0** | 10.9 | 1.2% | 27.0% |

Both targets read "ok" in both rows. Again.

Two separate problems, and they are not the same size:

1. **Economically, it is minor.** 868 → 720 gold destroyed is a 17% recovery. Not a crisis.
2. **Experientially, it is total.** Towers destroyed goes from 5.8 to **zero**. A player who watches
   health bars never sees a tower die — they pre-empt it every time. The mechanic `tower-combat` exists
   to install stops happening.

And it is **dominant but tedious**: a modest gain available only to a player willing to watch every
health bar and click at the right moment. That is the worst shape a mechanic can have — it bores the
players who do it and quietly penalises the ones who don't.

## The inconsistency this is really about

`tower-repair` introduced it, and it is the sharpest argument here:

> The game now restricts the **constructive** response to damage (repair — between waves only) and
> leaves the **destructive** one (sell) available at any moment.

That is backwards. Saving a tower is the interesting decision and it is rate-limited; abandoning one is
the boring decision and it is not.

## Pillar Check

| Pillar | | Note |
|---|---|---|
| 1 · The maze is the game | **Supports (must not break)** | Repositioning an *undamaged* tower is a legitimate and important play — re-mazing depends on it. A refund that scales with health leaves that case untouched at full value. If this slice makes repositioning expensive, it has failed. |
| 2 · Legible at a glance | **Supports (with work)** | The player must see what a tower will refund *before* selling it. Today nothing shows a sell price at all. |
| 3 · Deterministic, therefore fair | Neutral | Integer arithmetic over state that is already hashed. No new state. |
| 4 · Every loss is explainable | **Supports** | "I got 8 gold back because it was nearly dead" is explainable. "I got 25 back for a wreck" is not. |
| 5 · Small numbers, big decisions | **Supports** | No new piece. It prices an existing verb so that the choice between repair, sell, and let-it-die is a real one instead of a solved one. |

Nothing fights, provided pillar 1's repositioning case stays whole. That is constraint 1.

## TD Checklist

| Question | Answer |
|---|---|
| **Player fantasy** | Salvage, not insurance. You recover what is left of a thing, not what you paid for it. |
| **Pathing** | None directly. Selling already frees the cell and dirties the grid; this changes only the gold. |
| **Economy** | Removes a recovery channel that scaled with damage taken. Cumulative income falls slightly and unrecoverable loss rises — which is the tower-combat invariant, restored. |
| **Wave pressure** | Raises it slightly, by making sapper damage cost what it was supposed to cost. |
| **Failure state** | Holding a nearly-dead tower into a wave you cannot repair it before, and getting almost nothing back when you finally cut it loose. |

## Constraints

1. **Selling an undamaged tower must refund exactly what it does today.** Repositioning is pillar 1 and
   this slice must be invisible to it. A player who never lets a tower get hit must not be able to tell
   this shipped.
2. Refund must never exceed what was spent, at any health, at any level. The existing invariant.
3. Refund scales with **remaining health**, so the value the enemy destroyed is the value the player
   cannot recover.
4. Selling stays available mid-wave. The fix is the price, not another restriction — the game already
   has one rate limit and a second would make damage feel like paperwork.
5. The sell price is visible before the click, not discovered after it.
6. No new simulation state.

## Acceptance Criteria

1. Selling an undamaged tower refunds exactly `SellValueAt(level)` — bit-for-bit what it refunds today.
2. Selling a damaged tower refunds strictly less, in proportion to health remaining.
3. Selling a tower at minimum health refunds approximately nothing, and never a negative amount.
4. Refund never exceeds total spend, at any health and any level.
5. An upgraded tower's refund still accounts for upgrade costs, scaled the same way.
6. The sell price for the hovered tower is visible on the board before selling.
7. Selling remains available while a wave is running.
8. Two runs with identical inputs still produce identical state hashes.
9. `Restore(Snapshot())` is unaffected — no new state was added.
10. **Gold destroyed per run returns to roughly the no-salvage figure** (868) when the policy salvages,
    instead of the 720 it recovers today. This is the criterion the slice exists for.
11. **Towers destroyed per run stays above zero** with a salvaging policy — the player can no longer
    pre-empt every destruction profitably.
12. Balance targets still hold: leak ≤ 4%, runs lost 15–30%.

## Open Questions

None blocking.

Deliberately **not** asked: whether selling should also be restricted to between waves. That would be
symmetric with repair and it is the wrong symmetry — see constraint 4. It is measured in the design spec
rather than argued, because the last two slices both turned on a measurement of exactly this shape.
