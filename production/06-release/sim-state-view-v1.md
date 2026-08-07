# SimStateView — v1

**Slug:** `sim-state-view` · **Status:** done · **Verified at trace:** `987dc81d2e55a6cd` (at the time)

## What Shipped

The Core/View boundary as a compile-time fact rather than a convention.

`Sim.State` returns a read-only `SimStateView` struct. No setter, and accessors are **methods returning
copies, never arrays** — handing out an array hands out a write path, so the slight awkwardness at every
call site is the feature.

First-party tooling that genuinely needs to write uses `Sim.MutableState`, which is `internal` and
visible via `InternalsVisibleTo` to `Gridfall.Tests` and `Gridfall.Verify` only. The Godot project is
deliberately excluded, so the renderer has no write path at all.

## Player-Facing Change

None. Behaviour identical: trace 30/30, sim hash unchanged, captured frame byte-identical.

## Verified by Compile Failure

The claim is a guarantee, so the verification is a compilation error. A probe added under `godot/View/`
and then removed:

```
sim.State.Gold = 999999;  →  CS0200: cannot be assigned to -- it is read only
_ = sim.MutableState;     →  CS1061: 'Sim' does not contain a definition for 'MutableState'
```

Seven tests hold the shape by reflection, each with a guard so it cannot pass vacuously.

## Follow-Ups Not Done

| Item | Workspace | Slug |
|---|---|---|
| ~~`PathSystem` mutators are still public~~ | engine-systems | **closed by `route-overlay`** |

## Known Not Verified

Nothing outstanding for this slice. The first refactor to touch every read path in the renderer, and
the visual baseline proved it inert — which is what the baseline was built for.
