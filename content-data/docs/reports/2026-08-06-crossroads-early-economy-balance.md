# Balance Pass — the early economy

**Date:** 2026-08-06 · **Map:** crossroads · **Runs:** 30 · **Seed:** 1
**Policy:** competent-beginner (**corrected mid-pass — see below**)
**Before:** [HP scaling](2026-08-06-crossroads-hp-scaling-balance.md) · [tower upgrades](../../../production/06-release/tower-upgrades-v1.md)

## Intent

Wave 3 leaked 21.5% while the player held 12 gold. Every previous pass named this as the last
structural problem: the danger is being broke, not the wave being strong.

**One knob changed: `startingGold` 200 → 300.**

## First, the policy was wrong again

Instrumenting towers-on-board per wave showed the player entering **wave 1 with one tower** while
holding 200 gold — four towers' worth. The policy made a single build attempt between waves and then
pulled the next one immediately.

Fixed: it now spends down before starting a wave, one purchase per tick, and only pulls the wave when
gold no longer buys anything useful. Wave 1 now opens with 6 towers.

**That did not fix the cliff** — wave 3 went 21.5% → 23.2%. So the cliff is real. But every number
below is from the corrected policy, and the earlier ones were partly measuring the player.

This is the **third** time the policy has distorted a diagnosis. The rule earned by now: when a balance
result looks structural, instrument the player before believing it.

## Diagnosis

| Wave | Towers at start | Leak % |
|---|---|---|
| 1 | 4.0 | 0.0 |
| 2 | 5.0 | 8.6 |
| 3 | 7.5 | **23.2** |
| 4 | 10.6 | 14.9 |
| 5 | 15.8 | 1.5 |

The defence crosses "adequate" somewhere around 12–15 towers, and income delivers roughly 2.5 towers
per wave. So there is a two-wave window where the board is simply too thin, and it lands on wave 3.

**Not the brutes.** Removing wave 3's brutes dropped it to 14.7% and pushed wave 4 to 21.5% — the
cliff moved rather than disappearing. It is a systemic income-versus-scaling mismatch that surfaces
wherever the crossover falls.

## The sweep

| Change | Leak | Runs lost | Wave 3 |
|---|---|---|---|
| baseline | 2.9% | 35% | 20.5% |
| bounty +50% | 0.7% | — | 9.8% |
| startingGold 240 | 1.7% | 20% | 17.8% |
| startingGold 270 | 1.6% | 15% | 16.8% |
| **startingGold 300** | **0.9%** | **6.7%** | **13.5%** |
| startingGold 350 | 0.5% | — | 7.8% |

Bounty and starting gold both fix it. **Starting gold is the better lever**, and the reason matters:
bounty scales with every kill, so it inflates late income too — exactly where upgrades just finished
stopping a runaway. Starting gold is a one-time boost that lands only in the window that is broken.

300 is the smallest value that clears the 15% per-wave ceiling.

## Results at 300

| Metric | Value | Target | |
|---|---|---|---|
| Leak rate | 0.9% | ≤ 4.0% | ok |
| Runs lost | 6.7% | 0–5% waves 1–10 | marginally over |
| Wave 3 | 13.5% | ≤ 15% per wave | ok |
| Lives left, avg | 15.8 / 20 | — | |

| Wave | Leak % | Towers |
|---|---|---|
| 1–2 | 0.0, 2.4 | 6, 7 |
| 3 | **13.5** | 9.8 |
| 4 | 2.7 | 13.2 |
| 5–11 | **0.0** | 18 → 55 |

## Verdict

**Kept at 300.** The wave-3 cliff is gone: 21.5% → 13.5%, under the ceiling, and the difficulty now
rises and falls rather than spiking.

But be clear about what this did *not* do. **Waves 5–11 still leak nothing.** The early game is fixed;
the game still has no late game, and that has now survived three separate attempts to fix it from the
economy side. The remaining problem is not economic at all — it is that a defence of 55 towers cannot
be threatened by any number of two enemy archetypes.

| Follow-up | Workspace | Slug |
|---|---|---|
| **Enemy roster** — 2 archetypes against a 4–7 target, and both die to the same tower. This is now the binding constraint on the whole game | content-data / game-design | `enemy-roster` |
| Runs lost 6.7% against a 0–5% early target — marginal, revisit once the late game exists | content-data | — |
| The HP-growth band is still disputed at 1.10–1.18 vs a measured 1.02–1.04 | content-data | `hp-growth-target` |

## Trace re-recorded

`startingGold` is hashed state, so it diverged at **tick 0** — the earliest possible point, as expected
for a value present before the first tick. Diagnosed before re-recording.
`17ed9dcac986f821` → `bd132968cc9f2ef5`.

## Reproduce

```bash
dotnet run --project Gridfall.Verify -c Release -- balance --map crossroads --runs 30 --seed 1
```
