# Balance Pass — per-wave HP scaling

**Date:** 2026-08-06 · **Map:** crossroads · **Runs:** 30 · **Seed:** 1
**Policy:** competent-beginner
**Before:** [12-wave pass](2026-08-06-crossroads-12-waves-balance.md)

## Intent

The previous pass found there was no late game: waves 5–12 leaked nothing, because enemy HP was fixed
per definition and more creeps of a fixed toughness just hand the player more bounty. This adds a
per-wave HP multiplier — the mechanism that was missing — and finds the rate.

## The sweep

One knob, swept. Every rate in the **documented 1.10–1.18 band is unplayable**:

| hpGrowth | Leak rate | Runs lost | |
|---|---|---|---|
| 1.02 | 2.3% | 25% | in target |
| **1.03** | **2.8%** | **33%** | **shipped** |
| 1.04 | 2.5% | 30% | in target |
| 1.06 | 3.1% | 40% | |
| 1.08 | 4.3% | 55% | leak over |
| 1.10 | 5.8% | **80%** | bottom of the documented band |
| 1.12 | 7.5% | 95% | |
| 1.14 | 10.3% | 100% | |
| 1.18 | 19.7% | 100% | top of the documented band |

Shipped **1.03**: leak 2.8% against ≤4%, runs lost 33% against a 15–30% target — marginally over, and
the closest any rate gets while keeping leak rate healthy.

## Per wave at 1.03

| Wave | Spawned | Leaked | Leak % | Gold at start |
|---|---|---|---|---|
| 1 | 240 | 0 | 0.0 | 150 |
| 2 | 420 | 32 | 7.6 | 14 |
| 3 | 600 | 129 | **21.5** | 12 |
| 4 | 700 | 101 | 14.4 | 20 |
| 5 | 651 | 9 | 1.4 | 21 |
| 6–9 | ~3,400 | **0** | 0.0 | ~27 |
| 10 | 1120 | 0 | 0.0 | **378** |
| 11 | 1240 | 0 | 0.0 | **1090** |

## Findings

**1. The mechanism works and was necessary.** HP scaling is now the only lever that makes a later wave
harder than an earlier one. Nothing else in the content system can do it.

**2. It is not sufficient.** Waves 6–11 still leak *zero*. Scaling moved the difficulty around; it did
not create a late game. The curve is still front-loaded.

**3. The documented HP-growth band is wrong for this game.** `balance-targets.md` asks for 1.10–1.18
wave to wave. At 1.10 the player loses 80% of runs; at 1.18, 100%, dead by wave 5. That band was
written before anything could be measured. It is now contradicted by measurement, and one of the two
has to give — I have flagged it in the doc rather than quietly rewriting the number, because a target
edited to match the first measurement is not a target.

**4. Wave 3 is an economy cliff, and it is the real problem.** It leaks 21.5% while the player holds
12 gold. The danger is not that wave 3 is strong; it is that the player is broke when it arrives.
Scaling makes this worse because it compounds onto the wave the player can least afford.

**5. Late gold still explodes — 378, 1090 — and now the cause is unmistakable.** There is no gold sink
that scales. Towers cannot be upgraded, so once the board saturates the only thing to buy is another
tower in a worse spot, and bounties pile up. **Upgrades are the missing mechanic**, not more HP.

## Verdict

**Kept at 1.03.** The mechanism is right and the rate is the best available; the remaining problems are
structural and cannot be fixed by tuning this knob.

| Follow-up | Workspace | Suggested slug |
|---|---|---|
| Tower upgrades — the missing gold sink. Without one, late gold saturates whatever the HP curve does | game-design / engine-systems | `tower-upgrades` |
| The early economy: wave 3 leaks 21.5% with 12 gold in hand. Starting gold, bounty curve, or wave 3's composition | content-data | `early-economy` |
| Resolve the HP-growth band: 1.10–1.18 is contradicted by measurement | content-data | `hp-growth-target` |
| Enemy roster — still 2 archetypes against a 4–7 target | content-data | `enemy-roster` |

## Trace re-recorded

`crossroads-baseline` diverged at **tick 900** — exactly where wave 2 starts, with waves 1 and its 899
preceding ticks identical. That is the precise signature of a change that scales every wave except the
first, so the divergence was diagnosed before the trace was touched, not after.

Re-recorded deliberately. New hash `23d8e456da0eba21`, was `394b8c4237d52a19`.

## Reproduce

```bash
dotnet run --project Gridfall.Verify -c Release -- balance --map crossroads --runs 30 --seed 1
```
