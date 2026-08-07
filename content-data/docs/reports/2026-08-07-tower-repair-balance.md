# Tower Repair — Balance

**Date:** 2026-08-07 · **Slug:** `tower-repair` · **Map:** `crossroads` · **200 runs, seed 1**

## The result

Repair as originally designed — available whenever the player holds gold — **switched tower destruction
off completely**, at every price the loader will accept. It took a rule change, not a tuning change, to
fix. That is the finding; the numbers below are the evidence for it.

## The sweep that mattered

| Config | built | standing | **lost** | repairs | repair gold | leak | runs lost |
|---|---|---|---|---|---|---|---|
| No repair (`tower-combat`) | 55.7 | 45.8 | **9.9** | — | — | 1.3% | 28.5% |
| `repairPercent` 60, any time | 45.6 | 45.6 | **0.0** | 19.0 | 454 | 1.2% | 25.5% |
| `repairPercent` 96, any time | 44.3 | 44.3 | **0.0** | 18.7 | 692 | 1.3% | 28.0% |
| **60, between waves only** | **51.6** | **45.8** | **5.8** | 6.2 | 169 | 1.2% | 26.0% |

96 is not an arbitrary stopping point — it is the **highest value the loader accepts**. At 97 the arrow
tower's level-1 repair cost (25) meets its sell-and-rebuild cost (25) and `ValidateRepairCurve` throws.
So row three is the ceiling of the entire legal range, and it still ends every run with every tower
standing.

**No price fixes this.** The wall that makes repair worth doing is the same thing that keeps it cheap:
repair must beat sell-and-rebuild, sell-and-rebuild costs half a tower, and a tower costs 50–90 gold in
an economy that moves 6,479 gold over twelve waves. Repair is bounded at roughly 0.5% of lifetime income
no matter what the knob says.

## Why price was never the lever

`tower-combat` established that tower destruction is **throughput-driven** — 62 sappers over twelve
waves, not damage per hit. The consequence nobody drew at the time:

> A throughput-driven threat cannot be countered by an action available at unlimited rate.

If the player may repair whenever they hold gold, they win the throughput race at any affordable price.
Cost only decides how much gold the immortality costs, not whether it is available. Rows two and three
differ by 238 gold and not by a single tower.

## The lever that did not work either

Before the rule change, the obvious fix was tried: make sappers out-damage the player's repair rate.

| `attackCooldown` | lost | repairs | repair gold | leak | runs lost |
|---|---|---|---|---|---|
| 1.2 (shipped) | 0.0 | 19.0 | 454 | 1.3% | 28.5% |
| 0.6 | 0.0 | 39.1 | 888 | 1.3% | 29.0% |
| 0.3 | 1.6 | 75.1 | 1,544 | 2.0% | **42.0%** |

Quadrupling sapper attack rate recovered 1.6 towers and blew the runs-lost band (15–30%) to 42%. The
player pays three times as much and still loses almost nothing.

The two throughputs are not symmetric levers, because the player's counter is funded by gold and gold
scales with the wave. Raising the threat raises difficulty without making the counter fail.

**No enemy data changed in this slice.** `attackCooldown` is back at 1.2.

## The shipped configuration

`repairPercent: 60` on both towers — a repair from zero costs 60% of what selling and rebuilding would.

| Metric | Value | Target | |
|---|---|---|---|
| leak rate | 1.2% | ≤ 4.0% | ok |
| runs lost | 26.0% | 15–30% late | ok |
| towers built | 51.6 | — | |
| towers standing | 45.8 | strictly below built | **ok** |
| **towers lost** | **5.8** | **> 0** | **ok** |
| repairs bought | 6.2 | — | |
| gold spent repairing | 169 | — | |
| lives left | 11.7 | — | |

Per-wave leak is unchanged from `tower-combat` to the tenth of a percent at every wave — including
**wave 3 at 14.1%**, still the worst wave and now for five passes running (`early-economy-2`).

That identity is worth stating plainly: repair changed *which towers survive*, and changed nothing about
what leaks. It is a tower-economy mechanic, not a difficulty mechanic, and the numbers agree.

## What the repair bill actually is

169 gold per run against 6,479 earned — **2.6% of lifetime income**, spread over 6.2 repairs.

That is small, and it is the honest number rather than a satisfying one. Repair is not currently a
meaningful gold sink; it is a meaningful *timing* decision. The gold it costs is close to noise, and
`repairPercent` is the knob that would change that without touching tower survival at all.

Anyone tempted to raise it should know what it does and does not buy:

- **It moves the bill.** 60 → 96 nearly doubled repair spend in the any-time runs.
- **It does not move survival.** Same runs, zero towers lost at both.

## Standing rule this slice adds

`tower-combat`'s report left one:

> When a slice adds a mechanic, a balance target is necessary and not sufficient. Measure that the
> mechanic is still doing something.

This slice needed the converse, and both balance targets passed for the entire time the bug existed:

> **Measure that the *previous* slice's mechanic is still doing something.** A new mechanic can satisfy
> every target while quietly deleting the one before it.

The number that caught it — towers lost per run — is now printed by `balance` on every run, precisely so
the next slice cannot delete this one without saying so.

## Not measured

- **`gauntlet`.** Sappers are only in the `crossroads` wave table, so repair has nothing to do there.
  Unchanged by this slice and still carrying its own cliff (`gauntlet-sappers`).
- **A player who sells mid-wave.** Selling still works while a wave runs; repairing does not. Cutting a
  damaged tower loose mid-wave and rebuilding between waves may beat repairing outright. The beginner
  policy never sells, so this sim cannot see it (`salvage-value`).
- **Per-archetype repair cost.** Both towers ship at 60; no sweep separated them (`repair-tuning`).
