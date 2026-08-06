# Path Recompute — Verification (pass 2, after loop-back)

**Slug:** `path-recompute` · **Status:** review · **Verdict:** PASS
*Workflow: WF-05, second run. Would overwrite `production/05-verify/path-recompute-report.md`;
`version_file` keeps pass 1 as `path-recompute-report-v1.md`.*

## What changed since pass 1

The slice went back to `04-build`. Two changes:

1. `FlowField.Build` — the equal-distance branch is now a no-op. First assignment wins, which is what
   the architecture specified.
2. `PathRecomputeTests.EqualCost_AsymmetricMap_SingleRoute` replaces the mirror-symmetric test. The
   asymmetric map from pass 1 is committed as `test-maps/dogleg-tie.json`.

Build notes updated with a third row in the decisions table recording why the equal-distance branch is
a no-op — so the next person to "optimize" it has to read the reason first.

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | 48 passed, 0 failed (one test replaced, one added) |
| Determinism trace | PASS | 2 runs × 4 maps, identical per-tick hashes through tick 5,400 |
| Perf ≤ 8 ms/tick | PASS | Worst case 2.1 ms — unchanged; the fix removed a write, not added one |

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | Placing re-routes creeps | PASS | unchanged from pass 1 |
| 2 | Selling re-routes creeps | PASS | unchanged |
| 3 | Sealing build refused | PASS | unchanged |
| 4 | Identical runs, identical hashes | PASS | re-run including the new map |
| 5 | Equal-cost: one route, same every run | **PASS** | `EqualCost_AsymmetricMap_SingleRoute` — all 40 creeps take the northern dogleg; identical across 50 runs |
| 6 | No turning between cells | PASS | unchanged |
| 7 | Inside the frame budget | PASS | see gate |
| 8 | Drag preview matches the real route | NOT-VERIFIABLE-BY-AGENT | **human signed off 2026-08-06** — overlay matches |
| 9 | Sealing ghost shows refusal pre-release | NOT-VERIFIABLE-BY-AGENT | **human signed off 2026-08-06** — with a note: the flash is too brief to notice at first. Filed as a follow-up, not a blocker |
| 10 | Selling mid-wave re-routes | PASS | unchanged |

## Structural Invariants

| Invariant | Result |
|---|---|
| Core references no `GodotSharp` | PASS |
| No `float` / `double` / `Random` / `DateTime` in Core | PASS |
| Never-fully-blockable on every shipped map | PASS — 4 maps now, including `dogleg-tie` |
| State hash covers new state | PASS |

## Branch Resolution

None — verdict is PASS. The slice advances to `06-release`.

## Carried Forward

- Criterion 9's human sign-off came with a usability note: `refusalFlashDuration` is set too low to
  read. That is a knob, it is owned by `content-data`, and it goes in the release note as a follow-up
  rather than holding the slice.
