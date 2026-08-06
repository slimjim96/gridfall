# Worked Example — `path-recompute`

One slice through all six stages, including a failed criterion and its loop-back. This is a **reference
pass**: none of this code exists yet. Read it to learn the shape of the artifacts, then skip it.

`_examples/` is in `excludedFolders`, so nothing here shows up in `session_start` or
`list_workspace_state` as real work.

## The slice

*Creeps must find a new route when the player changes the maze.* It is the first real slice Gridfall
needs, and it exercises everything: pathing, determinism, the never-fully-blockable rule, the tick
order, and a presentation contract.

## The trail

| File here | Would live at | Workflow |
|---|---|---|
| [`01-requirements.md`](01-requirements.md) | `production/01-requirements/path-recompute-requirements.md` | WF-01 |
| [`02-design.md`](02-design.md) | `production/02-design/path-recompute-design.md` | WF-02 |
| [`03-architecture.md`](03-architecture.md) | `production/03-architecture/path-recompute-architecture.md` | WF-03 |
| [`03-architecture-adr-0003.md`](03-architecture-adr-0003.md) | `engine-systems/decisions/ADR-0003-flow-field-pathfinding.md` | WF-X3 |
| [`04-build-notes.md`](04-build-notes.md) | `production/04-build/path-recompute/build-notes.md` | WF-04 |
| [`05-report-fail.md`](05-report-fail.md) | `production/05-verify/path-recompute-report.md` (first pass) | WF-05 |
| [`05-report-pass.md`](05-report-pass.md) | same path, after the loop-back | WF-05 |
| [`06-release-v1.md`](06-release-v1.md) | `production/06-release/path-recompute-v1.md` | WF-06 |

One slug, eight artifacts, six stages. `find_by_slug path-recompute` would return the whole story.

## What to notice

- **The requirements file contains no numbers.** "Recomputes within a tick" is a criterion; "within
  2 ms" arrives in architecture, where the budget lives.
- **The design spec does not mention flow fields.** That is a structural choice and it belongs to
  stage 03. Design says creeps re-route; architecture decides how.
- **Stage 03 places every change in the tick order** and answers the determinism checklist item by
  item — including the one that catches the bug in stage 05.
- **The build notes were written during the build**, which is why they record a decision the architect
  did not anticipate (the dirty-flag granularity).
- **The first verify FAILs on criterion 5** — the interesting part. Branch resolution names *one*
  stage to loop back to, and picks 04 over 03 with a reason. The architecture was right; the build
  broke the tie-break rule it specified.
- **The release note carries the un-verified item forward** rather than quietly dropping it.
