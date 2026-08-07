# Camera Pan & Zoom — Requirements

**Slug:** `camera-pan-zoom` · **Status:** backlog · **Owner:** design-lead

## In One Sentence

You can move the camera around a board bigger than the screen — drag, edge-scroll, or arrow keys, the
way SimCity and Warcraft III do — and zoom in gentler steps than the current jump.

## Why now

**A legal map today can be more than half invisible, with no way to reach the rest.**

`MapValidator` permits any board from 8×8 to 64×64. `IsoGrid.FitOrthoSize` frames the board and then
clamps to `MaxOrthoSize = 30`. Working the clamp backwards:

```
needed = 0.3977 × (W + H)        ← cos(45°) yaw, then the 16:9 width constraint
framed = needed × 1.28           ← the margin FitOrthoSize applies
framed ≤ 30  ⟹  W + H ≤ 59
```

So **any board where width + height exceeds ~59 is cropped at maximum zoom-out, permanently.** A
32×32 map (span 64) already exceeds it. A 64×64 map (span 128) needs an ortho size of ~65 and gets
30 — well under half the board, and the remainder is unreachable by any input the game has.

The board editor can *paint* a 64×64 map. Nothing can look at one.

## This implements a contract that already exists

Not a new capability — a missing implementation of a written one.

- [`docs/iso-grid.md`](../../docs/iso-grid.md) §Camera behavior already states: *"Pan is clamped to
  the map bounds plus a two-cell margin."*
- `IsoGrid.PanMarginCells = 2.0f` exists **and is read by nothing.** A dead constant that has been
  documenting an unbuilt feature.
- [`tooling/docs/board-editor-spec.md`](../../tooling/docs/board-editor-spec.md) lists `Middle drag |
  Pan` in its input table. Also unbuilt, since v1.

Two documents and one constant have described this feature for as long as they have existed. This
slice makes them true, and the projection contract does not need to change.

## Pillar Check

| Pillar | | Note |
|---|---|---|
| 1 · The maze is the game | **Supports** | Bigger boards mean longer mazes. The map size the validator already allows becomes usable. |
| 2 · Legible at a glance | **At risk** | A camera the player can lose is a legibility regression. Losing the goal off-screen during a wave is the failure mode to design against. |
| 3 · Deterministic, therefore fair | Neutral | The camera is pure view. It must not touch the sim — assert an unchanged capture hash. |
| 4 · Every loss is explainable | **At risk** | "I lost a life and never saw the creep" is exactly the unexplainable loss pillar 4 forbids. A leak off-screen needs a cue. |
| 5 · Small numbers, big decisions | Neutral | No new numbers the player reasons about. |

## Scope

**In:**

- **Pan** by middle-drag, by arrow keys / WASD, and by edge-scroll (cursor at the screen edge), in
  both the game and the board editor
- **Clamp** to map bounds + `PanMarginCells`, so the board can never be lost entirely
- **Finer zoom steps** — see the open question below
- **Recentre** on a key, because a clamped camera can still be somewhere unhelpful
- Boards up to the validator's existing 64×64 becoming genuinely playable and editable

**Out, deliberately:**

| Not in | Why |
|---|---|
| Camera rotation | `iso-grid.md` is explicit: rotation, if ever added, snaps to the four 90° yaws. That is its own slice with its own art consequences — every asset's implied lighting assumes the contract angles. |
| Free pitch/yaw | Same. The projection contract is load-bearing for every tile and every prompt written so far. |
| Raising `MaxOrthoSize` past 30 as the fix | Zooming out far enough to fit a 64×64 board makes a creep ~12 px. That trades an unreachable board for an unreadable one. |
| Minimap | Plausibly the right answer to "where am I", but a separate feature with its own render path. |
| Momentum / inertial scrolling | Feel work. Wants a human at the controls, not a spec. |

## Open Questions — these change the work

1. **"Slight zoom factor" — finer steps, or a wider range?** The current range is `MinOrthoSize` 10 to
   `MaxOrthoSize` 30 (3×), stepped by `±1.5` per wheel notch — about 13 notches end to end. Finer
   steps are a one-line change; a wider range interacts with readability at both ends. **Assumed for
   now: finer steps, same range.**
2. **Is mobile a target platform?** "Mobile arrows" implies on-screen controls and touch drag, which
   is a distinct input surface, a HUD change, and a testing problem — not a variation on mouse input.
   Currently nothing in the repo targets mobile. **Assumed for now: design the input layer so touch
   can be added, but ship mouse and keyboard.**
3. **Should the camera follow anything automatically?** Snapping to a leak, or to a spawn when a wave
   starts, would answer pillar 4 — and would also take control away mid-wave. Probably a later slice.

## Acceptance Criteria

- [ ] A 64×64 board can be panned end to end, in the game and in the editor
- [ ] Pan is clamped to map bounds + `PanMarginCells`; the board cannot be lost off-screen
- [ ] `IsoGrid.PanMarginCells` is **read**, not merely declared
- [ ] Middle-drag, arrow keys/WASD, and edge-scroll all pan; a recentre key exists
- [ ] Zoom steps are finer than 1.5 per notch, within the existing 10–30 range
- [ ] **Picking still lands on the right cell after panning and zooming**, at both extremes
- [ ] The camera never changes pitch or yaw
- [ ] Every existing visual baseline is **byte-identical** — shot mode must pin the camera
- [ ] The determinism trace is unchanged, and a capture's sim hash is unchanged
- [ ] `docs/iso-grid.md` §Camera behavior and the board editor spec both become true

## Known Traps

**Picking is free, but verify it anyway.** `IsoGrid.TryPick` casts through
`camera.ProjectRayOrigin/ProjectRayNormal`, so it follows the camera with no code change. That makes
it exactly the kind of thing nobody tests and everybody assumes.

**The board editor resets the camera on every brush stroke.** `RebuildEverything` calls
`IsoGrid.ConfigureCamera(_camera, map)`, which re-frames from scratch — so a naive pan implementation
would snap back to centre every time you painted a cell. The camera's position has to survive a
rebuild, or become something the rebuild does not own.

**Shot mode must pin the camera or every baseline breaks.** There are currently five committed
captures (`board`, `sapper`, `repair`, `editor`, `editor-tiles`, `unit-formats`) and all of them
depend on `ConfigureCamera` framing the board identically every run. Any pan state that leaks into a
`--shot` run makes them all non-reproducible at once.

**Edge-scroll and a windowed cursor fight each other.** Cursor-at-edge panning is hostile in a window
you are trying to click outside of. Worth a modifier, a deadzone, or a toggle — and worth a human's
opinion before it ships.

## Downstream

| Workspace | What it will need |
|---|---|
| `engine-systems` | Nothing — the camera is view-only. No ADR unless the input layer grows a scheme worth recording. |
| `presentation` | The work. Camera state, input handling, and the readability judgement at both zoom extremes. |
| `tooling` | Board editor spec's `Middle drag \| Pan` row stops being aspirational; the guide's key table grows. |
| `content-data` | Maps larger than span 59 become authorable for the first time. `MapTargets` bands were tuned on 20×9 and 10×10 — a 64×64 board's path-length and buildable-share targets are almost certainly wrong at that scale. |
