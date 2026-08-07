# Gauntlet Cliff — v1

**Slug:** `gauntlet-cliff` · **Status:** done · **Verified at trace:** unchanged

## What Shipped

**Not a fix.** A diagnosis, a metric, and an amendment to a proposed target.

- **`lives left` now reports standard deviation and range**, not just the mean, on every balance run.
- **gauntlet's map and wave JSON now document it as a negative result** — kept as evidence, not as a
  shippable second map.
- **The density target should not be promoted alone.** `map-density-target` is amended.

gauntlet's geometry, wave table, and growth rate are unchanged. crossroads is untouched.

## The result that matters

gauntlet loses 0% of runs at growth 1.12 and **95% at 1.125**. The brief was to fix that. It cannot be
fixed by any number, and the reason is one line of output:

```
lives left (avg)    20.0   sd 0.0, range 20-20
```

At the shipped rate, all 200 runs finish with **exactly** 20 lives. Not a mean of 20 — a range of
20–20. Every run is the same game.

A mean can cross zero gradually. A distribution with no width crosses it all at once. **That is what a
cliff is**, and nothing already in the balance report could show it — leak rate moves perfectly
smoothly across the cliff (2.0% → 2.4% → 2.7%) while runs-lost steps.

| | mean lives | sd | range |
|---|---|---|---|
| crossroads | 7.6 | **6.8** | **0–20** |
| gauntlet @1.09 | 20.0 | **0.0** | **20–20** |

crossroads at a comparable mean has more than double the spread. Its runs cross zero one at a time;
gauntlet's cross together.

## Five things that do not fix it

I carried a hypothesis in from `early-economy-2` — *"the shaped curve may be its answer"*. It is wrong,
and so are four others.

| Hypothesis | Result |
|---|---|
| The growth rate is mistuned | Cliff relocates, never flattens |
| Missing sappers — gauntlet is the last place "towers are permanent" still holds | Real improvement: sd 0.0 → **1.4**, built 28.9 vs 20.2 standing. Cliff moves and keeps its shape |
| The wave table is wrong for this map | It **is** wrong — byte-identical to crossroads' despite 40% more coverage per tower. Reshaping moves first leaks from wave 10 to wave 5 and does not widen the distribution |
| Two-row chambers seal too easily | Three-row chambers raise towers **20.9 → 32.0**. sd stays 0.2–0.5. Density goes out of band |
| The route needs a real choice (twin connectors) | Path collapses **29 → 17**, density **1.7 → 3.9**, 100% lost at 1.09 |

## Root cause

gauntlet's route is **fixed by its walls** — three chambers joined by single-cell connectors. Whatever
you build, creeps pass through the same three rooms in the same order.

crossroads is a **mazing** map: its route runs over buildable cells and is a property of the flow field,
not the terrain, so different build orders make genuinely different mazes. That is where its variance
comes from.

> A map whose route cannot vary has one solution, therefore one outcome, therefore a threshold rather
> than a curve. No wave table can supply a distribution the geometry forbids.

## The finding that outlives gauntlet

gauntlet exists to satisfy the proposed density target — 1.5–2.0 buildable cells per route cell, where
crossroads sits at 4.0.

**The way to score well on density is to wall the route in**, and that is exactly what deletes mazing.
Both variants tried here that restored route freedom moved the geometry straight back out of band.

> Density measures how much defence a map permits. It says nothing about whether the player has a
> decision — and optimising it alone selects for maps that cannot be balanced.

`map-density-target` proposed promoting density to a `MapTargets` constant. **It should not be promoted
without a route-variability companion.**

## Why nothing was tuned

gauntlet *can* be placed inside the 15–30% runs-lost band, at growth 1.12–1.125. It was not.

A number that satisfies a target while a 0.005 edit flips the game is not balance. This project has
rejected that shape three times already — `tower-combat`'s `arrow hp 1300`, `tower-repair`'s
repair-at-any-time, `salvage-value`'s full refund. Turning gauntlet's MISS into an "ok" that way would
be the fourth, and the report would be a lie.

**crossroads remains the only balanced map**, and that is now a stated fact rather than an accident.

## Player-Facing Change

None. gauntlet plays exactly as before.

## Follow-Ups Not Done

| Item | Workspace | Slug |
|---|---|---|
| Do not promote density to a `MapTargets` constant without a route-variability companion | content-data | `map-density-target` (amended) |
| A route-variability metric — how much can the route change under building? Would have predicted this before the map was built | tooling | `route-variance-metric` |
| A second *mazing* map, so crossroads is not the only balanced one | content-data | `second-mazing-map` |
| gauntlet still has no sappers; "towers are permanent" holds there alone | content-data | `gauntlet-sappers` |
| A 0.005 growth change swings crossroads' runs-lost ~18 points too | tooling | `difficulty-slope` |

## Known Not Verified

- Whether a mazing map can hit the density band at all. Two counterexamples is a strong prior, not a
  proof — which is why `route-variance-metric` is proposed rather than a new target asserted.
- Whether a human produces more run-to-run variance than the scripted policy. They would vary more, but
  the geometry bounds how much *any* player can vary the route.
