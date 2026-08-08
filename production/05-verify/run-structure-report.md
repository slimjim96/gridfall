# Run Structure — Verification

**Slug:** `run-structure` · **Status:** review · **Verdict:** PASS on the gates; **run-end screens not
seen** (see below)

Option **A** from the requirements: a run is one board, it can be won, it can be lost, and its length
is a content field.

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` (5 projects) | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **191 passed**, 0 failed (was 187; +4) |
| Determinism trace | PASS | `Verify replay` 30/30, **not re-recorded** — see below |
| Balance sim | PASS | Runs at every `runWaves` value tested |

**The trace did not need re-recording, by design.** `RunComplete` rides the same transition as the
final `WaveCleared` rather than storing a flag, so no hashed state was added. A stored `_runEnded`
in `SimState` would have changed the hash of every existing trace for a purely presentational fact.

## What was actually broken

Three things, all invisible from inside the game:

1. **`GameOver` fired into nothing.** `EconomySystem` emits it at zero lives and says the caller
   decides whether the run ends — and `grep` across `godot/`, `Verify/` and `Tests/` found no caller.
   The game kept running at zero lives indefinitely.
2. **No victory existed.** Clearing the last wave produced no signal at all.
3. **No progression.** `MapId` is a `const`.

This slice fixes 1 and 2. **3 is deliberately not fixed** — option A makes boards self-contained, and
a board *selector* is the follow-up (`board-select`), not part of being able to finish one.

## The finding that changes the answer to the question asked

The request was "12 waves might be too much, can this be a setting". It can, and it is — but the
measurement says the setting is not what it looks like.

| `runWaves` | Runs lost | Lives left |
|---|---|---|
| 8 | **0.0%** | 18.7, sd 2.8 |
| 10 | **0.0%** | 18.5, sd 2.9 |
| 12 | **23.3%** | 8.7, sd 7.0, range 0–20 |

**All of crossroads' difficulty is in waves 11–12.** The HP curve is authored per wave index, so
truncating removes the top of the curve — a 10-wave crossroads is not five-sixths of the game, it is
a game with no losing condition, which is precisely the `gauntlet` failure already on record.

`runWaves` therefore ships as a **testing and gentle-board** knob with the numbers in
`balance-targets.md`, and shortening a *real* run is a curve re-authoring job (`hpGrowth`,
`hpGrowthFrom`) rather than a truncation. Follow-up `short-run-curve`.

That measurement is the deliverable here as much as the code is.

## Criteria

| # | Criterion | Result |
|---|---|---|
| 1 | Zero lives ends the run | PASS by construction — `EndRun(won: false)` |
| 2 | Clearing the last wave alive wins | PASS — `ClearingTheLastWaveAliveEmitsRunComplete` |
| 3 | The win fires exactly once | PASS — `RunCompleteFiresExactlyOnce` |
| 4 | `runWaves` truncates | PASS — `RunWaves_TruncatesTheTable` |
| 5 | A bad `runWaves` is refused, not clamped | PASS — `RunWaves_OutsideTheAuthoredTableIsRefused` |
| 6 | No new hashed state | PASS — trace unchanged |
| 7 | The end screen is legible | **NOT SEEN** — no capture |

## Not Verified

| What | Why |
|---|---|
| **The two end screens on screen** | No shot seed reaches wave 12 or zero lives; the existing seeds stop at wave 7. Adding one means simulating ~4,000 ticks in shot mode. Worth doing, not done. |
| Whether stopping the sim outright is the right feel | It freezes the final board deliberately, so "why did I lose" is answerable by looking — but a human should judge it. |
| Anything about board-to-board flow | Out of scope by decision. |

## Branch Resolution

Held at `review` for the same reason as `camera-pan-zoom`: a visual claim with no capture behind it
is not verified. The gates and the balance measurement stand on their own.
