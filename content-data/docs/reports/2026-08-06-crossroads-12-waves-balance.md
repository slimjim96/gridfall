# Balance Pass — crossroads extended to 12 waves

**Date:** 2026-08-06 · **Map:** crossroads · **Runs:** 30 · **Seed:** 1
**Policy:** competent-beginner
**Before:** [3-wave baseline](2026-08-06-crossroads-baseline-balance.md)

## Intent

Only three waves existed, so every target about waves 10–20 was unmeasurable. Added waves 4–12 to make
them measurable. **No knob was tuned** — the change is wave composition only, so the result stays
attributable.

Ramping levers available: count, spacing, and the brute ratio. That is the whole list, and it turns out
to matter — see the findings.

## Results

| Metric | Value | Target | |
|---|---|---|---|
| Leak rate, overall | 0.5% | ≤ 4.0% | ok |
| Runs lost | 3.3% | 15–30% for waves 11–20 | **MISS — far too easy** |
| Lives left, avg | 17.1 / 20 | — | |
| Towers standing | ~40 | — | |

### Per wave

| Wave | Spawned | Leaked | Leak % | Ticks | Gold at start |
|---|---|---|---|---|---|
| 1 | 240 | 0 | 0.0 | 297 | 150 |
| 2 | 420 | 4 | 1.0 | 551 | 14 |
| 3 | 600 | 42 | **7.0** | 858 | 5 |
| 4 | 750 | 24 | 3.2 | 782 | 14 |
| 5 | 899 | 0 | 0.0 | 804 | 22 |
| 6 | 1015 | 0 | 0.0 | 706 | 13 |
| 7 | 1160 | 0 | 0.0 | 717 | 30 |
| 8 | 1305 | 0 | 0.0 | 745 | 15 |
| 9 | 1450 | 0 | 0.0 | 775 | 32 |
| 10 | 1624 | 0 | 0.0 | 838 | **414** |
| 11 | 1798 | 0 | 0.0 | 907 | **1126** |
| 12 | 2030 | 0 | 0.0 | 1011 | **1934** |

## Findings

**1. Difficulty peaks at wave 3 and then collapses.** Waves 5–12 leak *nothing* — zero creeps out of
roughly 11,000 spawned. The hardest moment in the game is wave 3, when the player cannot yet afford
towers. After that it is a formality.

This is backwards from the design intent, which wants 0–5% of runs lost in waves 1–10 and 15–30% in
waves 11–20. What actually happens is the opposite: the only danger is early.

**2. The cause is structural, not a tuning miss.** Enemy HP is **fixed per definition** and there is no
per-wave multiplier. Sending more creeps of the same two types cannot outpace tower accumulation —
every extra creep is also extra bounty, which becomes another tower. Wave 12 throws 70 creeps at the
player, but each one is exactly as fragile as wave 2's.

More waves of the same enemies will never produce a late game. This needs either a per-wave HP scalar
(an engine change) or genuinely tougher archetypes.

**3. The economy breaks once the board saturates.** Gold sits near zero through wave 9 — healthy — then
runs to 414, 1126, 1934. Once towers cover everything worth covering, bounties have nowhere to go. That
is the "money and no decision" failure the targets are meant to catch, and it lands right where the
game stops being able to threaten the player.

**4. Wave 1 is still short.** 297 ticks is 9.9 s against a 20–45 s band. Unchanged from the baseline.

## A correction to the first measurement

The first run of this pass reported an inverted-but-shallow curve and gold reaching 3,227 by wave 12,
with towers stalling at ~21. **That was the policy, not the game.**

The policy jittered among its top 3 placements by coverage and gave up if all three were refused. On a
filling board its three favourite cells are all chokepoints the never-fully-blockable rule rejects — so
it stopped building at 21 towers while sitting on thousands of gold, and I nearly wrote that up as an
economy finding.

A real player takes a worse spot. The policy now falls back to scanning the ranked candidates until one
passes the seal check, and it reaches ~40 towers. Every number above is from the corrected policy.

The lesson generalises: **a balance report measures the policy as much as the game.** When a result
looks like a dramatic game finding, check whether the player is the thing that is broken.

## Verdict

**Kept.** The waves stay — they did their job, which was to make the late game measurable, and what they
measured is that there is no late game. Reverting would only restore the blind spot.

No knob tuned; nothing here is a tuning problem.

| Follow-up | Workspace | Suggested slug |
|---|---|---|
| Per-wave HP scaling, or an enemy roster with real toughness spread — without one of these the late game cannot exist | engine-systems / content-data | `wave-scaling` |
| More archetypes: 2 of a target 4–7, and both die to the same tower | content-data | `enemy-roster` |
| Lengthen wave 1 to reach the 20–45 s band | content-data | `wave-1-length` |
| Brute speed is 0.03 against a 0.036–0.108 spread target — already out of band before this pass | content-data | `brute-speed` |

## Reproduce

```bash
dotnet run --project Gridfall.Verify -c Release -- balance --map crossroads --runs 30 --seed 1
```
