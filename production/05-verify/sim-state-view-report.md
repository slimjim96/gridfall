# SimStateView — Verification

**Slug:** `sim-state-view` · **Status:** review · **Verdict:** PASS

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` (5 projects) | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **77 passed**, 0 failed (was 70; +7) |
| Determinism trace | PASS | `Verify replay` — 30/30 checkpoints |
| Render unchanged | PASS | Capture byte-identical to `presentation/docs/board-baseline.png` (`99fdcb4b…`) |
| Sim unchanged with renderer attached | PASS | `tick=90 hash=987dc81d2e55a6cd` — same value as before the refactor |

## The claim, tested directly

The point of this slice is a compile-time guarantee, so the verification is a compile failure. A probe
file was added under `godot/View/`, built, and removed:

```csharp
sim.State.Gold = 999999;   // error CS0200: 'SimStateView.Gold' cannot be assigned to -- it is read only
_ = sim.MutableState;      // error CS1061: 'Sim' does not contain a definition for 'MutableState'
```

Both write paths rejected. That is the guarantee, demonstrated rather than asserted.

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | The renderer cannot write simulation state | PASS | CS0200 + CS1061 above |
| 2 | The view exposes no settable member | PASS | `View_ExposesNoWritableMember` — reflection over properties and fields |
| 3 | The view hands out no array or by-ref | PASS | `View_HandsOutNoArrayOrReference` — a returned array would be a write path |
| 4 | `Sim.State` is the read-only type | PASS | `Sim_State_IsTheReadOnlyView` |
| 5 | `MutableState` is not public | PASS | `Sim_MutableState_IsNotPublic` |
| 6 | The view reports the same values as the state | PASS | `View_ReadsTheSameValuesAsTheState` — field by field over live entities |
| 7 | No Godot source references `MutableState` | PASS | `TheGodotProject_NeverTouchesMutableState`, guarded by a scan-is-working test |
| 8 | Behaviour unchanged | PASS | Trace 30/30, hash identical, frame byte-identical |

## What the baseline earned

This is the first slice where `board-baseline.png` did its job. A refactor touching every read path in
the renderer produced a **byte-identical frame** — which is much stronger evidence than "it still looks
right to me", and it took one `md5sum`.

## Not Verified

| What | Why |
|---|---|
| Input, still | Nothing in this slice touched it, and nobody has clicked yet. Carried forward from the view-layer slice. |
| `PreviewRoute` | Engine guide 05 described it; it does not exist. `PathSystem.WouldRemainConnected` is the block check, but it is not exposed to the view. The guide now says so instead of implying otherwise. |
| Whether `internal` is the right escape hatch long-term | It is right today: two first-party assemblies, no runtime cost. If a third consumer appears, revisit rather than widen by reflex. |

## Branch Resolution

None — verdict is PASS.
