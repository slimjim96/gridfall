# Balance Pass — a tighter map

**Date:** 2026-08-07 · **Maps:** gauntlet (new) vs crossroads · **Runs:** 30 · **Seed:** 1
**Before:** [enemy roster](2026-08-07-crossroads-enemy-roster-balance.md)

## Intent

The roster pass found that `crossroads` allows 55 towers against a 19-cell route — 4.0 buildable cells
per route cell — and concluded the late game was trivial because the board holds more defence than any
enemy can overcome.

This pass built a map designed against that number. **It did not work, and the reason is the most
useful thing found so far.**

## What shipped

`gauntlet`, 10×10. Three two-row chambers joined by single-cell connectors.

| Target | crossroads | gauntlet |
|---|---|---|
| Unmazed path (18–30) | 19 | **29** |
| Buildable (35–55%) | 42% | **49%** |
| Buildable per route cell (proposed ≤ 2.0) | **4.0 — fails** | **1.7** |

It is the first shipped map where a build **can** seal the lane, so the never-fully-blockable refusal
is finally reachable in a real game rather than only in tests. The sim refuses ~168,000 such placements
across 5 runs.

## The result

| | crossroads | gauntlet |
|---|---|---|
| Towers standing | 52.8 | **20.8** |
| Leak rate | 0.5% | **0.0%** |
| Runs lost | 6.7% | **0.0%** |
| Lives left | 15.5 | **20.0** |

The density fix worked exactly as designed — tower count fell by 60%. **And the map got easier.**

## Why: gold is conserved

| | crossroads | gauntlet |
|---|---|---|
| Total route-cells covered | 308 | **165** |
| Coverage per tower | 5.8 | **7.9** |
| Upgrades per tower | 0.77 | **1.82** |

Two things happened, and together they cancel the intervention:

1. **A winding route makes each tower worth more.** A tower in a chamber has range onto two legs at
   once, so coverage per tower rose 36%.
2. **Gold that cannot buy breadth buys depth.** With nowhere left to build, the policy poured income
   into upgrades — 1.8 per tower against 0.77. A level-3 tower does 4× damage.

Half the coverage, but each point of it hits roughly four times as hard. The defence came out the same.

## The actual structure, after six passes

Every pass has tried to stop the player out-scaling the enemy, and each time the pressure moved
somewhere else:

| Pass | Intervention | Where the gold went |
|---|---|---|
| more-waves | more creeps | more bounty → more towers |
| wave-scaling | tougher creeps | more towers still |
| tower-upgrades | a sink for idle gold | depth |
| early-economy | more starting gold | more towers, earlier |
| enemy-roster | enemies that resist a monoculture | unchanged |
| **tighter-map** | **less room to build** | **depth again** |

The invariant nobody has touched:

> **Total defence tracks cumulative income. Constraining any one sink diverts gold to another.**

Cumulative income grows with the *sum* of all creeps killed so far. Per-wave enemy strength grows with
that wave's creeps × 1.03^n. A running total outgrows a per-wave figure, so the player pulls ahead
however the board is shaped — and no amount of map geometry, enemy statistics, or sink design changes
that, because none of them touch the ratio.

## Verdict

**`gauntlet` ships.** It is a better map on every stated measure, it makes tower placement a real
decision instead of an exercise in filling space, and it exercises a rule no shipped map previously
reached. `crossroads` stays as the comparison case and as the reason the density metric exists.

But the late game is still not fixed, and this is the sixth pass to find that out. **The next attempt
should not be another sink or another stat.** The one relationship never examined is
**bounty-per-creep against HP-growth-per-wave** — the income-to-difficulty ratio itself.

| Follow-up | Workspace | Slug |
|---|---|---|
| **Model income against enemy strength across 12 waves and find the ratio where they track.** Every other lever has now been shown not to matter | content-data / game-design | `income-vs-difficulty` |
| Make density a `MapTargets` constant now that a passing map exists to calibrate against | content-data / tooling | `map-density-target` |
| A wave table authored for gauntlet's shape — it currently reuses crossroads' | content-data | `gauntlet-waves` |

## Reproduce

```bash
dotnet run --project Gridfall.Verify -- maps
dotnet run --project Gridfall.Verify -c Release -- balance --map gauntlet --runs 30 --seed 1
```
