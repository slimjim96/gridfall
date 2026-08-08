# Tower Range Tiers — v1

**Slug:** `tower-range-tiers` · **Status:** done · **Verified at trace:** re-recorded

## What was wrong

The **cheap starter had the longest reach on the board and the tallest silhouette.**

| | cost | range | model height |
|---|---|---|---|
| `arrow-tower` (starter) | 50 | **3.0** | 1.45 — tallest |
| `cannon` | 90 | 2.5 | 0.62 — squat |

Reported by the human after the range ring shipped: *"I thought the range of their shot was too far
away"* — so they under-built, because one starter tower looked like it covered everything. The ring
did its job; it exposed a stat, not a rendering bug.

## The rule now

**Height means range.** Taller reaches further, costs more, one rule learnable in a game.

| | cost | range | model height | route cover |
|---|---|---|---|---|
| `arrow-tower` | 50 | **2.0** | **0.85** | crossroads 22% → **11%** |
| `cannon` | 90 | **3.5** | **1.55** | — |

Silhouettes stay distinct — thin vs broad, short vs tall — so the rule in
`placeholder-standard.md` still holds; only its vocabulary changed.

## What it cost, and the wave adjustment

Coverage scales with r², so 3.0 → 2.0 removes ~55% of one tower's reach:

| | runs lost | lives |
|---|---|---|
| before | 24.0% | 8.4 |
| after the range change, waves unchanged | **80.7%** | 0.8 |
| after `hpGrowth 1.10` from wave **6** | **20.0%** | 13.1 |

Back inside the 15–30% late target. **The rate stays inside the documented 1.10–1.18 band** — delaying
the ramp from wave 4 to wave 6 did the work, not a rate below band. Two knobs again, as the original
curve pass found: the rate could not do it alone.

## What did not change, and it is informative

**`gauntlet` is still 0.0% lost, sd 0.2.** Halving every tower's reach did not make it interesting.

That is the clearest evidence yet that its problem is **route freedom, not tuning**: a walled-in route
gives the player no decision to get wrong, so no amount of making towers weaker creates one. See
`route-variance-metric` and the gauntlet cliff report. Its wave table was deliberately left alone.

## Verification

`dotnet build` 0/0 · **200 tests** · determinism trace **re-recorded** (tower stats are simulation
inputs) · three gameplay baselines re-recorded (tower models changed height) · `Verify maps` cover
column updated.

## Known Not Verified

- **Whether the new ranges feel right in play.** The sim says 20.0%; whether a 2-cell starter is
  satisfying to place is a human call, and it is the reason this change was requested.
- `gauntlet` untouched and still degenerate.
- No third tower tier. "Taller ones with wider ranges" currently means exactly one step, arrow →
  cannon. A genuine tier ladder wants more of the roster — `tower-pool`.
