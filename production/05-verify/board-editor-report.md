# Board Editor v1 — Verification

**Slug:** `board-editor` · **Status:** review · **Verdict:** PASS with one requirement unmet

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` (5 projects) | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **94 passed**, 0 failed (was 86; +8) |
| Determinism trace | PASS | 30/30 — after a real regression, below |
| Game render unchanged | PASS | `c50a84c1…`, byte-identical to the baseline |
| Editor runs and renders | PASS | `presentation/docs/editor-baseline.png` |

## The regression the harness caught

Worth leading with, because it is the harness earning its cost.

I changed the `PathSystem` constructor to build its field immediately, so the editor could get a usable
field without a public mutator. But `Sim`'s constructor still called `ForceRebuild()` — so the field was
built twice and `Version` was **2 at tick 0 instead of 1**. `Version` is hashed. Every hash in every
recorded trace shifted: `987dc81d2e55a6cd` → `b3dd9a86c29842f0`, and the trace failed immediately.

Two things make this a good catch:

- **Nothing else noticed.** All 94 tests passed. The captured frame was byte-identical, because the
  visible state at tick 90 was genuinely unchanged — only a counter differed. The visual baseline and
  the test suite both said fine; the trace diff did not.
- It is exactly the "behaviour changed" case in engine guide 08 §C, and the right response was to find
  the cause, not to re-record the trace. Removing the redundant rebuild restored the original hash.

## Criteria (from the spec's "Done when")

| # | Criterion | Result |
|---|---|---|
| 1 | A map can be created, painted, saved, and loaded with no hand-editing | PASS — `RoundTrip_ThroughJson_PreservesTheMap` proves the editor writes what the loader reads |
| 2 | `F5` plays the unsaved map, `Esc` returns to it | PASS by construction — `GameplayScene.PlaytestDraft` hands the draft over; Esc routes back. **Not exercised by hand** |
| 3 | Save refuses an invalid map with the validator's own message | **PASS — human sign-off 2026-08-06.** Confirmed by hand: painted a map into an invalid state and the save was refused. This was the one path a scripted capture could not reach |
| 4 | Editor and game rendering share a code path | PASS — the editor instantiates `WorldRenderer` and `RouteOverlay`, the same classes the game uses |
| 5 | Warnings never block a save; only errors do | PASS — `Validator_WarnsButDoesNotError_OnTargetMisses` |
| 6 | The maze estimate is labelled a lower bound wherever it appears | PASS — visible in the capture: "1.2x (greedy lower bound, target <= 3x)" |
| 7 | `MapTargets` is the only place the thresholds live in code | PASS — `MapValidator` reads them; nothing else defines a threshold |
| 8 | Stroke-end validation under 1 ms | PASS by construction — one BFS per stroke on ≤4,096 cells. **Not timed** |
| 9 | **`Dev/` absent from a release export** | **NOT MET — see below** |
| 10 | Every keybind works and is listed on an in-editor help overlay | PARTIAL — all bound, `F1` overlay implemented and rendering. **Not exercised by hand** |

## Requirement not met

**`godot/export_presets.cfg` does not exist**, so there is no release export to check `Dev/` against —
and the spec says this must be *verified, not assumed*. Right now the editor cannot ship by accident
only because nothing can be exported at all. That is not the same thing, and I am not going to record
it as a pass.

Closing it means creating an export preset with a `Dev/` exclusion filter and confirming the scene is
absent from the output. Follow-up slug `release-export`.

## The validator refactor

The spec's central rule is that the editor implements no validation of its own. Making that true meant
extracting the loader's checks into `MapValidator`, which returns findings instead of throwing, and
having `ContentLoader.LoadMap` call it and throw on the first error.

So there is now literally one function deciding what a legal map is, called by both. The editor decides
only how to *draw* the answer.

### A bug that took the whole suite down

The extraction broke 53 of 86 tests at once, with the message `fixture:  (0,0)`. Three compounding
causes, all mine:

1. `MapSeverity.Error` was the **zero value**, so `default(MapFinding)` claimed to be an error.
2. `MapFinding` is a **struct**, so LINQ's `FirstOrDefault` returns that default rather than `null` —
   and my `is { } fatal` pattern matched it happily.
3. The finding's cell defaulted to `(0,0)`, which I had special-cased as "no cell" — but **(0,0) is a
   legal cell**; spawns sit on the west edge.

Fixed by numbering the enum from 1 (with a comment saying why), taking a nullable cell, and dropping
`FirstOrDefault` for an explicit loop.

## Not Verified

| What | Why |
|---|---|
| Anything requiring hands | **Partly closed.** Human sign-off 2026-08-06 covers painting, live validation, and save refusal — the whole error path end to end. Still unexercised: undo/redo, `F5` playtest and `Esc` back, `F6` maze estimate, the `F1` overlay. |
| The release export exclusion | No export preset exists. Criterion 9 above. |
| Stroke-end timing | Argued from the cell count, not measured. |
| Resize | Implemented in `MapDraft` and tested, but **no UI is wired to it** — the spec's width/height panel does not exist. |
| Open (`Ctrl+O`) | Not implemented. `--map <id>` on the command line loads a map; there is no in-editor picker. |

## Branch Resolution

None — verdict is PASS on everything except criterion 9, which is recorded as unmet rather than
waived. Two spec items (resize UI, open dialog) are not built and are listed above.
