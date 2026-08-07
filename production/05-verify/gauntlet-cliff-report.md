# Gauntlet Cliff — Verification

**Slug:** `gauntlet-cliff` · **Status:** review · **Verdict:** NOT FIXED — diagnosed, and the fix rejected on evidence

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **170 passed**, 0 failed (unchanged — no behaviour changed) |
| Determinism trace | PASS | Unchanged; no content or Core change reaches the sim |
| Balance targets | **crossroads ok, gauntlet MISS** | gauntlet cannot hold the band; see below |
| Visual capture | n/a | No renderer change |

## What this slice set out to do, and what it did

**Intent:** fix gauntlet's difficulty cliff — 0% → 90% of runs lost on a 0.005 change in `hpGrowth`,
flagged since `income-vs-difficulty` and made more visible by `early-economy-2`'s split metric showing
gauntlet fully trivial (0% leak, 0% lost, 20/20 lives).

**Outcome: the cliff is structural and no data change fixes it.** Nothing was shipped to gauntlet's
geometry, wave table, or growth rate. The deliverables are the diagnosis, the metric that produced it,
and an amendment to the density target.

I carried a hypothesis in from the last slice — *"the shaped curve may be its answer"* — and it is
wrong. So are four others.

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | The cliff reproduces and is characterised | PASS | 200 runs: 1.12 → 0% lost / 5.5 lives; 1.125 → **95%** lost / 0.8 lives |
| 2 | The cause is identified | PASS | Run-to-run variance is zero: sd **0.0**, range **20–20**, across 200 runs at the shipped rate |
| 3 | The cause is distinguished from the mean | PASS | Leak rate moves *continuously* (2.0 → 2.4 → 2.7%) while runs-lost steps. The mean was never the problem |
| 4 | gauntlet holds the 15–30% runs-lost band | **MISS — and not shippable** | Only reachable at 1.12–1.125, where a 0.005 edit flips the game |
| 5 | crossroads is unaffected | PASS | Unchanged: leak 1.6%, 3.5% / 21.5% split, sd 6.8 |
| 6 | The metric is reusable | PASS | `lives left` now prints sd and range on every balance run, both maps |

## The measurement

The balance report now prints the **spread** of lives left, not just the mean:

```
lives left (avg)    20.0   sd 0.0, range 20-20
```

A mean can cross zero gradually. A distribution with no width crosses it all at once — **that is what a
cliff is**, and no summary statistic already in the report could show it.

| | mean lives | sd | range |
|---|---|---|---|
| crossroads | 7.6 | **6.8** | **0–20** |
| gauntlet @1.09 | 20.0 | **0.0** | **20–20** |
| gauntlet @1.12 (its best case) | 5.5 | 3.1 | 4–19 |

crossroads at a comparable mean has more than double the spread.

## Five hypotheses tested and rejected

| # | Hypothesis | Result |
|---|---|---|
| 1 | The growth rate is mistuned | Cliff relocates, never flattens. 0% → 96% between adjacent samples at every base |
| 2 | Missing sappers — gauntlet is the last place "towers are permanent" holds (`built = standing = 20.9`, gold destroyed 0) | Breaks the ceiling (built 28.9 vs 20.2 standing) and lifts sd 0.0 → **1.4**. Cliff moves 1.12→1.125 into 1.08→1.09 and keeps its shape |
| 3 | The wave table is wrong for this map — it is **byte-identical to crossroads'** apart from sappers, despite 7.9 route-cells covered per tower against 5.7 | Reshaping (base multiplier + gentler ramp, base 1.5–2.5 × growth 1.03–1.07) moves first leaks from wave 10 to wave 5 and mean lives smoothly 20 → 14.5 → 9.9 → 0.8. Runs lost still steps 0% → 96% |
| 4 | Two-row chambers seal too easily (153,692 seal refusals per 100 runs) | Three-row chambers at 9×13 raise towers **20.9 → 32.0**. sd stays 0.2–0.5. Also pushes density to 2.2, out of band |
| 5 | The route needs a real choice — twin connectors per wall | Collapses the map: path **29 → 17** (below the 18–30 band), density **1.7 → 3.9**, 100% lost at growth 1.09 |

Hypothesis 3 found a genuine defect worth recording regardless: gauntlet was handed crossroads' wave
table unchanged, and it produces zero leaks for nine waves here.

## Root cause

gauntlet's route is **fixed by its walls**. Three chambers joined by single-cell connectors: whatever
the player builds, creeps pass through the same three rooms in the same order, and a tower shifts the
path within a chamber by a cell or two without changing its shape.

crossroads is a **mazing** map — the route runs over buildable cells and is a property of the flow
field, not the terrain. Different build orders make genuinely different mazes. That is where sd 6.8
comes from.

> A map whose route cannot vary has one solution, therefore one outcome, therefore a threshold rather
> than a curve. No wave table can supply a distribution the geometry forbids.

## The finding that outlives gauntlet

gauntlet exists to satisfy the proposed density target (1.5–2.0 buildable per route cell; crossroads is
4.0). **The way to score well on density is to wall the route in** — and that is precisely what removes
mazing.

Both variants here that restored route freedom moved geometry straight back out of band: three-row
chambers to density 2.2, twin connectors to density 3.9 and path 17.

`map-density-target` proposes promoting density to a `MapTargets` constant. On this evidence it should
not be promoted alone — it needs a route-variability companion, or it will keep selecting for maps that
cannot be balanced.

## Why nothing was tuned

gauntlet *can* be put inside the 15–30% band, at growth 1.12–1.125. It was not, deliberately.

A number that satisfies a target while a 0.005 edit flips the outcome is not balance, and this project
has now rejected that shape three times: `tower-combat`'s `arrow hp 1300`, `tower-repair`'s
repair-at-any-time, and `salvage-value`'s full refund. Shipping a knife-edge gauntlet to turn a MISS
into an "ok" would be the fourth.

gauntlet stays at 1.09 — trivial, clear of its edge, and now documented as a negative result in its own
map and wave JSON.

## Not Verified

| What | Why |
|---|---|
| Whether a mazing map can hit the density band at all | Two counterexamples is not a proof. It is a strong prior and the reason `route-variance-metric` is proposed |
| Whether a human player produces more variance than the scripted one | The policy jitters among the top-3 placements; a human varies more. But the geometry bounds how much any player can vary the route |
| gauntlet with sappers on its own terms | It does not fix the cliff, but "towers are permanent" holding on one map is its own defect (`gauntlet-sappers`) |

## Branch Resolution

None — no stage failed. The slice's premise was wrong, which is a finding rather than a defect.
