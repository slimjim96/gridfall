# Board Editor — Spec v1

A dev-only scene inside the game project for painting maps and immediately playing them.

**Deliberately small.** It is a development accelerator, not a product. Every feature below earns its
place by removing a hand-edit of JSON or a restart of the game; anything that does not do one of those
two things is out of scope.

## Where it lives

`godot/Dev/BoardEditor.tscn`, launched from a dev menu or `godot --scene Dev/BoardEditor.tscn`.

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

**Out, deliberately:**

| Not in v1 | Why |
|---|---|
| Wave composition | A second editor mode with its own UI. Wave tables stay hand-authored JSON. |
| Live validation while painting | Deferred — see the note below. |
| Undo history beyond a flat stack | A 50-step flat undo is enough; branching history is not. |
| Multi-map / tileset management | The filesystem is the map manager. |
| Terrain height, decoration, theming | Gridfall's grid is flat and the art is procedural. |
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
| Middle drag | Pan |
| Wheel | Zoom, clamped to the `IsoGrid` limits |
| `Ctrl+Z` / `Ctrl+Shift+Z` | Undo / redo, 50 steps |
| `Ctrl+S` | Save |
| `Ctrl+N` / `Ctrl+O` | New / open |
| `F5` | **Playtest** |
| `Esc` | Return to editing from playtest |

Picking is the same ray-to-ground-plane intersection the game uses
([`docs/iso-grid.md`](../../docs/iso-grid.md) §Picking). Not a second implementation — the same function.

Grid resize is on a small panel: width and height spinners, 8–64, content anchored to the north corner
and truncated rather than scaled.

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

- **Invent a second map format.** It reads and writes the game's format, or it is worse than useless.
- **Ship in a release build.** `Dev/` is excluded from export presets; there is no runtime flag to get
  it back.
- **Draw the board differently from the game.** If a divergence appears, the editor is wrong.
- **Validate on its own terms.** Only the game's validator decides.
- **Grow a wave editor without a new spec.** That is a v2 conversation.

## Deferred, with a note

**Live validation while painting** — showing the current route, flagging an unreachable goal, warning
when the map violates the never-fully-blockable rule — is out of v1 by decision.

Worth recording: once playtest exists, the pieces are already present. `PathSystem.Build` runs on the
in-memory map in well under a millisecond, and `SimStateView.PreviewRoute` already exists for the drag
preview. If painting starts producing broken maps often enough to hurt, this is a small addition and it
should be reconsidered — not rebuilt from scratch.

## Done when

- [ ] A map can be created, painted, saved, and loaded by the game with no hand-editing
- [ ] `F5` plays the unsaved map and `Esc` returns to it unchanged
- [ ] Save refuses an invalid map with the validator's own message
- [ ] The editor's rendering and the game's rendering come from the same code path
- [ ] `Dev/` is absent from a release export — verified, not assumed
- [ ] Every keybind above works and is listed on an in-editor help overlay (`F1`)
