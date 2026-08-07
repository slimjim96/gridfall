# Gridfall

An isometric tower defense game — **Godot 4 + C#** — laid out as a folder-as-workspace project so AI
agents can do real work in it without being re-briefed every session.

The maze is the game: placing a tower changes where creeps walk. That one design decision is what
drives the engineering standard, because a game whose paths change constantly is only balanceable if
the simulation is deterministic.

## For a human, first time

1. `CLAUDE.md` — the map. Where everything lives.
2. **`docs/engine-guide/`** — the developer manual for the simulation. Eleven chapters: the tick loop,
   fixed-point math, the state hash, pathing, content loading, the determinism playbook, and two
   end-to-end recipes. This is the centre of the project.
3. `workflows/README.md` — the eleven procedures agents run.
4. `_examples/path-recompute/` — one feature taken through all six stages, including a failed
   criterion and the loop-back that fixed it. The fastest way to see the shape of the thing.

## For an agent, every session

```
session_start                  → orientation brief
CLAUDE.md                      → the map (always loaded)
CONTEXT.md                     → route the task to one workspace
<workspace>/CONTEXT.md         → what to load, what to skip
workflows/<one workflow>       → the procedure
```

## Layout

| Path | What it is |
|---|---|
| `CLAUDE.md` | Layer 1 — the map. Always loaded. |
| `CONTEXT.md` | Layer 2 — the router. Read once per task. |
| `*/CONTEXT.md` | Layer 3 — a scope. Load/skip rules for one area. |
| `workflows/` | The runnable procedures. Six pipeline + five cross-cutting. |
| `docs/` | Stable reference: tech standards, the iso-grid contract, conventions, glossary. |
| `docs/engine-guide/` | **The engine manual.** Eleven chapters + two recipes. |
| `game-design/` | Requirements, pillars, feature design. |
| `engine-systems/` | Simulation architecture and ADRs. |
| `content-data/` | Towers, enemies, waves, maps — and the balance targets they answer to. |
| `presentation/` | Iso view, HUD, feel, placeholders, and `prompts/` for Ludo.ai. |
| `tooling/` | The in-Godot board editor and the headless CLIs. |
| `production/` | The pipeline. One slice, six stages, filename as status. |
| `workspace.config.json` | All of the above, machine-readable. |

### The code

| Path | Target | What it is |
|---|---|---|
| `Gridfall.Core/` | `net8.0` | The simulation. No Godot, no floats, no clock. |
| `Gridfall.Io/` | `net8.0` | Reads `content-data/` off disk, so Core never touches the filesystem. |
| `Gridfall.Verify/` | `net10.0` | Determinism harness, balance sim, map and perf reports. |
| `Gridfall.Tests/` | `net10.0` | 175 tests. |
| `godot/` | `net8.0` | Godot 4.6.3 project: renderer, HUD, and the board editor under `Dev/`. |

## The pipeline

```
01-requirements → 02-design → 03-architecture → 04-build → 05-verify → 06-release
                                     ▲                          │
                                     └──── loop back on a failed criterion
```

One slug follows a slice from requirements to release, so `find_by_slug` returns the whole story.

## Art and tools

**Every visual is a placeholder** — procedural C#, minimal detail, an hour's budget, one hard
requirement: a distinct silhouette. They exist so the game is playable and balanceable now. Final
assets come from **Ludo.ai**, run by a human and tweaked in an image editor, and the durable artifact is
the prompt set in `presentation/prompts/` — sprite form, mesh form, and animation clips, written while
the design intent is still fresh. Both formats work behind one view interface (ADR-0004), so the
question of what Ludo.ai actually returns does not block anything.

