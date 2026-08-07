# Analysis — income against difficulty

**Date:** 2026-08-07 · **Maps:** crossroads, gauntlet · **Runs:** 20–30 · **Seed:** 1
**Before:** [tighter map](2026-08-07-gauntlet-tighter-map-balance.md)

## Intent

Six passes each pushed a different lever and the gold went somewhere else every time. The one
relationship never examined was **income against enemy strength** — the ratio itself rather than any
one term in it.

This pass built a model of that ratio, used it to pick a candidate, tested the candidate, **found the
model wrong**, and swept for the real answer.

## The tool

`Verify curve` computes income and enemy strength wave by wave straight from the content — pure
arithmetic over the wave table, no simulation. `capacity` is what **cumulative** income could buy in
damage, because towers persist and a wave does not. That asymmetry is the whole problem.

```
dotnet run --project Gridfall.Verify -- curve --map crossroads
```

## What the model showed

At the shipped `hpGrowth` of 1.03, the player's capacity-to-threat ratio grows **6.4× over 12 waves**.
That is the trivial late game, quantified for the first time.

Solving for flat:

| hpGrowth | ratio at wave 7 | at wave 12 |
|---|---|---|
| 1.03 | 4.58 | 6.41 |
| 1.15 | 2.64 | 2.13 |
| **1.25** | **1.74** | **0.93** |
| 1.40 | 0.99 | 0.30 |

The model said **1.25**: player comfortably ahead mid-game, slightly behind by wave 12. On paper, a
textbook difficulty curve.

## The model was wrong

| hpGrowth | crossroads leak / lost |
|---|---|
| 1.03 | 0.5% / 5% |
| 1.15 | 5.7% / **90%** |
| 1.25 | 21% / **100%** |

1.25 kills every run. The model over-predicts the player by a wide margin, and the reason is
instructive: **it treats capacity as total damage, when what matters is throughput.** A creep passes
each tower once. Damage that exists but cannot be delivered before the goal does not count. The model
computes an upper bound the player never reaches.

Kept anyway — it correctly identified the *shape* of the problem and the direction of the fix, and it
is the only tool here that explains *why* rather than *whether*. But its absolute numbers are a
ceiling, not a prediction, and that is now written on it.

## The real answer, by sweep

| hpGrowth | crossroads leak / lost |
|---|---|
| 1.05 | 0.6% / 10% |
| 1.07 | 1.0% / 15% |
| **1.09** | **1.1% / 20%** |
| 1.11 | 2.5% / 60% |
| 1.13 | 4.7% / 70% |

**Shipped 1.09.** Leak 1.1% against a ≤4% target, runs lost 20% against a 15–30% target.

**This is the first configuration in the project's history to hit the runs-lost target.** Twelve waves,
a real ramp, and a late game that can actually kill you.

## gauntlet has a cliff

The map built last pass to satisfy the density metric does not work at any growth rate:

| hpGrowth | gauntlet leak / lost |
|---|---|
| 1.11 | 0.0% / **0%** |
| 1.12 | 1.9% / **0%** |
| **1.125** | 2.3% / **90%** |
| 1.13 | 2.6% / 90% |

**A 0.005 change in growth takes it from untouchable to unsurvivable.** That is not a difficulty curve,
it is a threshold, and it fails pillar 4 — a loss you cannot see coming is not explainable.

The cause is the shape that made it look good on the density metric: chambers let each tower cover
several legs, so the defence is highly efficient and uniform. It holds perfectly until enemy HP crosses
what those ~21 towers can kill in the time available, and then everything leaks at once. crossroads
spreads more towers more thinly and therefore **degrades gracefully**.

So the previous pass's conclusion inverts again: by the density metric gauntlet is the better map, and
by the difficulty curve it is much worse. Shipped at 1.09, far from its edge, and flagged.

## What this pass actually established

- The trivial late game was a **6.4× capacity-to-threat divergence**, now measured rather than inferred.
- It is fixed by a growth rate an order of magnitude higher than shipped — 1.09 against 1.03.
- The earlier verdict that "1.10 loses 80% of runs" was taken **before** upgrades and before
  `startingGold` 300. With the economy fixed, the playable band moved, and the disputed 1.10–1.18
  target turns out to have been much closer to right than my measurement of it.
- **Graceful degradation is a map property, and nothing measures it.** Two maps can hit identical
  balance numbers and have completely different failure shapes.

| Follow-up | Workspace | Slug |
|---|---|---|
| **Rework gauntlet's cliff** — or accept that chamber maps need a different wave shape | content-data | `gauntlet-cliff` |
| **A difficulty-slope metric**: sweep growth and report how sharply runs-lost changes. Nothing currently catches a knife-edge map | tooling | `difficulty-slope` |
| Resolve the HP-growth target now that the band is nearly vindicated — 1.09 sits just below 1.10 | content-data | `hp-growth-target` |
| Model throughput, not just total damage, so `curve` predicts rather than bounds | tooling | `throughput-model` |

## Trace re-recorded

`hpGrowth` 1.03 → 1.09 changes every creep's HP from wave 2 on. `17ed9dca` → `78975e4e`.

## Reproduce

```bash
dotnet run --project Gridfall.Verify -- curve --map crossroads
dotnet run --project Gridfall.Verify -c Release -- balance --map crossroads --runs 30 --seed 1
```
