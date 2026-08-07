# Early Economy 2 — Verification

**Slug:** `early-economy-2` · **Status:** review · **Verdict:** PASS

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **170 passed**, 0 failed (was 165; +5) |
| Determinism trace | PASS | Re-recorded after diagnosis — see below |
| Balance targets | PASS | Both runs-lost targets, for the first time |
| Visual capture | n/a | No visual claim; no renderer change |

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | Wave 3 leaks under the 15% per-wave ceiling | PASS | 14.1% → **4.3%** |
| 2 | Runs lost in waves 1–10 within 0–5% | PASS | 25.5% → **3.5%** |
| 3 | Runs lost in waves 11+ within 15–30% | PASS | 0.5% → **21.5%** |
| 4 | Overall leak rate ≤ 4% | PASS | 1.6% |
| 5 | No single wave over the 15% ceiling | PASS | Worst is wave 12 at 6.7% |
| 6 | Lost runs die late rather than early | PASS | Mean death wave 4.3 → **10.9** |
| 7 | `hpGrowthFrom` absent reproduces the old curve exactly | PASS | `WithoutGrowthFrom_TheCurveIsExactlyWhatItWasBefore` — raw `Fix32` equality, every wave |
| 8 | Waves at or before the ramp start are flat | PASS | `WavesAtOrBeforeGrowthFrom_AreFlat` |
| 9 | The ramp runs at the declared rate afterwards | PASS | `AfterGrowthFrom_TheRampRunsAtTheDeclaredRate` |
| 10 | `hpGrowthFrom < 1` fails to load | PASS | `AGrowthFromBelowOne_FailsToLoad` |
| 11 | Explicit `hpScale` still overrides | PASS | `AnExplicitScale_StillOverridesGrowthFrom` |
| 12 | `gauntlet` is unchanged | PASS | Declares no `hpGrowthFrom`; criterion 7 makes the default provably identical |

## The finding was an instrumentation gap, not a tuning error

The brief was "wave 3 leaks 14.1%, worst wave for six passes". Wave 3 was a symptom.

`balance-targets.md` has carried **two** runs-lost targets since it was written — 0–5% for waves 1–10
and 15–30% for waves 11–20. The balance sim printed **one** number, labelled it `15-30% late`, and
checked it against `lostRate is >= 0 and <= 60`, a band that appears nowhere in the doc.

Split, the shipped 26.0% was **25.5% early and 0.5% late** — the exact inverse of intent, and lost runs
died at wave 4.3 of 12. Six passes read that number as "ok".

This is the fourth consecutive slice where the failure was invisible to the metric rather than absent
from the game. The pattern is now explicit in `balance-targets.md`.

## Why one knob could never fix it

`hpGrowth` applies from wave 1, so wave 3 carries `growth²` and wave 12 carries `growth¹¹`. Raising the
rate to threaten wave 12 also inflates waves 2–4, which are where the player is broke — the binding
constraint on the whole curve. Every previous pass faced a forced choice between a lethal opening and a
trivial ending, and took the ending the targets could not see.

`content-data/CONTEXT.md`'s **one knob per pass** rule is why this survived six attempts. It is right
about attribution and wrong about arity: the curve needed both ends moved in opposite directions. The
resolution is to sweep a **grid**, which keeps attribution while allowing two knobs — recorded in the
balance report rather than silently violated.

## Two fixes measured; the less obvious one shipped

| | gold 500, growth 1.10 | **flat to 4, then 1.14** |
|---|---|---|
| leak | 1.5% | 1.6% |
| lost, waves 1–10 | 0.0% | 3.5% |
| lost, waves 11+ | 27.5% | 21.5% |
| died at wave | 12.0 (range 11–12) | 10.9 (range **3–12**) |
| starting gold | 500 | **300, unchanged** |

Both pass. Shipped the second: with 500 gold **nobody can lose before wave 11**, which is a gate rather
than a curve, and ten towers at wave 1 removes the opening as a decision. Deaths spanning waves 3–12
keep a tail.

Wave 3 lands at 4.3% in **every** shaped row regardless of rate — so the six-pass problem was never the
player's wallet. Wave 3 carried `1.08²` of scaling before the player owned a board.

## Trace re-recorded, after diagnosis

Diverged at **tick 900**, exactly the tick the trace script starts wave 2, whose scale goes 1.08 → 1.00.
Wave 1 is unaffected because its scale is 1.0 under both curves, and checkpoints 0–800 passed.

The divergence was predicted from the content change before the replay was run, which is the standard
this project holds re-records to. `f07f32a860421e94` → `240e010e52e5d640`.

## Caveat carried into the release

The shipped configuration is **not robust to casual edits**. Growth 1.135 → 1.14 → 1.145 moves runs-lost
11% → 29% → 47%; a 0.005 change swings it ~18 points, because `growth⁸` amplifies rate changes and the
wave-12 margin is thin.

It is not `gauntlet`'s cliff (0.005 taking it 0% → 90%) — crossroads degrades across a range rather than
flipping — but `difficulty-slope` is now the more pressing follow-up, with two maps to measure.

## Not Verified

| What | Why |
|---|---|
| Whether a flat opening *feels* like a tutorial or like filler | Needs a human. Four waves at scale 1.0 is a design claim about pacing, not a measurable one |
| `gauntlet` under a shaped curve | It declares no `hpGrowthFrom` and is unchanged. The split metric shows it is fully trivial (0% leak, 0% lost, 20/20 lives) and it has a known cliff at 1.125 (`gauntlet-cliff`) |
| Whether the 1–10 / 11–20 band split suits a 12-wave game | Inherited from a 20-wave design; it buckets 10 of 12 waves as "early" (`wave-band-targets`) |

## Branch Resolution

None.
