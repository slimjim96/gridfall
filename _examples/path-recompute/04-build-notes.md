# Path Recompute — Build Notes

**Slug:** `path-recompute` · **Status:** review
*Workflow: WF-04. Would live at `production/04-build/path-recompute/build-notes.md`.*

## What Was Built

| File | New / Changed | Tick phase |
|---|---|---|
| `Gridfall.Core/Path/FlowField.cs` | New | 2 |
| `Gridfall.Core/Path/PathSystem.cs` | New | 2 |
| `Gridfall.Core/Systems/CommandSystem.cs` | Changed — block check before grid mutation | 1 |
| `Gridfall.Core/Systems/MovementSystem.cs` | Changed — reads the flow field, drops per-creep paths | 4 |
| `Gridfall.Core/SimState.cs` | Changed — `_version` added to the hash | 9 |
| `Gridfall.Tests/Determinism/PathRecomputeTests.cs` | New | — |

## Decisions Made While Building

| Decision | Rejected alternative | Why | ADR? |
|---|---|---|---|
| One dirty flag for the whole grid, not per-lane | Per-lane dirty flags | A build affects at most one lane's cost array, but the block check has to run all lanes anyway. Per-lane flags would save a BFS on multi-lane maps only when a build is legal — measured at 0.3 ms on the 4-lane map. Not worth the state. | No — reversible |
| `PreviewRoute` reuses the block check's scratch buffer | A second buffer for the view | They can never run in the same tick: preview is a view-side query between ticks, the check is phase 1. Sharing keeps them provably identical. | No — reversible |
| Ring buffer sized to `cells`, not `cells + 1` | The usual +1 sentinel | BFS visits each cell at most once, so `cells` is a hard bound. Asserted in a test rather than assumed. | No |
| Unreachable is `flow = 15`, not a separate array | A parallel `bool[] reachable` | Fits in the nibble already spent, and it makes "unreachable" impossible to forget to check. | No |

## Deviations From the Architecture Note

**One.** The note specified `ushort _version` in `PathSystem`. It is implemented on `SimState` instead,
because the hash is computed there and reaching into `PathSystem` from the hash would have put a
system reference in `SimState`. Same semantics, same hash coverage, cleaner boundary. The architect was
told; the note was not amended, because it is a placement detail rather than a structural change.

## Determinism

- State hash updated: **yes** — `_version` is folded in at phase 9.
- Trace recorded: `Gridfall.Verify/traces/path-recompute-baseline.trace`
- One comment left in `FlowField.Build` explaining why the neighbor loop is a hardcoded array of four
  offsets rather than an enum iteration: enum ordering is stable in C# but the intent is load-bearing
  here, and a future refactor should have to read the comment before changing it.

## Build Status

```
dotnet build   → 0 warnings, 0 errors
dotnet test    → 47 passed, 0 failed
```

## Notes for Verification

The tie-break test on the symmetric map passes locally, but it was written against a map with two
routes of equal length that are also **mirror images**. A map where the equal-cost routes are
*asymmetric* would be a stronger test. Flagging it rather than fixing it — the verification engineer
should decide whether that gap matters.
