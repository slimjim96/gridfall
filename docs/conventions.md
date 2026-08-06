# Conventions

Stable reference. Load on demand.

## Filenames are the database

There is no tracker. The name of a file is its state.

| Pattern | Example | Meaning |
|---|---|---|
| `[slug]-[status].md` | `siege-tower-in-progress.md` | Work at a named status |
| `[slug]-requirements.md` | `path-recompute-requirements.md` | Pipeline stage 01 artifact |
| `[slug]-design.md` | `path-recompute-design.md` | Stage 02 |
| `[slug]-architecture.md` | `path-recompute-architecture.md` | Stage 03 |
| `[slug]/` | `path-recompute/` | Stage 04 — a folder of code + `build-notes.md` |
| `[slug]-report.md` | `path-recompute-report.md` | Stage 05 |
| `[slug]-v[n].md` | `path-recompute-v1.md` | Stage 06, versioned |
| `ADR-[nnnn]-[slug].md` | `ADR-0003-flow-field-pathfinding.md` | Decision record |
| `[YYYY-MM-DD]-[slug].md` | `2026-08-06-balance-pass.md` | Dated log entry |

Slugs are lowercase kebab-case, and **the slug stays the same for the whole life of a slice**. That is
what lets `find_by_slug` reassemble the trail from requirements to release.

Statuses, in order: `backlog → ready → in-progress → review → done`.

**Blocked work** is not a status suffix — the pipeline's status list is linear on purpose, so
`advance_status` never has a wrong answer. Record a block as a `## Blocked` section at the top of the
file, naming what you are waiting on and who owns it. It is visible where it matters: in the file.

## Data files

- Tower/enemy/wave/map data is **JSON**, authored by hand, one entity per file, named for the entity:
  `content-data/towers/frost-spire.json`.
- Godot `.tres` resources are **generated** from that JSON at import. Never hand-edit a `.tres`.
- A data file with no matching entry in `content-data/docs/balance-targets.md` is incomplete.

## Code

- Namespaces mirror folders: `Gridfall.Core.Systems`, `Gridfall.Core.Path`, `Gridfall.View.Iso`.
- One system per file, named for its tick step (`MovementSystem.cs`, `TargetingSystem.cs`).
- Anything in Core that could plausibly be nondeterministic gets a one-line comment saying why it isn't.
- Tests that assert on the state hash live in `Gridfall.Tests/Determinism/`.

## Commits

`<area>: <what changed>` where area is a workspace name or a slice slug.

```
engine-systems: ADR-0003 accepted — flow field over per-unit A*
path-recompute: reject builds that would fully block a spawn
content-data: frost-spire slow 40% → 35% (leak rate +1.2pp, within target)
```

Data changes cite the balance delta in the message. That is the whole audit trail.

## Writing for the next agent

- Every artifact opens with a one-sentence statement of what it is. No preamble.
- Decisions are recorded where they were made, when they were made. A decision reconstructed a week
  later is a guess wearing a decision's clothes.
- When you could not verify something, write that you could not verify it. "Compiles; not visually
  run" is a complete and acceptable sentence.
