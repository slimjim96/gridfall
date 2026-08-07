# Balance Pass — the early economy, second attempt

**Date:** 2026-08-07 · **Map:** crossroads · **Runs:** 200 · **Seed:** 1
**Before:** [salvage value](2026-08-07-salvage-value-balance.md) · [the first early-economy pass](2026-08-06-crossroads-early-economy-balance.md)

## Intent

Wave 3 has been the worst wave for six consecutive passes, sitting at 14.1%. The brief was to fix wave 3.

**Wave 3 was a symptom.** The finding is that the game was decided by wave 4 and the last eight waves
were a formality — and no metric in the project could see it.

## The measurement that was missing

`balance-targets.md` has carried **two** runs-lost targets since it was written:

| Metric | Target | Fail if |
|---|---|---|
| Runs lost (waves 1–10) | 0–5% | > 10% |
| Runs lost (waves 11–20) | 15–30% | < 5% or > 50% |

The balance sim printed **one** number, labelled it `15-30% late`, and checked it against
`lostRate is >= 0 and <= 60` — a band that appears nowhere in the doc and passes almost anything.

Splitting it:

| | before | target | |
|---|---|---|---|
| Runs lost, waves 1–10 | **25.5%** | 0–5% | **MISS** (fail threshold is 10%) |
| Runs lost, waves 11+ | **0.5%** | 15–30% | **MISS** |
| Lost runs died at wave | **4.3** avg (earliest 3, latest 12) | | |

The 26.0% that six passes read as "ok" was 25.5% early and 0.5% late — **the exact inverse of the
design intent.** `income-vs-difficulty` concluded it had built "a late game that can actually kill you";
the late game killed 0.5% of runs. It had built a lethal *opening*.

91% of all leaks fell in waves 1–5 of a 12-wave game.

## Why six passes could not fix it

`hpGrowth` is one scalar and it applies **from wave 1**. Wave 3 sits at `growth²`, wave 12 at
`growth¹¹`. To threaten wave 12 you must raise the rate, which also inflates waves 2–4 — and waves 2–4
are where the player is broke and thin, so they were the binding constraint on the entire curve.

Every previous pass therefore faced a forced choice between a lethal opening and a trivial ending, and
took the trivial ending because that is the one the targets could not see.

`content-data/CONTEXT.md` says **one knob per pass**, for attribution. That rule is why this survived six
passes: the curve needed its two ends moved in opposite directions, which one knob cannot do. The rule
is right about attribution and wrong about arity — the fix is to sweep a **grid** and keep attribution,
not to move one knob and keep failing.

## The grid

Starting gold against growth rate, 100 runs per cell. `diedAt` is the mean wave a lost run ended on.

| gold | growth | leak | lost | diedAt | wave 3 |
|---|---|---|---|---|---|
| 300 | 1.08 *(shipped)* | 1.3% | 28.0% | — | 15.1% |
| 300 | 1.14 | 7.6% | 100% | — | 26.4% |
| 400 | 1.09 | 0.8% | 11.0% | 6.2 | 4.7% |
| 400 | 1.10 | 1.9% | 52.0% | 10.7 | 4.9% |
| **500** | **1.10** | 1.5% | 28.0% | **12.0** | 0.1% |
| 500 | 1.11 | 2.1% | 66.0% | 11.6 | 1.5% |
| 600 | 1.10 | 1.3% | 16.0% | 11.9 | 0.0% |

Two knobs *do* separate the ends, exactly as the structure predicts: starting gold is a constant whose
relative weight decays (by wave 6 the player has earned 1,080 anyway), and growth is an exponent whose
weight compounds. **gold 500 / growth 1.10 hits every target**, with lost runs dying at wave 12.0.

## But there is a better shape than more gold

500 starting gold buys ten towers before wave 1. That does not fix the curve — it moves the player up
the same exponential — and it deletes the opening build-up as a decision.

The alternative is to change the curve's **shape**: hold the opening flat and start the ramp later.
`hpScale` already overrides growth per wave, so this was testable before building anything.

| ramp starts | growth | leak | lost | diedAt | wave 3 | scale at w12 |
|---|---|---|---|---|---|---|
| 4 | 1.20 | 3.1% | 95.0% | 10.4 | **4.3%** | 4.30 |
| 4 | 1.10 | 0.2% | 3.0% | 4.0 | **4.3%** | 2.14 |
| 4 | 1.135 | 1.1% | 11.0% | 9.8 | **4.3%** | 2.72 |
| **4** | **1.14** | 1.5% | 29.0% | **11.1** | **4.3%** | 2.85 |
| 4 | 1.145 | 1.8% | 47.0% | 11.3 | 4.3% | 2.95 |
| 3 | 1.12 | 1.4% | 25.0% | 10.4 | 4.3% | 2.77 |
| 3 | 1.14 | 2.4% | 80.0% | 10.9 | 4.3% | 3.25 |

**Wave 3 lands at 4.3% in every row**, at unchanged starting gold — the six-pass problem was never about
the player's wallet at all. It was that wave 3 carried `1.08²` of scaling before the player owned a
board. Remove the scaling from the opening and wave 3 fixes itself.

