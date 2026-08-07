# Route Overlay — Verification

**Slug:** `route-overlay` · **Status:** review · **Verdict:** PASS

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` (5 projects) | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **86 passed**, 0 failed (was 77; +9) |
| Determinism trace | PASS | `Verify replay` — 30/30 checkpoints |
| Capture reproducible | PASS | Two runs + baseline all `c50a84c1…` |

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | The live route is drawn from the real flow field | PASS | `RouteOverlay` calls `PathSystem.TraceRoute`; visible in the capture |
| 2 | Hovering a buildable cell previews the resulting route | PASS | Gold pips in the capture, diverging from the live route at the hover cell |
| 3 | The preview matches what actually happens | PASS | `PreviewRoute_MatchesRealityAfterTheBuildLands` — predicted and actual cell sequences compared element by element |
| 4 | A sealing hover shows a refusal instead of a route | PASS by construction | `ShowPreviewFor` marks the cell in the reserved red when `WouldRemainConnected` is false. **Not seen** — `crossroads` has no sealable cell |
| 5 | The trace never runs away on a malformed field | PASS | `TraceRoute_TerminatesOnAnUnreachableCell`; step cap is the cell count |
| 6 | No allocation per frame | PASS by construction | Caller-provided span; one BFS per hover-cell *change*, not per frame |
| 7 | The view still cannot mutate pathing | PASS | `PathMutators_AreNotPubliclyReachable` — `SetBlocked`, `MarkDirty`, `ForceRebuild`, `RecomputeIfDirty`, `RestoreFrom` are all internal now |
| 8 | Routes readable against the terrain | PASS | Fixed after looking: see below |

## A hole closed on the way past

`PathSystem` had the same problem `SimState` had before the last slice: `SetBlocked`, `MarkDirty`,
`ForceRebuild`, `RecomputeIfDirty`, and `RestoreFrom` were all **public**. The renderer could have
dirtied or rebuilt the flow field and desynchronised itself from the simulation.

All five are now `internal`, reachable from `Sim`, `ContentLoader`, and the test suite, and not from the
Godot project. `SimStateView` closed half this boundary; this closes the other half.

## The failure worth recording

`PreviewRoute_MatchesRealityAfterTheBuildLands` failed on first run, and the cause is a distinction
that had been quietly blurred:

**`WouldRemainConnected` answers "does the board stay connected", not "may you build here."**

My fixture cell was path-only. The connectivity check happily said yes, the preview drew a detour, and
then the actual build was refused — so reality never changed and the two disagreed. The check was
right; the test was asking it the wrong question.

Two consequences:
- A new fixture, `LaneMap`, whose lane is buildable. `ArenaMap`'s lane is path-only, so **no legal build
  on it can change the route at all** — which means the neighbouring "preview is longer" test had been
  passing for the wrong reason.
- A test named for the distinction, `WouldRemainConnected_AnswersConnectivity_NotBuildability`, so the
  next person meets it as a documented property rather than as a confusing afternoon.

The view was already correct: `GameplayScene` checks `CellKind.Buildable` before previewing.

## Found by looking at the frame

`RouteLive` was first set to `7f96ad` — a muted slate that turned out to be almost exactly the buildable
terrain it is drawn on top of, so the live route was invisible. Raised to `cfe2f2`.

One emergent behaviour worth a human's opinion: the preview is drawn slightly above and slightly larger
than the live route, so where the two agree the preview covers it, and **the live route is only visible
where the routes actually differ**. That reads well — it shows the delta rather than two overlapping
lines — but it was not designed, and a player might read it as "the live route starts mid-board".

## Not Verified

| What | Why |
|---|---|
| The sealing-hover refusal | `crossroads` has no cell whose blocking would seal a lane, so the red path is unreachable in the real game today. Covered by construction and by the block-check tests, but not seen. |
| Whether always-on live routes are the right default | It is on by default with `r` to toggle. That is a taste call, and it wants a human playing rather than a still frame. |
| Readability at wave-18 density | Still four creeps. Carried forward. |

## Branch Resolution

None — verdict is PASS. The one failure was a test defect, fixed within stage 04.
