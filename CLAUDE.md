# Gridfall — Map

## What This Is

**Gridfall** is an isometric tower defense game built with **Godot 4 + C#**. Creeps walk a grid toward
your line; you place towers on it; the towers change the paths.

Two rules define the codebase and everything downstream of it:

1. **The simulation is deterministic.** Same map + same inputs + same tick count = byte-identical state.
2. **The Core never sees Godot.** The view reads simulation state and cannot write it — `SimStateView`
   makes that a compile-time fact, not a convention (ADR-0001).

## Structure

```
gridfall/
├── CLAUDE.md / CONTEXT.md   ← Layer 1 map (always loaded) + Layer 2 router
├── workspace.config.json    ← machine-readable definition of the workspaces below
│
│   ── the code (net8.0 unless noted) ──
├── Gridfall.Core/           ← the simulation. No Godot, no floats, no clock
├── Gridfall.Io/             ← reads content-data/; Core never touches disk
├── Gridfall.Verify/         ← net10.0 · determinism harness, balance sim, map/perf reports
├── Gridfall.Tests/          ← net10.0 · 102 tests
├── godot/                   ← Godot 4.6.3 · renderer, HUD, Dev/BoardEditor
│
│   ── the workspaces ──
├── docs/                    ← stable reference + engine-guide/ (the Core manual, 11 chapters)
├── workflows/               ← the runnable procedures (start at workflows/README.md)
├── game-design/             ← requirements, pillars, feature design
├── engine-systems/          ← simulation architecture + ADRs 0001–0005
├── content-data/            ← tower/enemy/wave/map data, balance targets, balance reports
├── presentation/            ← iso view, HUD, placeholders, prompts/ for Ludo.ai
├── tooling/                 ← board editor spec + headless CLIs
└── production/              ← the pipeline: 01-requirements/ … 06-release/
```

**Source lives at the repo root, not in a stage folder** — a .NET solution needs stable project paths;
`production/04-build/[slug]/` holds the build notes and points at the files.

## Quick Navigation

| Want to... | Go here |
|------------|---------|
| Pick the right workflow for a task | `workflows/README.md` |
| **Work inside the engine** | `docs/engine-guide/README.md` |
| Analyze a request into requirements | `game-design/CONTEXT.md` |
| Design a simulation system, or decide something technical | `engine-systems/CONTEXT.md` |
| Change a number, a wave, or a map | `content-data/CONTEXT.md` |
| Touch anything the player sees, or write asset prompts | `presentation/CONTEXT.md` |
| Board editor or the headless CLIs | `tooling/CONTEXT.md` |
| Move a slice through build → verify → release | `production/CONTEXT.md` |
| Know what a term means | `docs/glossary.md` |
| Know how grid coords become screen coords | `docs/iso-grid.md` |
| Know why the game is not balanced yet | `content-data/docs/reports/` (newest first) |
| Build, test, replay, or run the game | `docs/tech-standards.md` §Commands |

**Run Godot as `godot-mono`**, never `godot` or `godot-4` — those are 4.7 here and the project is
pinned to 4.6.3 mono (ADR-0005). A non-mono build silently ignores every C# script, which looks like
a broken game rather than the wrong binary.

## What Not to Load

Each workspace's `CONTEXT.md` carries a Load/Skip table, and the Skip column is a constraint, not
advice. Two rules hold everywhere: **never load a workspace you were not routed to** (cross-workspace
knowledge arrives as a handoff file), and **`docs/` and `_examples/` are load-on-demand only** — load
the one reference a Scope names, never the folder.

## Naming Conventions

One slug follows a slice from requirements to release, so `find_by_slug` returns the whole story.
The filename is the status; there is no database.

| Stage | Pattern | | Type | Pattern |
|---|---|---|---|---|
| 01 requirements | `[slug]-requirements.md` | | ADR | `ADR-[nnnn]-[slug].md` |
| 02 design | `[slug]-design.md` | | In progress | `[slug]-[status].md` |
| 03 architecture | `[slug]-architecture.md` | | Data | `[slug].json` |
| 04 build | `[slug]/` + `build-notes.md` | | Balance report | `[date]-[slug]-balance.md` |
| 05 verify | `[slug]-report.md` | | | |
| 06 release | `[slug]-v[n].md` | | | |

Statuses, in order: `backlog → ready → in-progress → review → done`.

## Flow

Domain workspaces hold standing knowledge. `production` carries **one slice at a time** through
`01-requirements → … → 06-release`, looping back a *stage* on a failed criterion.

```
game-design ──┬──▶ engine-systems ──┐
              ├──▶ content-data ◀───┼── tooling
              ├──▶ presentation ────┼──▶ production
              └─────────────────────┘
```

**There are no reverse handoffs.** Work belonging to a domain workspace comes back as a new item
there, never as a handoff pointed backwards.