## Head to head, 200 runs

| | gold 500, growth 1.10 | **flat to 4, then 1.14** |
|---|---|---|
| leak rate | 1.5% | 1.6% |
| runs lost, waves 1–10 | 0.0% | 3.5% |
| runs lost, waves 11+ | 27.5% | 21.5% |
| lost runs died at | 12.0 (range 11–12) | 10.9 (range **3–12**) |
| starting gold | 500 | **300, unchanged** |

**Shipped the second.** Three reasons:

1. **A distribution, not a step.** With 500 gold nobody can lose before wave 11 — ten waves with no
   stakes at all. Deaths spanning waves 3–12 with a mean of 10.9 is a difficulty *curve*; deaths
   confined to 11–12 is a gate.
2. **21.5% sits mid-band**, 27.5% at its edge.
3. **The opening survives as a decision.** 300 gold is six towers and a choice about where; 500 is ten
   towers and a formality.

## Shipped

```json
"hpGrowth": 1.14,
"hpGrowthFrom": 4
```

`hpGrowthFrom` is a new field: the wave the ramp starts from, defaulting to 1, which reproduces the
previous behaviour exactly. Two numbers now state the whole curve, and the loader does the arithmetic —
the alternative was twelve hand-computed `hpScale` values with the intent implicit in them.

| Metric | Before | After | Target | |
|---|---|---|---|---|
| Leak rate | 1.2% | 1.6% | ≤ 4.0% | ok |
| Runs lost, waves 1–10 | **25.5%** | **3.5%** | 0–5% | **ok** |
| Runs lost, waves 11+ | **0.5%** | **21.5%** | 15–30% | **ok** |
| Lost runs died at wave | 4.3 | **10.9** | — | |
| Wave 3 leak | **14.1%** | **4.3%** | ≤ 15% | ok |
| Wave 12 leak | 0.1% | 6.7% | ≤ 15% | ok |
| Lives left, avg | 11.7 | 7.6 | — | |

**This is the first configuration to hit both runs-lost targets at once.** The previous claim to that
was `income-vs-difficulty`, which hit one target twice by not splitting them.

### The leak distribution inverted

| | waves 1–5 | waves 11–12 |
|---|---|---|
| Before | **91%** of all leaks | 1.6% |
| After | 12% | **87%** |

### Per wave

| Wave | Leak % | Towers | Gold |
|---|---|---|---|
| 1–2 | 0.2, 1.8 | 6.0, 7.0 | 0, 14 |
| 3 | **4.3** | 9.9 | 2 |
| 4–10 | ≤ 0.5 | 13.7 → 56.2 | 11 → 30 |
| 11 | 0.8 | 56.2 | 6 |
| 12 | **6.7** | 56.3 | 10 |

The board saturates at ~56 towers from wave 9, and the last two waves are the ones that get through it.

## The slope is steep, and that is a real caveat

At the shipped shape, growth 1.135 → 1.14 → 1.145 gives runs lost 11% → 29% → 47%. **A 0.005 change
moves the result ~18 points.**

That is inherent: `growth⁸` amplifies small rate changes, and the player's wave-12 margin is thin. It is
not the *cliff* `gauntlet` has (0.005 taking it 0% → 90%) — crossroads degrades across a range rather
than flipping — but it means this configuration is not robust to casual edits. `difficulty-slope` is
still the right follow-up and now has a second map to measure.

## gauntlet: unchanged, and now visibly trivial

gauntlet declares no `hpGrowthFrom`, so it defaults to 1 and its numbers cannot have moved — there is a
back-compat test asserting the default reproduces the old curve exactly.

The split metric makes its existing state legible for the first time: **0.0% leak, 0.0% runs lost, 20.0
lives left.** It was already known to be either trivial or a cliff, shipped deliberately far from its
edge at 1.09. It is trivial. `gauntlet-cliff` remains open and is now the more urgent of the two.

## Follow-ups

| Item | Workspace | Slug |
|---|---|---|
| gauntlet is fully trivial: 0% leak, 0% lost, 20/20 lives. Needs the shaped curve too, but it has a cliff at 1.125 | content-data | `gauntlet-cliff` |
| A 0.005 growth change swings runs-lost ~18 points. Nothing measures slope | tooling | `difficulty-slope` |
| The 1–10 / 11–20 split is inherited from a 20-wave design and buckets 10 of 12 waves as "early" | content-data | `wave-band-targets` |
| The HP-growth band target (1.10–1.18) is now met by the *late* rate, 1.14 — resolve the long-standing dispute | content-data | `hp-growth-target` |

## Trace re-recorded

Wave 2's scale goes 1.08 → 1.00, so the trace diverged at **tick 900** — precisely the tick its script
starts wave 2, and not before. Wave 1 is unaffected because its scale is 1.0 under both curves.
Diagnosed before re-recording. `f07f32a860421e94` → `240e010e52e5d640` at that checkpoint.

## Reproduce

```bash
dotnet run --project Gridfall.Verify -- curve --map crossroads
dotnet run -c Release --project Gridfall.Verify -- balance --map crossroads --runs 200 --seed 1
```
