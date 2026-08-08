# Camera Pan & Zoom — v1

**Slug:** `camera-pan-zoom` · **Status:** done · **Verified at trace:** unchanged

Boards bigger than the screen are usable. The camera moves the way SimCity and Warcraft III move —
drag, edge-scroll, arrow keys — and zoom got gentler.

## What Shipped

**`CameraRig`** — a focus point on the ground plus an ortho size, and nothing else. The pitch and yaw
are the projection contract's and are never touched. Both the game and the board editor drive the
same rig, so panning cannot behave one way while you paint and another while you play.

| Input | Does |
|---|---|
| Middle drag | Pan, board tracking the cursor exactly |
| Arrows / WASD | Pan, speed scaling with zoom |
| Cursor at screen edge | Pan — **game only** |
| Wheel | Zoom, multiplicative |
| `Home` | Recentre |

## This was a missing implementation, not a new feature

`docs/iso-grid.md` has said *"pan is clamped to the map bounds plus a two-cell margin"* since it was
written. `IsoGrid.PanMarginCells = 2.0f` existed **and was read by nothing**. The board editor spec
has listed `Middle drag | Pan` since v1. Two documents and a constant described this for as long as
they have existed; the slice makes them true and changes no contract.

It was also already a live defect: `FitOrthoSize` clamps to `MaxOrthoSize = 30`, so any board where
width + height exceeded ~59 was cropped **permanently**, with no input able to reach the rest. The
validator has always permitted 64×64. The editor could paint one and nothing could look at one.

## Three decisions worth the words

**Zoom is multiplicative — 1.06 per notch, not a flat 1.5.** A flat step is 5% of the range zoomed
out and 15% zoomed in, so the same notch lurched at one end and did nothing at the other. A ratio
feels identical everywhere, and gives ~19 notches across 10–30.

**Vertical drag divides by `GroundCompression` = sin(pitch).** A world step along `ScreenUp` covers
only half the screen at 30°, so without it the board lags the cursor at half speed vertically. Same
0.5 `FitOrthoSize` already uses.

**Edge-scroll is off in the board editor.** Painting a border wall means holding the cursor at the
screen edge on purpose; a camera that slides away while you do it is worse than no panning. On in the
game, where it is the behaviour that was asked for.

And one that is easy to get wrong: **`Reframe` only re-clamps, it does not re-frame.** The editor
rebuilds the world on every brush stroke, and re-centring there would make a large board impossible
to edit.

## Verification

**4096 of 4096.** Picking follows the camera "for free" through `ProjectRayOrigin` — the kind of
claim nobody checks. So it was checked exhaustively: on a 64×64 board, after a synthetic middle-drag
and six zoom notches through the rig's real public API, every cell centre was projected to screen and
fed back through `TryPick`. Every one came back to the cell it started in.

**Six committed captures byte-identical**, which is what proves shot mode's `Locked` is honoured
rather than merely present. `dotnet build` 0/0, **182 tests** (+3), `replay` 30/30.

Three of the new tests read the view's source, following `MapThemeTests`: that `PanMarginCells` is
genuinely read, that the rig never assigns pitch/yaw/`Basis`, and that shot mode can lock it.

## The slice was held for a day, on purpose

Both X displays died mid-slice, so the four criteria that need a frame were filed as **unverified,
not passed** — and they were the four most likely to catch a defect. The verify report was written
that way and the slice stayed at `review` until the session came back. Recorded rather than tidied
away.

Diagnosing the outage did surface a real bug: the editor constructed a rig and went straight to
`RebuildEverything` → `Reframe` without ever calling `Initialise`, so it had no camera and would have
thrown on the first frame.

## Player-Facing Change

You can move the camera, and zoom in steps that no longer lurch. Boards larger than the screen are
playable and editable for the first time.

## Follow-Ups Not Done

| Item | Workspace | Slug |
|---|---|---|
| `MapTargets` bands are size-absolute and wrong at scale — a 64×64 board reports 89% buildable (target 35–55%) and path 63 (target 18–30) just for being large | content-data | `map-targets-at-scale` |
| Touch / on-screen arrows for mobile | presentation | `mobile-input` |
| A cue when something happens off-screen — pillar 4 says a loss you never saw is unexplainable | presentation | `offscreen-cues` |
| Minimap | presentation | — |

## Known Not Verified

- **Whether edge-scroll is pleasant or maddening.** Feel, in a window, with hands. The deadzone is
  24 px and a cursor outside the window is ignored, but that is a guess at a comfortable value.
- Whether pan speed and zoom rate feel right. Both are single constants at the top of `CameraRig`.
- Behaviour at 64×64 *with a wave running*. The big board was verified geometrically, not in play.
