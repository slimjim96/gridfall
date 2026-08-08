# Wave Pacing — Verification

**Slug:** `wave-pacing` · **Status:** done · **Verdict:** PASS

Four content knobs that turn the gap between waves into a resource: `prepTicks`, `waveClearGold`,
`midWaveBuildPercent`, `earlyCallGoldPerSecond`. All default to the original behaviour.

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | 196 passed, 0 failed |
| Determinism trace | PASS | Re-recorded — `PrepTicksRemaining` is genuine sim state |
| Balance in target band | PASS | 24.0% runs lost, inside the 15–30% late target |
| Shot seeds intact | PASS | `sappers` and `repair` both 28 towers / 20 lives |

## The measurement, 150 runs, `crossroads`

| clear | prep | mid % | Runs lost | Lives |
|---|---|---|---|---|
| — | — | 100 | 27.3% | 7.8 |
| 25 | 0 / 30 / 300 | 100 | **4.0% — identical at all three** | 12.1 |
| 80 | 0 / 300 | 100 | **0.7% — identical at both** | 18.4 |
| **25** | **300** | **115** | **24.0%** | **8.4** |
| 25 | 300 | 125 | 40.0% | 6.9 |
| 40 | 300 | 125 | 20.0% | 8.8 |
| — | 300 | 150 | 86.7% | 2.5 |

**Shipped: clear 25 / prep 300 / mid 115.** Inside the target band and within noise of the 27.3%
baseline — the same difficulty, with income now arriving in the gap and a cost for reacting late.

## Three findings the sim produced that the design did not predict

**1. A prep window alone does nothing — at any value, including 0.4 seconds.** Income was bounty-only,
so nothing was earned between waves and a spent-down player had no use for the time. Every prep row
above is byte-identical to its prep-less twin. `waveClearGold` was added specifically to fix this and
is the reason the other knobs work at all.

**2. `prepTicks` is unmeasurable by this harness even so.** `PlayPolicy` spends down and *then* calls
the wave, so it already has unlimited prep and no timer ever binds. Prep constrains how fast a *human*
decides; the policy decides instantly. **300 is a placeholder to be tuned by playing** — recorded as
such rather than presented as a result.

**3. The premium is a cliff.** 100 → 27%, 115 → 24%, 125 → 40%, 150 → 87%. Ten points of price is
worth sixteen points of loss rate between 115 and 125. Anything above 125 is a different game.

## The regression this caused, and what it revealed

Enabling `midWaveBuildPercent 115` **broke both long shot seeds**: `sappers` and `repair` finished at
5 towers and 0 lives instead of 28 and 20. Isolated to the premium — at 100 both recovered
byte-identically, so it was not the timer and not the income.

Cause: both seeds called `StartWave` the tick the board went inactive and built *afterwards*, so every
tower was bought mid-wave. Harmless while building cost the same either way. The moment a premium
existed, the seeds paid it on all 28 towers.

> **A verification seed must not model the one playstyle the economy is designed to discourage.**

Both now build between waves and call the wave only once they cannot spend — the order `PlayPolicy`
already used. That is also strictly a better model of a real player.

## Two measurement artifacts, recorded

- The first sweep read **81.3%** because the wave-start patch went into `Perf`, not `Balance`. The
  balance policy lives in `PlayPolicy`, not the run loop.
- `PrepTicksRemaining` entering the hash diverged the trace at tick 0. The line also re-hashed
  `WaveActive`, which `SimState.cs:224` already did; the redundancy was removed before re-recording.

## Not Verified

| What | Why |
|---|---|
| **`prepTicks 300` as a feel** | Unmeasurable here by construction. Needs hands. |
| `earlyCallGoldPerSecond` | Left at 0. Worth ~27 points of loss rate as pure income while no timer binds — it cannot be tuned until `prepTicks` is. |
| Whether a premium reads as fair in play | "Towers cost more right now" needs to be visible in the HUD; it currently is not. Follow-up `premium-hud-cue`. |
