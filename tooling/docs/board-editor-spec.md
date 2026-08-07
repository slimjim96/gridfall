# Board Editor — Spec v1

A dev-only scene inside the game project for painting maps and immediately playing them.

> **Status 2026-08-06: built.** `godot/Dev/BoardEditor.cs`, launched with
> `./run-editor.sh <id>`.
> Built and verified in `production/05-verify/board-editor-report.md`.
> **To actually use it, read [board-editor-guide.md](board-editor-guide.md)** — this file is the spec,
> not a tutorial.
>
> **Three items from this spec are not built:** the resize panel (the model supports it, no UI),
> `Ctrl+O` open (use `--map <id>`), and the release-export exclusion — there is no
> `export_presets.cfg`, so "Dev/ is absent from a release build" is **unverified**, not passing.
> Follow-up slug `release-export`.
>
> **Extended 2026-08-07** (`terrain-tiles`): tile themes read from `presentation/tiles/`, and the
> overlay rebuilt as a left rail plus a brush bar. See Tile themes and The overlay, below.

**Deliberately small.** It is a development accelerator, not a product. Every feature below earns its
place by removing a hand-edit of JSON or a restart of the game; anything that does not do one of those
two things is out of scope.

## Where it lives

`godot/Dev/BoardEditor.tscn`, launched from a dev menu or `./run-editor.sh <id>`.

A scene in the game project, not a Godot `EditorPlugin` and not a separate app. The reason is single:
**it reuses the real renderer, the real `IsoGrid` mapping, and the real map loader.** What you paint is
drawn by the same code that draws the game, so an editor that looks right and a game that looks wrong
is not a possible state.

`Dev/` is excluded from release exports. The editor cannot ship by accident.

## Scope — v1

**In:**

- Paint cell types onto the grid
- Place spawns and the goal
- Resize the grid
- New / open / save maps as the game's own JSON format
- **Playtest**: run the map immediately with a test wave, then return to editing
- **Live validation**: the route, connectivity, and the map targets, updated as you paint

**Out, deliberately:**

| Not in v1 | Why |
|---|---|
| Wave composition | A second editor mode with its own UI. Wave tables stay hand-authored JSON. |
| Undo history beyond a flat stack | A 50-step flat undo is enough; branching history is not. |
| Multi-map / tileset management | The filesystem is the map manager. |
| Terrain height, decoration | Gridfall's grid is flat. **Theming was here too and is now in — see below.** |
| AI map generation | Nice to have. Not what unblocks development. |

## Editing

| Input | Action |
|---|---|
| `1` | Brush: buildable (`b`) |
| `2` | Brush: path-only (`.`) |
| `3` | Brush: blocked (`#`) |
| `4` | Brush: spawn (`S`) |
| `5` | Brush: goal (`G`) — placing a new goal moves the existing one |
| Left drag | Paint with the current brush |
| Right drag | Paint buildable (the eraser) |
| `[` / `]` | Brush size 1×1 / 3×3 |
| Middle drag | Pan (also arrows / WASD; `Home` recentres). Edge-scroll is **off** in the editor — painting a border wall means holding the cursor at the edge |
| Wheel | Zoom, clamped to the `IsoGrid` limits |
| `Ctrl+Z` / `Ctrl+Shift+Z` | Undo / redo, 50 steps |
| `Ctrl+S` | Save |
| `Ctrl+N` / `Ctrl+O` | New / open |
| `F2` | Toggle the route overlay |
| `F3` | Toggle the validation panel |
| `F4` | Cycle the terrain theme (saved in the map file) |
| `F5` | **Playtest** |
| `F6` | Run the maze estimate (on demand — see below) |
| `F7` | Re-read `presentation/tiles/` — see Tile themes, below |
| `Esc` | Return to editing from playtest |

Picking is the same ray-to-ground-plane intersection the game uses
([`docs/iso-grid.md`](../../docs/iso-grid.md) §Picking). Not a second implementation — the same function.

Grid resize is on a small panel: width and height spinners, 8–64, content anchored to the north corner
and truncated rather than scaled.

### Theming (added after v1)

`F4` steps through the registered terrain palettes and marks the draft dirty, because the theme is
saved in the map file and a change you cannot tell you have made is worse than no feature. The current
theme is shown on the brush bar.

v1 excluded theming on the grounds that "the grid is flat and the art is procedural". The second half
of that stopped being true when maps started declaring a ground palette. **The exclusion is lifted only
this far**: picking one of the shipped ramps is choosing what the map *declares*, not authoring art.
Terrain height and per-cell decoration are still out.

### Tile themes (added after v1)

A theme may now be **a folder of PNGs** rather than three colours:
`presentation/tiles/[theme]/[kind]/[name].png`. `F4` cycles colour ramps and tile folders alike;
`F7` re-reads the folder without relaunching.

The full folder contract — connection masks, variants, what connects to what — is
[`presentation/tiles/README.md`](../../presentation/tiles/README.md). It is not restated here.

**Themes are unrelated folders and need not hold the same files**, so `F4` routinely lands on a
theme missing tiles the last one had. That must never error, and it must never be silent:

