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
| `Gridfall.Tests/` | `net10.0` | 102 tests. |
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
only errors block a save. Scoped to geometry, playtest, and validation; wave editing is out by decision,
and `tooling/docs/board-editor-spec.md` says why.

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
dotnet build && dotnet test                                    # 102 tests
dotnet run --project Gridfall.Verify -- replay                  # determinism: replay recorded traces
dotnet run --project Gridfall.Verify -c Release -- balance --map crossroads --runs 30
godot-mono --path godot                                         # play it
godot-mono --path godot --scene res://Dev/BoardEditor.tscn -- --map crossroads
```

**Godot is pinned to 4.6.3 mono, run as `godot-mono`** — not `godot` or `godot-4`, which are 4.7 here
(ADR-0005). Capture a frame with `-- --shot /tmp/x.png --shot-after 40`; the capture is
byte-reproducible and there are baselines in `presentation/docs/`.

## Where it actually stands

The engine, the renderer, and the editor exist and are green. **The game is not balanced, and the
reason is now well understood rather than suspected.**

Three balance passes (`content-data/docs/reports/`) converged on the same conclusion from different
directions: the problem is the economy, not the enemies.

- Difficulty peaks at **wave 3**, when the player is broke, and collapses after — waves 6–11 leak
  nothing.
- Late gold runs away (378 → 1090) because **there is no gold sink that scales**. Towers cannot be
  upgraded, so once the board saturates, bounties pile up with nothing to buy.
- Per-wave HP scaling exists and was necessary, but it moved the difficulty rather than fixing it.

The next slice that would change the game rather than measure it is **tower upgrades**.

Every number in `content-data/` is still a placeholder, marked `_untuned`.
