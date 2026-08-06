# Core Foundation — Build Notes

**Slug:** `core-foundation` · **Status:** review

Source lives at the repository root, not here — see the deltas table in the architecture note and
`docs/conventions.md` §Where the source lives.

## What Was Built

| File | Tick phase |
|---|---|
| `Gridfall.Core/Math/Fix32.cs`, `FixVec2.cs`, `FixMath.cs` | — |
| `Gridfall.Core/SimRandom.cs`, `FnvHash.cs`, `GridCell.cs`, `Commands.cs` | — |
| `Gridfall.Core/Content/Defs.cs`, `ContentLoader.cs` | — (pre-sim) |
| `Gridfall.Core/Path/PathSystem.cs` | 2 |
| `Gridfall.Core/SimState.cs` | 9 (the hash) |
| `Gridfall.Core/Events/SimEvent.cs` | 9 |
| `Gridfall.Core/Systems/CommandSystem.cs` | 1 |
| `Gridfall.Core/Systems/SpawnSystem.cs` | 3 |
| `Gridfall.Core/Systems/MovementSystem.cs` | 4 |
| `Gridfall.Core/Systems/TargetingSystem.cs` | 5 |
| `Gridfall.Core/Systems/ProjectileSystem.cs`, `DamageBuffer.cs` | 6 |
| `Gridfall.Core/Systems/DamageSystem.cs` | 7 |
| `Gridfall.Core/Systems/EconomySystem.cs` | 8 |
| `Gridfall.Core/Sim.cs` | the loop |
| `Gridfall.Tests/*` — 70 tests | — |
| `Gridfall.Verify/*` — replay, record, balance, maps, perf | — |
| `content-data/{maps,towers,enemies,waves}/*.json` | — |

## Decisions Made While Building

| Decision | Rejected alternative | Why | ADR? |
|---|---|---|---|
| `Fix32` has no `FromFloat`, only `FromFraction` | A convenience `FromFloat` for tests | It is the door every determinism bug walks through. Content parses decimals to exact rationals instead — `DecimalToRational`, never `GetDouble()`. | No — implements ADR-0002 |
| Exact bit-by-bit integer sqrt | Newton–Raphson with fixed iterations, as the guide specified | Exact at the same cost, and there is no iteration count to get wrong. Guide corrected. | No |
| `FixMath.Sin`/`Cos` not implemented | Table seeded from `double` at static init | That puts platform-dependent values in Core, which is the one thing Core exists to prevent. Nothing needs trig yet. Guide corrected. | No — revisit when something needs an angle |
| Damage buffered in phase 6, applied in phase 7 | Apply inline when a projectile lands | Two towers killing one creep on one tick must produce one death and one bounty. Tested. | No — follows guide 02 |
| Projectiles fizzle when their target dies | Re-target mid-flight | Re-targeting makes the outcome depend on removal order. | No |
| Creep ids in a dense `int[]` slot map, grown by doubling | `Dictionary<int,int>` | No hash-iteration hazard, and growth is amortised and rare. | No |
| Projectiles hashed in id order via a stack-allocated index sort | Hash in slot order | Slot order depends on creation/removal interleaving; the sort is over a handful of elements. | No |
| Stranded creeps stand still and emit `CreepStranded` | Throw | A throw in the tick loop takes the whole run with it. Standing still is visible and cannot cascade. | No |
| Traces are committed to git | Ignored as regenerable | `replay` has nothing to check without them, and a trace that exists on one machine cannot catch a platform divergence. `.gitignore` corrected. | No |

## Deviations From the Architecture Note

**One, and it is in the note's own deltas table:** the note listed five expected deltas from the engine
guide. All five held. Nothing else diverged.

`Sim.Finalize` had to become `Sim.FinalizeTick` — `Finalize` collides with `Object.Finalize` and C#
rejects it outright (CS0465). Guide chapter 02 corrected.

## Determinism

- State hash updated: **yes** — every field on `SimState`, with one coverage test per field
  (`HashCoverageTests`, 19 tests). Includes `_slotOfCreepId` indirectly via id-order iteration.
- Trace recorded: `Gridfall.Verify/traces/crossroads-baseline.json` — 3,000 ticks, 30 checkpoints.
- Source purity is a test, not a promise: `SourcePurityTests` greps Core for `float`, `double`,
  `Random`, `DateTime`, `Guid`, `Parallel`, `Godot`, dictionary iteration, and filesystem access on
  every `dotnet test`.

## Build Status

```
dotnet build   → 0 warnings, 0 errors  (Core is warnings-as-errors)
dotnet test    → 70 passed, 0 failed
```

## Notes for Verification

Three things I want a second pair of eyes on, rather than fixing quietly:

1. **The balance policy builds nothing.** `Verify balance` runs, but its "policy" places no towers, so
   its output describes an undefended board — 100% leak rate, every run lost. It prints that caveat
   itself. It is not a balance report yet and must not be quoted as one.

2. **Nothing consumes randomness.** `SimRandom` exists, is seeded, and is hashed, but no system draws
   from it. Every seed therefore produces an identical run, which is why the balance sim's 50 runs are
   byte-identical. That is correct behaviour today and will silently stop being obvious the moment
   something does draw — worth knowing now.

3. **The tie-break test's fixture guard.** `FlowField_MatchesFirstAssignmentWinsReference` compares
   against a reference implementation with a switch for the exact overwrite-on-equal defect, and a
   second test asserts the fixture actually distinguishes the two. That is the gap the worked example
   warned about, closed. I would like it checked that the reference is a fair one and not just a
   restatement of the implementation.
