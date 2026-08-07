# Analysis — destructible towers

**Date:** 2026-08-07 · **Map:** crossroads · **Runs:** 200 · **Seed:** 1
**Before:** [income vs difficulty](2026-08-07-income-vs-difficulty.md)

## Intent

Seven passes converged on one invariant: **total defence tracks cumulative income, because towers are
permanent.** Every lever tried — more waves, HP scaling, upgrades, starting gold, armour, a tighter map
— moved gold somewhere else and left that relationship intact.

This pass attacks the invariant directly. Gold spent on a tower can now be **lost**, so income stops
compounding into permanent power. The mechanic is the `sapper`: it attacks towers while it walks, and a
tower reduced to zero is destroyed and its cell freed.

## It breaks the invariant

| | Before | After |
|---|---|---|
| Towers built per run | 52.4 | **55.7** |
| Towers standing at end | 52.4 | **45.8** |

Built and standing were the *same number* for the project's entire history. They are not any more.
Roughly 10 towers a run — 18% of everything built — are destroyed and have to be rebuilt or written
off. That is the first gold sink in this game that the player does not choose.

## Tuning took three attempts, and the first two were the same mistake

**Attempt 1 — sweep `attackDamage`.** Started at 34: 100% of runs lost. Swept down to 10 and *still*
lost 90%. Damage per hit was not the variable.

**Attempt 2 — read the number properly.** Twelve waves spawn 62 sappers. What kills a tower is not how
hard one hit lands, it is **how many hits arrive**. Throughput, not damage — the same distinction the
`curve` model got wrong last pass, arrived at from the other side. Fixed `attackDamage` at 22 and swept
tower structure HP instead:

| arrow `hp` | leak | runs lost | built / standing |
|---|---|---|---|
| 250 | 3.2% | 90.0% | 78.7 / 46.4 |
| 600 | 2.3% | 65.0% | 65.5 / 49.2 |
| 1000 | 1.6% | 35.0% | 58.5 / 48.9 |
| 1300 | 1.3% | 20.0% | 53.4 / 48.8 |

1300 hit both targets. **And it was the wrong answer.**

**Attempt 3 — check whether the mechanic still does anything.** At 1300 only ~5 towers die per run, and
leak 1.3% / lost 20% is *identical to the previous pass without sappers at all*. The tuning that hit
the target had tuned the mechanic down until it stopped mattering. Hitting a number while deleting the
feature is not a pass.

So the real question was never "how tough should a tower be" — it was **where difficulty should come
from**. Trading enemy-HP inflation for tower destruction:

| arrow `hp` / `hpGrowth` | leak | runs lost | destroyed per run |
|---|---|---|---|
| 600 / 1.06 | 0.8% | 10.0% | 16.2 |
| 800 / 1.07 | 1.1% | 15.0% | 10.9 |
| **800 / 1.08** | **1.3%** | **26.7%** | **11.4** |
| 700 / 1.08 | 1.4% | 30.0% | 13.0 |

**Shipped `hp` 800 (cannon 1440), `hpGrowth` 1.08.**

## The result

200 runs: leak **1.3%** against ≤4%, runs lost **28.5%** against 15–30%. Both inside target, and
`hpGrowth` came *down* from 1.09 — destruction pressure substitutes for enemy hitpoint inflation.

That substitution is the point. Difficulty from HP growth is a wall the player watches approach;
difficulty from losing towers is a thing that happens *to a place on the board* for a visible reason.
Pillar 4 prefers the second.

## Why the numbers look odd

800 structure HP against 22 damage per hit is a ratio of 36:1, which reads wrong beside every other
number in the game. It is correct anyway, and for a reason worth writing down: a tower is attacked by
*many* enemies over *many* waves, so its health is measured against cumulative throughput, not against
one hit. Rescaling to look tidier would mean re-running the whole sweep to land in the same place.

## Wave 3 is still the sharpest wave

14.1% leak, against a ≤15% single-wave target. Inside the target, but by less than a point, and it has
been the worst wave through four passes now. Tracked as `early-economy-2`.

## Follow-ups

| Item | Workspace | Slug |
|---|---|---|
| Sappers only appear on `crossroads`. `gauntlet` has no destructible-tower pressure at all, and its cliff is untouched | content-data | `gauntlet-sappers` |
| A tower the player can repair. Destruction currently has exactly one answer — rebuild | game-design | `tower-repair` |
| Wave 3 at 14.1% leak, four passes running | content-data | `early-economy-2` |
| Does the player *notice* towers dying, or just find gold missing? Needs a human | presentation | `destruction-feedback` |

## Trace re-recorded

`TowerHp` and `CreepAttackCooldown` are new hashed state, and `hpGrowth` changed. `58c48d08fee174be`
→ `73843cd13ed4dad6`.

## Reproduce

```bash
dotnet run --project Gridfall.Verify -c Release -- balance --map crossroads --runs 200 --seed 1
```
