# Analysis — gauntlet's cliff, and why no wave table fixes it

**Date:** 2026-08-07 · **Map:** gauntlet · **Runs:** 100 per sweep cell, 200 for the finals · **Seed:** 1
**Before:** [early economy 2](2026-08-07-early-economy-2-balance.md) · [income vs difficulty](2026-08-07-income-vs-difficulty.md)

## Verdict up front

**gauntlet's difficulty cannot be tuned, and the cause is not in any number.** Its route is fixed by the
map's walls, so every run is the same game — and a map with one game has no difficulty *curve*, only a
threshold.

**Nothing shipped.** gauntlet's geometry, wave table, and growth rate are unchanged. Five hypotheses
were tested and all five failed; the value of this pass is the diagnosis, the metric that found it, and
what it says about the density target.

## The cliff, at 200 runs

| hpGrowth | leak | runs lost | lives left |
|---|---|---|---|
| 1.09 *(shipped)* | 0.0% | 0.0% | 20.0 **sd 0.0, range 20–20** |
| 1.11 | 0.0% | 0.0% | 20.0 **sd 0.0, range 20–20** |
| 1.12 | 2.0% | **0.0%** | 5.5 sd 3.1, range 4–19 |
| 1.125 | 2.4% | **95.0%** | 0.8 sd 3.3, range 0–15 |
| 1.13 | 2.7% | 95.0% | 0.3 sd 1.5, range 0–8 |

Note what moves smoothly and what does not. **Leak rate is continuous** — 2.0% → 2.4% → 2.7%. **Runs
lost is a step** — 0% → 95% on a 0.005 change.

At the shipped rate, all 200 runs finish with exactly 20 lives. Not a mean of 20: a *range* of 20–20.
Nothing in gauntlet has ever touched the player.

## The metric that found it

The balance report now prints the **standard deviation and range** of lives left, not just the mean.

```
lives left (avg)    20.0   sd 0.0, range 20-20
```

That one line is the whole diagnosis. A mean can cross zero gradually; a distribution with no width
crosses it all at once. **That is what a cliff is**, and the mean alone cannot show it.

The contrast is stark:

| | mean lives | sd | range |
|---|---|---|---|
| crossroads | 7.6 | **6.8** | **0–20** |
| gauntlet @1.09 | 20.0 | **0.0** | **20–20** |
| gauntlet @1.12 (its best case) | 5.5 | 3.1 | 4–19 |

crossroads at a *similar mean* has more than double the spread. As difficulty rises there, runs cross
zero one at a time across a wide band. On gauntlet they cross together.

## Five hypotheses, five failures

### 1. The growth rate — no

Swept 1.09 → 1.14. The cliff does not flatten, it relocates. Runs lost goes 0% → 96% between two
adjacent samples at every base tested.

### 2. Sappers — no

gauntlet has no sappers, so `built = standing = 20.9` and `gold destroyed = 0`. It is the **last place
in the game where "towers are permanent" still holds** — the invariant `tower-combat` was built to
break. That made it the obvious suspect.

Adding crossroads' sapper schedule (and at half strength) does break the ceiling — built rises to 28.9
against 20.2 standing — and it does raise variance from sd 0.0 to **sd 1.4**. But the cliff simply moves
from 1.12→1.125 to 1.08→1.09, still 0% → 96%.

Real improvement, wrong order of magnitude. crossroads is at sd 6.8.

### 3. Reshaping the wave table — no

gauntlet's wave table is **byte-identical to crossroads'** apart from sappers — every wave, every count,
every spacing. That is a genuine defect: gauntlet's towers cover 7.9 route-cells each against
crossroads' 5.7, on a 29-cell route instead of 19. The same table produces **zero leaks for nine waves**
here.

So the curve was reshaped for gauntlet's own defence: a base multiplier (enemies start tougher) with a
gentler ramp, swept over base 1.5–2.5 × growth 1.03–1.07.

