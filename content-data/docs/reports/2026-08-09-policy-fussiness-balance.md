# Play Policy — Fussiness — Balance Pass

**Date:** 2026-08-09 · **Maps:** all twelve · **Runs:** 150 per map, seed 1 · **Slug:** `policy-fussiness`
· **Verdict:** the change was correct, the prediction that came with it was wrong, and the wrong part
is the finding

The premise in [`next-steps.md`](../../../production/docs/next-steps.md) §1 was: *"there is a real
rock-paper-scissors in the roster, and every balance number in the repo describes a game that never
uses half of it. Small change, large blast radius: it would move every figure in `example-levels.md`
and the balance report."*

The first sentence is true. The second is false, and it is false for a reason worth writing down.

---

## 1. What was broken — two blocks, not one

`PlayPolicy` ranked stations on **base** serving-per-gold, which is not a well-defined quantity when
`fussiness` is subtracted per hit. Against the shipped roster the ordering inverts:

| serving per second per gold | arrow (12 / 0.6 s / 50 g) | cannon (40 / 1.5 s / 90 g) |
|---|---|---|
| fussiness 0 | **0.400** | 0.296 |
| fussiness 4 | 0.267 | 0.267 |
| fussiness 8 (`husk`) | 0.133 | **0.237** |

Fixing only that changes nothing, because there was a **second, independent block**: the policy bought
the best station it could afford *this tick* and kept no reserve. On any roster that means the cheapest
station is bought the instant its price is reached and gold never approaches the price of anything
else. With a 50 g station on the board, a 90 g station is unreachable regardless of what it is worth.

Measured directly: on a board of nothing but husks, the census-aware policy without the second fix
built **2 arrow stations and 0 cannons**. Both fixes together, it builds cannons — asserted in
`PolicyFussinessTests.ThePolicyActuallyBuysTheCannon_WhenTheCensusWarrantsIt`.

## 2. The result: all twelve maps, byte-identical

150 runs, seed 1, before and after. Every line of every report matches, including the per-wave tables:

| Level | Runs lost | Lives left | sd | Death wave | Δ from the policy change |
|---|---|---|---|---|---|
| `comb` | 42.0% | 7.0 | 5.9 | 12 only | **none** |
| `spiral` | 26.7% | 7.4 | 6.6 | 12 only | **none** |
| `crossroads` | 20.0% | 13.1 | 8.1 | 3, 4, 5, 11, 12 | **none** |
| the other nine | 0.0% | — | — | — | **none** |

And the new `station mix` line, on all twelve boards:

```
  station mix     arrow-station 100%, cannon 0%
```

The policy can now buy a cannon and still never does. **That is a content finding, not a harness bug.**

## 3. Why — the husk is 16.5% of a wave and needs to be ~48%

The crossover is at average fussiness **4**. Weighted by appetite — the right weight, because a station
is bought to chew through health — the shipped tables peak at **1.53**, on wave 12, on every map:

| Map | Worst wave | Avg fussiness | arrow ÷ cannon | Husk share of appetite |
|---|---|---|---|---|
| `gauntlet` | 12 | 1.48 | 1.229 | 18.5% |
| every other map | 12 | 1.53 | **1.225** | 16.5% |

Even at its most armoured, the arrow station is **22.5% better value than the cannon**. The policy's
own census is cumulative and therefore flatter still: 1.15 by the end of a run.

Three ways to close that gap, priced against the shipped wave 12:

| Lever | What it would take | Note |
|---|---|---|
| **More husks** | 19 → **90** in wave 12 (48% of the wave's appetite) | Nearly five times the count. Wave 12 has 147 visitors; this makes it 218 |
| **Fussier husks** | **impossible at 19 husks** | Raising `fussiness` hurts *both* stations. Above 12 the arrow is already floored at 1 per hit and further increases only subtract from the cannon. The ratio is best at fussiness 11, and even there 19 husks do not flip it |
| **A better cannon** | cost 90 → **73** (−19%), or serving 40 → **49** (+22%) | One number, in one file. It also changes every board that offers a cannon |

The second row is the interesting one. **Fussiness has a maximum useful value**, and past it the
mechanic works against the archetype it is meant to reward. Nothing in the content or the docs said so.

## 4. The station targets that were never measured

[`balance-targets.md`](../balance-targets.md) has carried two roster targets since it was written:

| Target | Measured today |
|---|---|
| Share of a roster used in a winning run ≥ 4 of 8 distinct stations | **1 of 2** |
| Any single station's presence in winning runs ≤ 70% — *"a must-pick is a design failure"* | **100%** |

Both fail, and neither had ever been measured, because nothing printed the mix. The `station mix` line
exists now, so the second one is checked on every balance run from here.

The roster is two stations, so "4 of 8" cannot be met by anything and the target is aspirational until
the roster grows. The 70% one is real and failing today.

## 5. Method

- Baseline: the pre-change binary, published to a scratch directory and run over all twelve maps before
  a line was edited. Not remembered, not read out of the last report — the 2026-08-08 report is
  slightly stale relative to today's tree (`spiral` 25.3% → 26.7%, `chambers` 0.7% → 0.0%, both from
  the map redraws recorded in [`example-levels.md`](../example-levels.md)).
- After: the same twelve maps, same seed, same run count.
- Diff: full report text, header lines excluded. Zero differences.
- `replay` passed unchanged — the `ServingTaken` extraction in `Gridfall.Core` is behaviour-identical,
  and the harness is outside the simulation, so no trace could have moved.

## What this leaves for a person

**Nothing here is blocked, and nothing here should be tuned without a decision.** The husk currently
asks *"do you have burst?"* and the honest answer at shipped composition is *"you don't need any."*
Three options above, all cheap, all with different blast radii:

- **The cannon at 73 g** is one number and makes the answer "yes" on wave 12 of every board. It also
  makes the cannon better value in the *late* game generally, which no board has been measured with.
- **90 husks in wave 12** keeps the stations alone and makes exactly one wave ask the question.
- **Doing nothing** is defensible if the husk is meant to be flavour rather than a decision — but then
  `husk.json`'s `_asks` field is claiming something the content does not deliver, and the balance
  targets in §4 should be restated rather than left failing.

This is a content-composition call, and it belongs with the same person holding
[§3 of `next-steps.md`](../../../production/docs/next-steps.md) (whether the ten generated boards are
the shipped set). The harness now measures the thing either way.
