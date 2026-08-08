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

## RESOLVED — the band was restated for twelve waves

**Decision (2026-08-08): restate, do not grow the tables to 20.** The split is now `waveCount / 2`, so
a 12-wave table splits 1–6 / 7–12 and `Verify balance` prints the range it actually used. Band values
are unchanged — they were never the problem.

Re-measured, all twelve, 150 runs, against the restated split. `by wave` is the share of all runs that
ended on that wave:

| Map | Lost | Early 1–6 | Late 7–12 | By wave | Waves that can kill |
|---|---|---|---|---|---|
| `comb` | 42.0% | 0.0% ok | 42.0% MISS | w12:42% | **1** |
| `spiral` | 25.3% | 0.0% ok | **25.3% ok** | w12:25% | **1** |
| `crossroads` | 20.0% | 18.7% MISS | 1.3% MISS | w3:17% w4:1% w5:1% w11:1% w12:1% | **5** |
| `chambers` | 0.7% | 0.7% ok | 0.0% MISS | w3:1% | 1 |
| the other eight | 0.0% | — | — | — | **0** |

**The histogram is the finding, not the split.** No map's verdict moved — deaths land on wave 3 or
wave 12, on the same side of either boundary. What the restatement bought is a target a level can
*reach*: six waves to distribute lethality across instead of two.

And it exposes the real defect. `spiral` passes both bands and is still not a good level — **every one
of its lost runs dies at wave 12.** A map with one lethal wave is a gate, and passing a percentage band
does not make it a difficulty curve. `crossroads` is the only board where more than one wave can end a
run, and its distribution is a 17% spike at wave 3 with a thin tail — not a curve either, but at least
five waves participate.

Hence the new target: **waves that can kill you ≥ 3.** It is the one `comb` genuinely fails, and it
explains why its knobs are cliffs — every global knob moves its single lethal wave through the
threshold all at once, so there is nothing to interpolate.

---

## `comb` retuned by composition — it does not work, and the reason matters

Fifteen configurations across waves 9–12, holding wave 9 near its original strength and reshaping the
top of the curve. Effective strength is `units × hpScale`, which is the number that actually matters —
`hpGrowth` 1.10 from wave 6 compounds on top of the counts.

| w10 / w11 / w12 (effective) | Late lost | By wave | Killing waves |
|---|---|---|---|
| 177 / 223 / 286 *(shipped)* | 42.0% | w12:42% | 1 |
| 240 / 250 / 258 | 42.0% | w12:42% | 1 |
| 242 / 244 / 242 | **0.0%** | — | 0 |
| **244 / 246 / 244** | 42.0% | w11:24% w12:18% | **2** |
| **246 / 248 / 246** | **16.7%** ok | w12:17% | 1 |
| 249 / 251 / 249 | 42.0% | w11:23% w12:19% | **2** |
| 254 / 248 / 240 | 23.3% ok | w12:23% | 1 |
| 261 / 253 / 244 | 42.0% | w12:42% | 1 |
| 234 / 250 / 266 | 100% | w11:100% | 1 |

**Never three killing waves.** Two is the ceiling, and only at 42% — out of band. Landing *in* band
always cost the spread.

### It is not monotone, and not by a little

242 → **0.0%**, 244 → **42.0%**, 246 → **16.7%**, 248 → **42.0%**. Weakening the late waves made the
level harder, then trivial, then harder again. There is no gradient to follow, so any config that
lands in band is luck: its neighbour two points away is 42%.

The mechanism is the same one that made `waveClearGold` non-monotone. Wave composition changes what
the policy can afford and when, which changes where it builds, which changes what it seals, which
changes the route — and the route is the level. Difficulty is not a dial on this board.

### 42.0% is structural, not noise

That figure recurs across five unrelated configurations and holds across seeds: **42.0, 42.0, 42.0,
41.3** at seeds 1–4, 150 runs each. Repeat runs are byte-stable.

So it is not the wave table. **42% of the boards this policy builds on `comb` cannot hold the endgame,
whatever the endgame is made of.** Composition moves *which wave* kills; it does not move *how many
runs* die.

### Which points back at the map

`comb` stands at **17.8 towers at end from 243.7 built**, with **102,682 placements refused by the seal
check** — the highest in the set, against `crossroads`'s 25,879. Its interlocking teeth are what make
the route 2.1× the floor, and the same teeth mean almost every buildable cell would wall the route off.
The policy runs out of legal places to stand, its defence caps out around wave 5, and from there the
outcome is decided by which cells it happened to take.

**So `comb`'s problem is geometric after all — just not the geometry anyone was measuring.** Not route
shape, not maze multiplier, not `useful`: the number that matters is how much defence the board can
actually hold. `comb` cannot be fixed in `content-data/waves/`. Widening its pockets is a map change.

**Nothing was committed.** `comb.json` is byte-identical to what it was; every configuration above ran
against a scratch copy.

---

## 2. The late band was measured over two waves, not ten *(the problem, now fixed)*

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

### Then the obvious test, run: spread is bounded by runway

If death-wave spread is a wave-table property rather than a map property, extending the table should
produce spread. `comb` extended to 20 waves, scratch copy, three ramps:

| Waves 13–20 count growth | Late runs lost | Died at wave |
|---|---|---|
| — (shipped, 12 waves) | 42.0% | 12.0, **earliest 12, latest 12** — spread 0 |
| ×1.21/wave (extrapolated) | 100% | 12.6, earliest 12, **latest 13** — spread 1 |
| ×1.03/wave | 100% | 13.2, earliest 12, **latest 14** — spread 2 |
| ×1.00/wave (counts flat) | 100% | 13.2, earliest 12, **latest 14** — spread 2 |

Flat counts still kill every run by wave 14, because `hpGrowth` 1.10 compounding from wave 6 reaches
~3.8× by wave 20 while `comb`'s defence plateaus at 18 standing towers from wave 5. But the shape of
the answer is clear and it does not depend on the ramp:

> **A map cannot show death-wave spread after the last wave in its table.** `comb`'s difficulty
> crossing falls at wave 12 of 12, so its spread is 0 *by construction* — there is no runway for the
> variance to express in. Add runway and spread appears immediately, at 1 then 2.

`crossroads` has spread 9 because its crossing falls at **wave 3**, leaving nine waves for outcomes to
separate, and because it is recoverable: 41 standing towers and 316 built per run means crossing the
line is survivable. `comb` caps at 18 standing and dies within two waves of crossing.

**So `route-variance-metric` is blocked on §2, not open on its own.** Every map except `crossroads`
has its crossing at or near the last wave, which pins death-wave spread at 0 for eleven of twelve maps
regardless of their geometry — and pins sd to a coin flip on one wave. No map metric can be validated
against a target that the table length has already flattened. Settle the wave count, then re-measure;
the discriminator may well be visible then, and is not now.

Standing-tower count was checked as a candidate on the way and does not work either: `braid` and
`ringfort` both hold 37.4 towers and are both degenerate, while `crossroads` holds 41.3 and `comb`
17.8 and both have real outcomes.

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