- a missing tile is **substituted** with the nearest mask the theme does have, so a road keeps
  reading as a road rather than gaining a hole at every turn;
- the brush bar turns amber and **counts the substitutions**, and `F4` names them on the status line.

Both halves are load-bearing. Substituting without saying so trades a visible bug for an invisible
one, and the invisible one gets reported months later as "the tiles look weird sometimes".

### The surround

A theme may also carry `background/` — one image tiled across a large quad under and around the
board, so a board reads as a place rather than a slab in a void. It is **scenery, not board**:
nothing walks on it, and it can never be picked, because `IsoGrid.TryPick` solves the ground plane
analytically and bounds-checks against the map.

This is the one folder in the contract that is not a cell kind, and the distinction is worth keeping
sharp — everything else there is downstream of a simulation concept, and this is not.

**Why this is inside the scope above and not creep past it.** The theme is already an opaque string
that Core carries and never reads, so nothing new crosses the boundary; the editor gained no rule of
its own; and it removes a real hand-edit, which is the bar every feature here has to clear. What is
still out:

| Not in | Why |
|---|---|
| Per-cell tile placement | Choosing *this* bush for *that* cell needs a new per-cell layer in the map format. That is a format change, not an extension. Variants are distributed by coordinate hash instead. |
| Tile authoring or editing in-editor | The editor selects tiles. It does not draw them. |
| Terrain height, decoration | Unchanged from v1. Still out. |

The renderer reads the same folders in the game as in the editor (`TileLibrary.Scan` runs in both
`BoardEditor` and `GameplayScene`), which is what keeps "the editor cannot draw the board differently
from the game" true rather than hoped for.

## The overlay

Three regions, and a card that appears over them:

```
┌─ untitled *                    ! 1 warning ─┐
│ seeded for capture: a wall with one gap     │      status: which map, is it
└─────────────────────────────────────────────┘      dirty, does it save
┌─ VALIDATION ────────────────────────────────┐
│ ! buildable 60% is outside 35-55%           │      one row per finding,
│ · 20x12, 60% buildable                      │      coloured per severity
│ · path 19, spawns 1                         │
│ ─────────────────────────────────────────── │
│ maze estimate: 1.4x (lower bound, <= 3x)    │      F6, in the same card
└─────────────────────────────────────────────┘

                  ( board )

              ┌───────────────────┐
              │ ▣  ▤  ▩  ◆  ★     │              the five brushes, as the
              │ 1  2  3  4  5     │              tiles they actually paint
              │ path-only  1x1    │
              │ theme: desert     │
              └───────────────────┘
```

`F1` opens a key list centred over the board. `F3` hides the validation card.

**Everything sizes itself; nothing is positioned by a typed-in number.** The first version was five
labels at hand-computed y offsets, and the offsets were wrong — the maze estimate drew straight
through the last finding, because the guessed line height was 18px and the real one was 26. A panel
whose contents change length cannot have its layout written as constants.

Two rules that are easy to break here:

- **Every control ignores the mouse.** Picking happens in `_UnhandledInput`, and a `Control` with the
  default `MouseFilter.Stop` swallows the click first — so the board under the brush bar would
  quietly stop being paintable. Anything added to the overlay must go through `IgnoreMouse`.
- **Severity is per row, not per panel.** The old single-label panel turned every row red as soon as
  one row was an error, which made the error harder to find rather than easier.

## Playtest

`F5` builds a `Sim` from the in-memory map and a built-in test wave, and hands it to the normal
gameplay scene. `Esc` tears it down and returns to the editor with the map exactly as it was.

- The map does **not** need to be saved first. Playtesting an unsaved map is the entire point.
- The test wave is a fixed, hardcoded ramp — 20 runners then 5 brutes. Not configurable in v1; if you
  need a specific wave, author the wave table and run the game.
- Towers can be placed during playtest and are discarded on exit. Nothing a playtest does touches the
  map being edited.
- Playtest uses the real `Sim`, the real loader, and the real renderer. If a map crashes the loader in
  playtest, it would have crashed the game.

## Saving

Save runs the game's own `ContentLoader` validator before writing
([engine guide 07](../../docs/engine-guide/07-content-loading.md)). On failure it refuses to write and
shows the validator's message.

This is where validation comes from for free: the editor implements none of its own, so it can never
disagree with the game about what a legal map is.

Writes to `content-data/maps/<id>.json` in the format documented in engine guide 07 — the same rows-of-
strings layout, so a map still diffs readably in git. `meta.author` is set to `board-editor`.

## What it must not do

- **Invent a second map format, or a second validator.** It reads and writes the game's format and
  reports the game's verdict, or it is worse than useless.
- **Ship in a release build.** `Dev/` is excluded from export presets; there is no runtime flag to get
  it back.
- **Draw the board differently from the game.** If a divergence appears, the editor is wrong.
- **Validate on its own terms.** Only the game's validator decides.
- **Grow a wave editor without a new spec.** That is a v2 conversation.

## Live validation

