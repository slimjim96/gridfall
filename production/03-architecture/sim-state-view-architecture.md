# SimStateView — Architecture

**Slug:** `sim-state-view` · **Status:** done

Closes the gap the view-layer slice left open and the engine guide had been promising since before any
code existed. Stages 01 and 02 skipped: there is no player-facing change and no new behaviour — this is
a boundary being enforced rather than a feature being added.

## The problem

`Sim.State` returned the mutable `SimState`. ADR-0001 says the view reads state and never writes it,
and engine guide 05 claimed that was a compile-time fact. It was not: the renderer could have written
any field, and only code review stood between it and a determinism bug that would surface as a trace
divergence days later.

## Systems Touched

| Thing | Change |
|---|---|
| `SimStateView` | **New.** Read-only struct façade over `SimState` |
| `Sim.State` | Now returns `SimStateView` |
| `Sim.MutableState` | **New**, `internal` |
| `Gridfall.Core/Properties/AssemblyInfo.cs` | **New.** `InternalsVisibleTo` for Tests and Verify — not the Godot project |
| `UnitRenderer`, `Hud`, `GameplayScene`, `SimDriver` | Array indexing → accessor calls |
| Tests, `Verify.Perf` | Two writes moved to `MutableState` |

No tick-loop code changed. No state was added, so the hash is untouched.

## Design

**Accessors are methods returning copies, not arrays.** `CreepHp(slot)`, not `CreepHp[slot]`. Handing
out an array would hand out a write path, which is the one thing this type exists to prevent — so the
awkwardness is the feature.

**A struct wrapping one reference.** Passing it copies a pointer; nothing allocates per frame, which
matters because the renderer takes it every frame.

**`internal` rather than a debug flag.** The test suite must mutate single fields to prove hash
coverage, and the perf harness must grant itself gold. Both are first-party. `InternalsVisibleTo` gives
exactly those two assemblies write access and nobody else — no runtime check, no flag to forget.

## Determinism Checklist

| Check | Result |
|---|---|
| No floats in Core | Unchanged — `SourcePurityTests` still green |
| State hash covers new state | No state added |
| Iteration order | Unchanged — the view delegates to the same `*SlotByOrder` |
| Behaviour | Unchanged — trace replays 30/30, and the render is byte-identical to the baseline |

## Verify Plan

1. Build 0/0 across all five projects.
2. Full suite green, including new reflection tests over the view's shape.
3. **A write attempt from the Godot project must fail to compile** — the actual claim.
4. Trace replay unchanged.
5. Captured frame byte-identical to `presentation/docs/board-baseline.png`.
