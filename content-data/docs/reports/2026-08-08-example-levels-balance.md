# Example Levels — Balance Pass

**Date:** 2026-08-08 · **Maps:** all twelve · **Runs:** 150 per map, seed 1, competent-beginner policy
· **Verdict:** the scope call answered, and not the way it was framed

The open question was *"a per-level balance pass for all ten, or pick the two or three that earn a
tuning budget?"* Neither. Measuring first turned up three things that make tuning individual levels
the wrong next move.

---

## 1. The reference board fails both targets, and the accident passes both

`balance-targets.md` asks for **0–5% of runs lost in waves 1–10** and **15–30% in waves 11–20**.
Measured today:

| Map | Early (0–5%) | Late (15–30%) | |
|---|---|---|---|
| **`spiral`** | **0.0%** ok | **25.3%** ok | **passes both — the only board that does** |
| `crossroads` | 18.7% MISS | 1.3% MISS | the reference board, inverted |
| `comb` | 0.0% ok | 42.0% MISS | too hard, late |
| `chambers` | 0.7% ok | 0.0% MISS | dies early or not at all |
| every other map | 0.0% ok | 0.0% MISS | never dies |

`crossroads` is the board every tuning pass has been measured against and the only one previously
described as tuned. It loses **18.7% of runs in the first ten waves and 1.3% after** — players die
while learning and coast once they have a board, the exact inverse of the intent. This was already
written down on 2026-08-07 (`early-economy-2`) and is unchanged.

**`spiral` now passes both bands, and nobody tuned it.** Its wave table is still `crossroads`'s,
copied verbatim. It got there by having five walled-off buildable cells sealed — see
[`example-levels.md`](../example-levels.md) — which moved it 41.3% → 25.3%. The best-balanced board in
the repo is a generator artefact that was corrected by a bug fix.

## 2. The late band is measured over two waves, not ten

The target reads **waves 11–20**. Every wave table in `content-data/waves/` has **12 waves**. So the
"late" window is waves 11 and 12, and `Verify balance` labels it `in waves 11+`.

15–30% spread over ten waves is a gentle per-wave rate. The same 15–30% over two waves requires one
wave to be close to a coin flip. That is not a band a level can sit in stably, and it is why the
knobs behave the way §3 describes.

**This is a target that has outgrown its content.** Either the tables grow to 20 waves, or the band is
restated for the 12 that exist. Until one of those happens, "late runs lost" is not a number worth
tuning towards, and six previous passes reading it as `ok` is the documented history of what happens
when it is.

## 3. `comb` cannot be landed in the band with the documented knobs

`comb` is the strongest level in the set — hardest, most legible, the only geometry doing real work at
2.1× the floor. It is also the one that most obviously needs tuning at 42.0% late. It does not tune.

Every lost run dies at **wave 12, earliest 12, latest 12**. The failure is a single wave, so every
global knob is a threshold around it rather than a dial through it:

| Knob | Value | Late runs lost |
|---|---|---|
| `hpGrowthFrom` | 6 (shipped) | 42.0% |
| | 7 | 0.0% (9 lives left, sd 0.0) |
| | 8 | 0.0% (20 lives, untouched) |
| | 9 | 0.0% |
| `waveClearGold` | 25 (shipped) | 42.0% |
| | 35 | 0.0% |
| | 45 | **52.0%** |
| | 60 | 0.0% |

`hpGrowth` itself cannot move: it is already at 1.10, the floor of the documented 1.10–1.18 band.

The `waveClearGold` row is the important one — **it is not monotone.** More income makes the map
*harder* at 45 than at 35 and easier again at 60. Income changes what the policy buys and where it
puts it, which changes what it seals, which changes whether wave 12 breaks through. There is no
value to interpolate to; the surface is not smooth.

A number forced into 15–30% here would be luck, and it would move the next time anything else changed.
**`comb` needs its wave 12 spread across waves 8–15, not a global multiplier retuned.** That is a wave
composition job and it is worth doing — after §2 is settled, because the target it would aim at is
currently a two-wave window.

---

## What this says about `route-variance-metric`

The hunt has been for a map metric predicting **sd of lives left**. That statistic is measuring two
different shapes and cannot tell them apart:

| Map | sd | Lost runs died at wave | Shape |
|---|---|---|---|
| `crossroads` | 8.1 | 3.7 avg, **earliest 3, latest 12** | a curve — many waves can kill you |
| `comb` | 5.9 | 12.0, earliest 12, latest 12 | **a wall** — one wave, pass or fail |
| `spiral` | 6.7 | 12.0, earliest 12, latest 12 | a wall |
| `chambers` | 5.7 | 3.0, earliest 3, latest 3 | a wall, early |

`comb` and `crossroads` have similar sd and nothing else in common. One has a difficulty curve; the
other has a coin flip at wave 12 and is otherwise deterministic. **A geometric property of the map
could never have separated those, because the difference is in the wave table.** Three geometric
candidates were ruled out against a target variable that was mixing two populations.

**Death-wave spread** — `latest - earliest` over lost runs — separates them immediately, and it
separates `crossroads` from all eleven other maps: spread 9 against spread 0 everywhere else. That is
the one board anyone has called tuned.

Do not over-read it: **n = 1.** One well-regarded map has spread and no other map has any, which is as
consistent with "spread is what tuning produces" as with "spread is what tuning requires". It is not
a metric yet. It is a better-posed target than sd, and the first thing to check is whether spreading
`comb`'s wave 12 (§3) produces spread — because if difficulty spread is a property of wave tables
rather than of maps, then `route-variance-metric` has been looking in the wrong workspace the whole
time.

---

## Corrections to the committed table

Two rows in `example-levels.md` did not reproduce, independent of anything changed today. Re-measured
at 150 runs and confirmed identical in Debug and Release:

| Map | Doc said | Measured | Probable cause |
|---|---|---|---|
| `braid` | 0.7% lost, 18.6 lives, sd 2.9 | **0.0%, 19.9, sd 0.2** | predates `tower-range-tiers` |
| `switchback` | 0.0%, 16.9, sd 4.1 | **0.0%, 19.3, sd 1.0** | same |

`braid` at sd 0.2 is **degenerate, not "too easy"** — that moves the degenerate count from four to
five. Rows for `spiral` and `driftway` also changed, but those are explained by the stranded-cell
repair.

## Nothing was tuned

No wave table, tower or enemy number was changed by this pass. Every sweep above was run against a
scratch copy and reverted; `git status` was clean before and after. The one content change of the day
was the stranded-cell repair, and it is recorded in `example-levels.md`.