The map is checked as you paint. The point is to make a broken map **visible at the moment you break
it**, rather than at save, at load, or three waves into a playtest.

Same rule as everything else here: the editor **decides nothing itself**. Errors are the game
validator's verdict, surfaced early. Warnings are the map targets, read from the same constants the
balance sim uses.

### Three severities

| Level | Blocks save? | Source of truth | Shown as |
|---|---|---|---|
| **Error** | Yes | `ContentLoader`'s validator | Reserved red, offending cells outlined |
| **Warning** | No | `MapTargets` constants | Amber row in the validation panel |
| **Info** | No | Computed metrics | Plain row in the panel |

Errors are exactly the conditions the game already refuses to load
([engine guide 07](../../docs/engine-guide/07-content-loading.md)) — no goal, no spawn, a spawn that
cannot reach the goal, ragged rows. The editor adds none of its own, which is what keeps "the editor
and the game agree about what a legal map is" true by construction.

### What runs on every stroke

Recomputed on **stroke end** — mouse-up, not per pixel — and throttled to at most once per 100 ms
during a drag.

| Check | Level | Condition |
|---|---|---|
| Goal exists | Error | Exactly one `G` |
| At least one spawn | Error | ≥ 1 `S` |
| Every spawn reaches the goal | Error | `PathSystem.Build` on the empty board; any spawn at `Unreachable` fails, and **that spawn is outlined** |
| Isolated buildable pockets | Warning | Buildable cells the creeps can never path near are dead space |
| Shortest path, unmazed | Warning | Target 18–30 cells |
| Buildable share | Warning | Target 35–55% of the grid |
| Lane count | Info | 1–3 |
| Grid size, cell counts | Info | — |

Cost is one `PathSystem.Build` on the in-memory map: ~4,096 cell visits, well under a millisecond. It is
the same function the game uses in tick phase 2 — not a second implementation
([engine guide 06](../../docs/engine-guide/06-pathing.md)).

### The route overlay

`F2` draws what the flow field produces from each spawn: the route creeps would actually walk on the
empty board. It is the same `_flow` array the game reads, rendered by the same code as the in-game route
highlight.

Unreachable cells are dimmed. This is usually how you *see* the problem before you read the panel —
a lane going dark as you close it is more legible than a line of text.

### The maze estimate — `F6`, on demand

The one check that cannot run live. `MapTargets` caps the longest path achievable by legal tower
placement at **3× the unmazed path**, and finding the true worst case is a search problem, not a query.

`F6` runs a **greedy approximation**: repeatedly block whichever single buildable cell lengthens the
path most, skipping any block the game would refuse, until no legal block remains. Cost is
O(buildable² × cells) — a second or two on a full 64×64 map, which is why it is a keypress and not a
stroke handler.

Report it honestly, in the panel and in the code comment:

> Maze estimate: 2.4× (greedy lower bound — the true worst case may be higher)

Greedy is a **lower bound**. An estimate under 3× is not proof the map is inside the target; an estimate
over 3× is proof it is not. Do not print it as though it were exact, and do not let a green estimate
turn into a claim in a report.

### What live validation must not become

- **A second validator.** If the editor and the game ever disagree about legality, the editor is wrong,
  and the fix is to delete the editor's opinion — never to add a matching rule to the game.
- **A blocker on warnings.** Only errors stop a save. An unusual map is often a deliberate one, and a
  tool that refuses to let you build the strange thing is a tool you stop using.
- **Silent.** Every error names the cell and says what is wrong in the validator's own words.

### Keeping the targets in one place

The warning thresholds are `MapTargets` constants, shared by the editor and the balance sim's map
report, and documented for humans in
[`content-data/docs/balance-targets.md`](../../content-data/docs/balance-targets.md) §Map targets.

**Changing a target means changing the constant and the doc together.** Two copies of a number is one
copy too many, and the doc is the one people read before they trust the panel.

## Done when

- [ ] A map can be created, painted, saved, and loaded by the game with no hand-editing
- [ ] `F5` plays the unsaved map and `Esc` returns to it unchanged
- [ ] Save refuses an invalid map with the validator's own message
- [ ] Closing the last route to a spawn shows the error **on the stroke that closes it**, and names
      that spawn
- [ ] The route overlay is drawn from the same `_flow` array the game reads
- [ ] Warnings never block a save; only validator errors do
- [ ] The maze estimate is labelled a lower bound wherever it appears
- [ ] `MapTargets` is the only place the warning thresholds are written in code, and
      `balance-targets.md` matches it
- [ ] Stroke-end validation stays under 1 ms on a full 64×64 map
- [ ] The editor's rendering and the game's rendering come from the same code path
- [ ] `Dev/` is absent from a release export — verified, not assumed
- [ ] Every keybind above works and is listed on an in-editor help overlay (`F1`)
- [ ] A theme dropped into `presentation/tiles/` appears in `F4` with no code change
- [ ] A map with no tile folder renders **byte-identically** to before tiles existed
- [ ] The overlay never swallows a click meant for the board
- [ ] Nothing in the overlay is positioned by a hand-computed pixel offset
