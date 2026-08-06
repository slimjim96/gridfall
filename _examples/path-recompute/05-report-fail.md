# Path Recompute — Verification (pass 1)

**Slug:** `path-recompute` · **Status:** review · **Verdict:** FAIL
*Workflow: WF-05. Would live at `production/05-verify/path-recompute-report.md`.*

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | 47 passed, 0 failed |
| Determinism trace | PASS | 2 runs × 3 maps, per-tick hashes identical through tick 5,400 |
| Perf ≤ 8 ms/tick | PASS | Worst case (4 lanes dirty, 300 creeps): 2.1 ms; p99 0.4 ms |

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | Placing a tower re-routes creeps on the board | PASS | `PathRecomputeTests.Build_LengthensRoute_AllCreepsFollow` |
| 2 | Selling re-routes creeps on the board | PASS | `Sell_ShortensRoute_AllCreepsFollow` |
| 3 | Sealing build refused, grid unchanged, player told | PASS | 3 maps × 4 sealing cells; `BuildRejected` emitted, cost array byte-identical before/after |
| 4 | Identical runs produce identical hashes | PASS | See determinism gate |
| 5 | Equal-cost routes: all creeps choose the same one, same across runs | **FAIL** | See below |
| 6 | No creep turns while between cells | PASS | `MidCell_CompletesCrossingBeforeTurning`, offsets 0.25/0.5/0.75 |
| 7 | Recompute stays inside the frame budget | PASS | See perf gate |
| 8 | Drag preview matches the route actually taken | NOT-VERIFIABLE-BY-AGENT | Preview is drawn by the view layer; `PreviewRoute` returns the correct cells in a unit test, but whether the drawn overlay matches was not seen |
| 9 | Sealing ghost shows refusal before release | NOT-VERIFIABLE-BY-AGENT | Same — the state is correct in the test, the rendering was not observed |
| 10 | Selling mid-wave re-routes on the same rule | PASS | `Sell_DuringWave_ReroutesAtCellBoundary` |

## Criterion 5 — what happened

The build notes flagged that the tie-break test used a **mirror-symmetric** map. Built an asymmetric
map with two genuinely different routes of equal length (a 7-cell dogleg north and a 7-cell dogleg
west) and re-ran.

Result: creeps split between the two routes. Cells in the northern half flow north; cells in the
western half flow west. It is perfectly reproducible — criterion 4 still passes, and 50 runs produce
the identical split — but it is not what criterion 5 asks for, and not what the architecture specified.

Root cause, in `FlowField.Build`: when BFS reaches an already-visited cell at **equal** distance, the
implementation overwrites the stored direction instead of leaving the first assignment in place. The
architecture note is explicit — *"the fixed N, E, S, W visit order makes the first-assigned direction
win"* — and first-assigned is exactly what the code does not do. On a mirror-symmetric map both
assignments are equivalent, which is why the existing test never caught it.

One-line shape of the fix: the equal-distance branch should be a no-op, not a write.

## Structural Invariants

| Invariant | Result |
|---|---|
| `Gridfall.Core` references no `GodotSharp` | PASS — no reference in the csproj, no `Godot` symbol in the assembly |
| No `float` / `double` / `Random` / `DateTime` in Core | PASS |
| Never-fully-blockable holds on every shipped map | PASS — 3 maps, exhaustive single-cell build search |
| State hash covers state the slice added | PASS — `_version` folded in at phase 9; verified by mutating it and observing the hash change |

## Branch Resolution

**Loop back to: 04 — build.** The architecture named the rule correctly and the code does not implement
it; nothing about the design or the structure needs to change.

Not 03: the flow-field approach and the fixed visit order are right, and the perf and determinism
results confirm it. Not 02: criterion 5 is a good criterion, and it did its job — it caught a real
divergence between the spec and the code.

Also for the build pass: replace the mirror-symmetric tie-break test with the asymmetric map used here.
The old test cannot fail, which makes it worse than no test.

## Not Verifiable By Agent

| # | What a human must check |
|---|---|
| 8 | Drag a tower across a legal cell — does the drawn route overlay match where creeps then walk? |
| 9 | Drag over a cell that would seal the lane — does the ghost turn red *before* you release? |