The **board editor** is a dev-only scene inside the game project (`godot/Dev/`): paint the grid, place
spawns and the goal, hit `F5` to play the unsaved map, `Esc` to come back. It validates as you paint —
the route drawn from the real flow field, and a broken map flagged on the stroke that breaks it. It
reuses the game's own renderer, picker, loader, and validator, so it cannot disagree with the game about
what a legal map is: errors are the game's verdict shown earlier, warnings are the balance targets, and
only errors block a save. Scoped to geometry, theme, playtest, and validation; wave editing is out by
decision, and `tooling/docs/board-editor-spec.md` says why.

A map also declares a **terrain theme** — `slate`, `forest`, `desert`, `ocean`, `underwater`,
`mountain`, `space` — cycled with `F4` and saved in the map file. The simulation never reads it.

## The two rules everything else follows from

1. **The simulation is deterministic.** `Gridfall.Core` is a plain `net8.0` library: fixed-point math,
   a seeded PRNG, stable iteration order, and a per-tick state hash the harness diffs. Same inputs,
   same trace, every platform.
2. **The Core never sees Godot.** The view reads simulation state and emits commands. It mutates
   nothing. See `engine-systems/decisions/ADR-0001`.

## Tooling

`.mcp.json` registers [`folder-workflow-mcp`](../folder-workflow-mcp/), which supplies
`session_start`, `advance_stage`, `handoff`, `find_by_slug`, `lint_workspace`, and the rest. The
architecture this project uses is documented for humans in
[The Mirror Method](../mirror-workflow-guide/).

## Running it

```bash
dotnet build && dotnet test                                    # 175 tests
dotnet run --project Gridfall.Verify -- replay                  # determinism: replay recorded traces
dotnet run --project Gridfall.Verify -c Release -- balance --map crossroads --runs 30
./run-game.sh                                                   # play it
./run-editor.sh crossroads                                      # board editor
```

**Godot is pinned to 4.6.3 mono, run as `godot-mono`** — not `godot` or `godot-4`, which are 4.7 here
(ADR-0005). Capture a frame with `-- --shot /tmp/x.png --shot-after 40`; the capture is
byte-reproducible and there are baselines in `presentation/docs/`.

## Where it actually stands

The engine, the renderer, and the editor exist and are green: 175 tests, determinism traces replaying,
and every mechanic below reachable in the running game.

**crossroads is balanced. It is the only map that is.**

| | crossroads |
|---|---|
| Leak rate | 1.6% (target ≤ 4%) |
| Runs lost, waves 1–10 | 3.5% (target 0–5%) |
| Runs lost, waves 11+ | 21.5% (target 15–30%) |
| Lost runs end at wave | 10.9 of 12 |

Getting there took eleven balance passes, and the pattern in the last four is worth knowing before
starting a twelfth: **every failure was invisible to the metric rather than absent from the game.**

- `tower-combat` shipped a tuning that hit both targets while the new mechanic did nothing.
- `tower-repair` deleted tower destruction entirely — at *every legal price* — with both targets green.
- `salvage-value` deleted it again by a second route the new guard metric could not see.
- `early-economy-2` found the game was decided by wave 4: the 26% runs-lost everyone read as "ok" was
  25.5% early and 0.5% late, against two targets that had been in the doc, unmeasured, from the start.

The guard numbers those produced — `gold destroyed`, the runs-lost split, and the spread of lives left —
are printed by `balance` on every run for that reason. **Read the spread before the mean**: a map whose
runs all end identically has no difficulty curve, only a threshold.

`gauntlet` is a **documented negative result**, kept as evidence. It was built to satisfy a proposed
map-density target and cannot be balanced at any growth rate: its route is fixed by its walls, so all
200 runs finish with exactly 20 lives (sd 0.0) and difficulty steps 0% → 95% on a 0.005 change. Five
fixes were tried and rejected. The finding generalises — **the way to score well on density is to wall
the route in, which deletes mazing** — so density must not become a target on its own.

The current direction is board themes, tile art, and a shared tower pool drawn from per-theme subsets:
`game-design/docs/board-themes-direction.md`.
