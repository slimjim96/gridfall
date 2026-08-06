# Gridfall — Map

<!-- LAYER 1: always loaded. Where things live and what they're called — never how to do the work. -->

## What This Is

**Gridfall** is an isometric tower defense game built with **Godot 4 + C#**. Creeps walk a grid toward
your line; you place towers on it; the towers change the paths. The whole project is laid out so an AI
agent always knows what it's doing, what to load, and what to ignore.

Two rules define the codebase and everything downstream of it:

1. **The simulation is deterministic.** Same map + same inputs + same tick count = byte-identical state.
   Balance, replays, and regression tests all depend on this.
2. **The Core never sees Godot.** `Gridfall.Core` is a plain `net8.0` library. Godot lives only in the
   presentation layer, which reads simulation state and never mutates it.

## Structure

```
gridfall/
├── CLAUDE.md              ← you are here (Layer 1, always loaded)
├── CONTEXT.md             ← the router (Layer 2, read once per task)
├── workspace.config.json  ← machine-readable definition of everything below
├── docs/                  ← project-wide stable reference; load on demand, never by default
├── workflows/             ← the runnable procedures agents follow (start at workflows/README.md)
├── game-design/           ← requirements, pillars, feature design
├── engine-systems/        ← simulation architecture + ADRs
├── content-data/          ← tower/enemy/wave/map data + balance
├── presentation/          ← isometric view, camera, HUD, feel
└── production/            ← the pipeline: one slice, six stages
    ├── 01-requirements/ 02-design/ 03-architecture/
    └── 04-build/ 05-verify/ 06-release/
```

## Quick Navigation

| Want to... | Go here |
|------------|---------|
| Pick the right workflow for a task | `workflows/README.md` |
| Analyze a request into requirements | `game-design/CONTEXT.md` |
| Design a simulation system, or decide something technical | `engine-systems/CONTEXT.md` |
| Change a number, a wave, or a map | `content-data/CONTEXT.md` |
| Touch anything the player sees or clicks | `presentation/CONTEXT.md` |
| Move a slice through build → verify → release | `production/CONTEXT.md` |
| Know what a term means | `docs/glossary.md` |
| Know how grid coords become screen coords | `docs/iso-grid.md` |

## What Not to Load

Each workspace's `CONTEXT.md` carries a Load/Skip table, and the Skip column is a constraint, not
advice. Two rules hold everywhere, from this file down:

- **Never load a workspace you were not routed to.** Cross-workspace knowledge arrives as a handoff
  file, not as context you go and fetch.
- **`docs/` and `_examples/` are load-on-demand only.** Load the one reference a Scope names. Never
  the folder.

## Naming Conventions

| Type | Pattern | Example |
|------|---------|---------|
| Requirements | `[slug]-requirements.md` | `path-recompute-requirements.md` |
| Design spec | `[slug]-design.md` | `path-recompute-design.md` |
| Architecture note | `[slug]-architecture.md` | `path-recompute-architecture.md` |
| Build folder | `[slug]/` | `path-recompute/` |
| Verify report | `[slug]-report.md` | `path-recompute-report.md` |
| Release note | `[slug]-v[n].md` | `path-recompute-v1.md` |
| ADR | `ADR-[nnnn]-[slug].md` | `ADR-0002-flow-field-pathfinding.md` |
| Work in progress | `[slug]-[status].md` | `siege-tower-in-progress.md` |
| Data | `[slug].json` | `frost-spire.json` |

Statuses, in order: `backlog → ready → in-progress → review → done`. The filename is the status;
there is no database.

## Flow

Domain workspaces hold standing knowledge. `production` carries **one slice at a time** through six
stages, pulling from the domains on the way in.

```
game-design ──┬──▶ engine-systems ──┐
              ├──▶ content-data ────┼──▶ production
              └──▶ presentation ────┘

production:  01-requirements → 02-design → 03-architecture → 04-build → 05-verify → 06-release
                                    ▲                            │
                                    └──── loop back on a failed criterion ────┘
```

**There are no reverse handoffs.** A failed verify loops back a *stage* inside production. Work that
belongs to a domain workspace comes back as a new item there, not as a handoff pointed backwards.
