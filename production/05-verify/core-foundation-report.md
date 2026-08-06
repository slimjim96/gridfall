# Core Foundation — Verification

**Slug:** `core-foundation` · **Status:** review · **Verdict:** PASS

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` | PASS | 0 warnings, 0 errors across three projects; Core is warnings-as-errors |
| `dotnet test` | PASS | 70 passed, 0 failed, 69 ms |
| Determinism trace | PASS | `Verify replay` — `crossroads-baseline`, 3,000 ticks, 30/30 checkpoints match |
| Perf ≤ 8 ms/tick | PASS (partial) | `Verify perf`: 0.0034 ms avg, 0.0373 ms worst, 62 towers. **Not the documented worst case** — see below |

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | Sim advances by `Tick()` and never touches a clock | PASS | `SourcePurityTests.Core_ContainsNoClock` greps Core for `DateTime`/`Stopwatch`/`TickCount` |
| 2 | Identical inputs produce identical per-tick hashes | PASS | `TwoIdenticalRuns_ProduceIdenticalHashes` (600 ticks) + the replay gate. `TheRunActuallyDoesSomething` guards against passing vacuously — 50+ distinct hashes required |
| 3 | `Restore(Snapshot())` + N ticks == N ticks | PASS | `SnapshotRoundTrip_MatchesRunningStraightThrough`, 200 ticks compared hash for hash after a deliberate divergence |
| 4 | Sealing build refused, grid unchanged, gold unspent, event emitted | PASS | `Build_ThatWouldSealTheLane_IsRefusedAndLeavesTheGridUnchanged` — cost array asserted byte-identical |
| 5 | Equal-cost routes resolve the same way, every run | PASS | `FlowField_MatchesFirstAssignmentWinsReference` against a reference implementation carrying the exact defect as a switch, plus `ArenaFixture_ActuallyDistinguishesTheTieBreakDefect` proving the fixture is not vacuous, plus `CreepRoute_IsIdenticalAcrossRuns` over 50 seeds |
| 6 | A creep finishes crossing a cell before turning | PASS | `ACreep_FinishesCrossingACellBeforeTurning` — maze changed mid-crossing, heading held |
| 7 | Two towers killing one creep → one death, one bounty | PASS | `TwoTowersKillingTheSameCreep_ProduceOneDeathAndOneBounty` — 4 creeps, 4 deaths, 4 bounties, not 8 |
| 8 | Mutating any hashed field changes the hash | PASS | `HashCoverageTests` — 19 tests, one per field, plus `Hash_DoesNotDependOnSlotOrder` |
| 9 | Core contains no `float`/`double`/`Random`/`DateTime` | PASS | `SourcePurityTests` — 10 greps run on every `dotnet test`; `TheAuditActuallyScansSomething` guards the path finder |
| 10 | A wave runs start to finish | PASS | `AWave_RunsStartToFinish` — 4 spawned, 4 resolved exactly once, `WaveCleared` emitted |

## Structural Invariants

| Invariant | Result |
|---|---|
| `Gridfall.Core` references no `GodotSharp` | PASS — `CoreProject_ReferencesNoGodotPackage`, comments stripped before the check |
| No `float`/`double`/`Random`/`DateTime` in Core | PASS — only `Fix32.ToFloat`, the documented view-layer boundary conversion |
| Never-fully-blockable holds on every shipped map | PASS — enforced at load; `UnreachableGoal_FailsAtLoad` proves the loader refuses |
| State hash covers all state the slice added | PASS — 19 per-field tests |

## Defects Found and Fixed During Verification

**One real defect**, found by the snapshot test rather than by reading the code:

`Sim.Restore` rebuilt the flow field with `ForceRebuild()`, which increments `PathSystem._version`.
The version is part of the state hash, so a restored sim hashed differently from the sim it was
restored from — divergence at the very first hash after the round trip. Fixed by adding
`PathSystem.RestoreFrom(cost, version)`, which restores the counter alongside the grid.

This is precisely the failure mode engine guide 08 §D describes: state that exists and is hashed, but is
not part of the round trip. It was invisible to every other test.

**Four test defects**, all mine, all in the tests rather than the engine:

1. `Sqrt_NeverOvershoots` asserted exactness using `Fix32` multiply, which truncates — so the
   upper-bound check was weaker than it looked and failed on a value it should have accepted. Rewritten
   against exact `long` arithmetic on raw values, and the lesson added to engine guide 03.
2. `SubUnitAccumulation` expected 1/100 accumulated 100 times to reach 1. It does not: `FromFraction(1,100)`
   truncates to 655/65536, and 100 of those is 0.99945. Split into two tests — one using an exactly
   representable rate, one documenting the inexact case as designed behaviour.
3. `CoreProject_ReferencesNoGodotPackage` matched the word "Godot" inside the csproj comment explaining
   why the project is not a Godot project. Now strips XML comments first.
4. `ATower_KillsCreepsAndEarnsBounty` captured its gold baseline one tick too late. The cause is worth
   keeping: `StartWave` is applied in phase 1 and `SpawnSystem` runs in phase 3, so a zero-delay wave
   spawns on the *same* tick, and a long-range tower can kill on tick 0. Pinned down as its own test,
   `AWave_SpawnsOnTheTickItStarts`.

## Not Fully Verified

| # | What, and why |
|---|---|
| Perf | The 8 ms budget in tech-standards is written for 64×64 with 300 creeps and 60 towers. What was measured is 20×9 with 62 towers and far fewer creeps — 215× inside budget, but **not the documented case**. There is no 64×64 map yet to run it on. The harness prints this caveat itself. |
| Balance | `Verify balance` runs, but its policy places no towers, so its output describes an undefended board (100% leak, all runs lost) and is not a balance report. It says so on every run. A competent-play policy is required before any number from it is quoted. |
| Randomness | `SimRandom` is seeded, hashed, and correct, but **no system draws from it**, so every seed produces an identical run. Correct today; a trap the first time something does draw and the balance sim's "50 runs" stop being 50 different runs. |
| Cross-platform determinism | Every determinism claim was verified on **one machine**, one runtime (net10.0.10, ubuntu-x64). The whole point of `Fix32` is that this generalises — but generalising it is a claim, not yet an observation. |

## Branch Resolution

None — verdict is PASS. Advancing to `06-release`.