It works as intended — leaks begin at wave 5 instead of wave 10, and mean lives moves smoothly
20 → 14.5 → 9.9 → 0.8. **And runs lost still steps**, 0% → 96% between growth 1.035 and 1.040 at base
2.0. Moving the mean earlier does not widen the distribution.

### 4. Wider chambers — no

The chambers are two rows deep, so a tower cannot sit in one without nearly sealing it: 153,692 seal
refusals per 100 runs. Rebuilt at three rows deep (9×13, geometry in band at 54% buildable, path 29).

Tower count rose **20.9 → 32.0**. Variance did not: sd 0.2–0.5. The cliff moved to 1.125→1.13 and kept
its shape.

More placements did not mean more *different* boards.

### 5. Twin connectors, to give the route a real choice — no, and it breaks the map

The most direct attack on the diagnosis: two connectors per dividing wall instead of one, so the flow
field can choose and a build can flip it.

It collapses the map. Path falls **29 → 17** (below the 18–30 band) and density rises **1.7 → 3.9**
(against a proposed max of 2.0), because open connectors mean creeps stop being funnelled. 100% of runs
lost at growth 1.09, leak 4.8%.

## The root cause

gauntlet's route is **architecturally fixed**. Three chambers joined by single-cell connectors: whatever
the player builds, creeps snake through the same three rooms in the same order. Towers change the route
*within* a chamber by a cell or two and never change its shape.

crossroads is a **mazing** map — its route runs over buildable cells and is a property of the flow
field, not the terrain. Different build orders there produce genuinely different mazes, different route
lengths, and therefore different outcomes. That is where sd 6.8 comes from.

> A map whose route cannot vary has one solution. One solution means one outcome. One outcome means a
> threshold, not a curve — and no wave table can add a distribution that the geometry forbids.

## What this says about the density target

This is the part that outlives gauntlet.

gauntlet exists to satisfy the proposed density target — 1.5–2.0 buildable cells per route cell, where
crossroads sits at 4.0 and "no enemy design survives it"
([the roster pass](2026-08-07-crossroads-enemy-roster-balance.md)).

**The way to score well on density is to wall the route in.** Fewer buildable cells per route cell means
either a longer route or less buildable space beside it, and the cheapest way to get both is corridors
and chambers — which is exactly what removes mazing.

Both variants tried here that restored route freedom moved the geometry straight back out of band:
three-row chambers took density to 2.2, twin connectors took it to 3.9 and the path to 17.

> **Density alone rewards maps that delete the central mechanic.** It measures how much defence a map
> permits and says nothing about whether the player has a decision.

`map-density-target` proposes promoting density to a `MapTargets` constant. **It should not be promoted
on its own.** It needs a companion measuring route variability — how much the route can change under
building — or it will keep selecting for gauntlets.

## Recommendation

- **Leave gauntlet at hpGrowth 1.09**, where it is trivial (0% leak, 0% lost, 20/20 lives) and clear of
  its edge. Tuning it into the 15–30% band is possible only at 1.12–1.125, where a 0.005 edit flips the
  game — that is a number that satisfies a target without meaning anything, which this project has now
  rejected three times for three different reasons.
- **Treat gauntlet as a documented negative result**, not a shippable second map. Its notes now say so.
- **crossroads remains the only balanced map.**

## Follow-ups

| Item | Workspace | Slug |
|---|---|---|
| Do not promote density to a `MapTargets` constant without a route-variability companion | content-data | `map-density-target` (amended) |
| A route-variability metric: how much can the route change under building? Would have predicted this before the map was built | tooling | `route-variance-metric` |
| A second *mazing* map, so crossroads is not the only balanced one | content-data | `second-mazing-map` |
| gauntlet still has no sappers, so "towers are permanent" holds there alone. Worth fixing on its own terms even though it does not fix the cliff | content-data | `gauntlet-sappers` |

## Reproduce

```bash
dotnet run -c Release --project Gridfall.Verify -- balance --map gauntlet --runs 200 --seed 1
dotnet run -c Release --project Gridfall.Verify -- balance --map crossroads --runs 200 --seed 1
```

The `sd` and `range` on the lives-left line are the comparison.
