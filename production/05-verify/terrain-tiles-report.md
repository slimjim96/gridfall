# Terrain Tiles + Editor Overlay — Verification

**Slug:** `terrain-tiles` · **Status:** review · **Verdict:** PASS (with one item for a human)

Two asks in one slice: make the board editor's overlay readable, and make it possible to drop tile
art in and select it. They are one slice because the tile picker *is* part of the overlay.

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` (5 projects) | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **176 passed**, 0 failed (was 175; +1) |
| Determinism trace | PASS | `Verify replay` — `crossroads-baseline`, 3000 ticks, 30/30 checkpoints |
| Sim untouched by the visuals | PASS | Game capture hash `b9c3bc7c95e6f726` identical before and after the last render change |
| Scene wiring | PASS | `./run-game.sh --headless --quit` clean; prints `tiles: loaded roadway (24)` |
| **Untiled board unchanged** | PASS | `md5 711cb6f427594330b4b4ea27f8e0bd3d` — byte-identical to the committed `board-baseline.png` |

That last row is the one that matters most. The terrain mesh was restructured (UVs added, tops split
from sides, per-texture surfaces); a board with no tile folder still renders **the same bytes** it did
before any of it. The refactor is provably invisible where it should be.

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | A folder of PNGs becomes a selectable theme with no code change | PASS | `presentation/tiles/roadway/` loads as `roadway`; console `tiles: loaded roadway (24)` |
| 2 | Path tiles connect like a road | PASS | `editor-tiles-baseline.png` — two corners, a straight, and both arms of the seeded road |
| 3 | Multiple variants per kind, distributed | PASS | Same capture — stone and bush mixed along one wall |
| 4 | Variant choice is stable, not random | PASS by construction | `TileLibrary.VariantIndex` is a coordinate hash, no RNG, no state; repeated captures identical |
| 5 | Tiles cannot reach the simulation | PASS | Pre-existing `TheThemeIsNotSimulationState`; capture hash unchanged across render edits |
| 6 | The editor and the game draw the same board | PASS | `TileLibrary.Scan` in both `BoardEditor` and `GameplayScene`; game capture shows the same tileset |
| 7 | A partial theme degrades rather than breaks | PASS by construction | Resolution is exact mask → unmasked → theme colour, per kind |
| 8 | New PNGs appear without relaunching | PASS by construction | `F7` re-runs `Scan`; tiles load via `Image.LoadFromFile`, outside `res://`, so no import step exists to skip |
| 9 | Overlay laid out by containers, not offsets | PASS | `EditorHud` is `VBoxContainer`/`PanelContainer` throughout; no y arithmetic remains |
| 10 | Severity is per row | PASS | `errors.png` — red error row above plain info rows, in one card |
| 11 | Findings of any length do not collide | PASS | The estimate sits in the same card, below a separator; it moves with the list by construction |
| 12 | The overlay does not swallow board clicks | **PASS by construction only** | Every control set to `MouseFilter.Ignore` via `IgnoreMouse`. **Not exercised** — see Not Verified |
| 13 | The colour-only path still works | PASS | `editor-baseline.png` — slate swatches, `theme: slate (colours, F4)` |

## A crash found on the way past

Painting over the goal and then pressing `F4` **threw** `IndexOutOfRangeException` and killed the
editor.

`RebuildGeometry` — the mid-stroke path — has always guarded `!map.Goal.IsValid`, because a draft is
obviously allowed to be illegal while you are painting it. `RebuildEverything` did not, and built a
`PathSystem` unconditionally; `PathSystem` seeds its queue from the goal index with no check.

So the editor crashed at the exact moment it was one line away from displaying "map has no goal".
Reachable before this slice via `F4` and `Ctrl+N`; this slice's `F7` would have added a third door.

Fixed by giving `RebuildEverything` the same guard, and by adding `RouteOverlay.Clear()` so a
goal-less board does not keep drawing a route to a goal that is gone. Regression test:
`ADraftWithNoGoal_IsFlaggedAndCannotBuildAFlowField`, which pins the precondition rather than the
symptom — if `PathSystem` ever tolerates a goal-less map, the test fails and the guard can go.

Verified in `errors.png`: the goal-less draft draws, reports `× map has no goal`, clears the route,
and does not throw.

## Two defects caught by looking at a frame

Both were invisible to the compiler and to every test:

1. **Walls rendered untextured.** `occupied` was computed as `_path.IsBlocked(index)` — which is true
   for blocked *terrain* as well as for a tower — so every wall counted as occupied and lost its
   tile. A stone theme drew its walls in flat ramp colour. The original code sidestepped this by
   handling `Blocked` in a separate branch; the refactor merged the branches and inherited the bug.
2. **Inactive brush swatches went grey.** They were dimmed with alpha, which let the frame's
   colour-ramp background blend through — an inactive grass tile arrived as grey-green. Darkened
   instead of faded, so a swatch never shows a hue the theme does not have.

A third was fixed in the placeholder art rather than the code: the bush was authored within ~14
levels of its own ground and rendered as one flat green square. Same failure mode, and the same fix,
as the original slate terrain ramp — which is now noted in the tile README as a rule, not an anecdote.

## Scope

Added, and recorded in the spec: tile themes, `F7`, `--theme` on the editor, the overlay rebuild.

Explicitly **not** added, and recorded in the spec and the guide:

- Per-cell tile placement — needs a new layer in the map format. Variants go by coordinate hash.
- Tile drawing or editing in-editor.
- Terrain height and decoration — unchanged from the v1 exclusion.
- Wave editing — unchanged, still out by decision.

## Not Verified

| What | Why |
|---|---|
| **Mouse pass-through under the brush bar** | Needs a live click on a cell beneath the bar. `MouseFilter.Ignore` is set on every control, but the capture harness does not click. **First thing to try.** |
| `F7` reload with a genuinely new folder mid-session | The code path is the same `Scan` that runs at startup and is proven, but "add a folder while it is running" was not performed. |
| Whether the layout holds at other window sizes | Captured at 1280×720 only. The rail is anchored top-left and the bar bottom-centre, so it should, but "should" is not "seen". |
| Whether the tiles look *good* | Not an agent judgement, and they are placeholders by design — the bar is readability, not quality. |
| `Dev/` absent from a release export | Unchanged from the previous report: there is still no `export_presets.cfg`. Follow-up slug `release-export`. |

## Branch Resolution

None — verdict is PASS. The editor crash was found and fixed within stage 04, with a regression test.
