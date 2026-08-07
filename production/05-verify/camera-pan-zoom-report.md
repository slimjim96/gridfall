# Camera Pan & Zoom — Verification

**Slug:** `camera-pan-zoom` · **Status:** review · **Verdict:** PASS on every gate that can run —
**visual verification BLOCKED, not passed**

## The blocker, stated first

**No display.** Both X sockets died mid-slice — `:0` and `:10` are unreachable, `XDG_RUNTIME_DIR` is
unset, and Godot falls through X11 → Wayland → "Unable to create DisplayServer". On this VM the
display belongs to the RDP session (`board-editor-guide.md` §Troubleshooting), so it needs
reconnecting.

So the four criteria that need a frame are **unverified, not passing**:

- every existing baseline byte-identical
- picking still lands on the right cell after panning and zooming
- a 64×64 board panned end to end
- edge-scroll feel

Those are the criteria most likely to catch a defect in this slice. Treat the verdict accordingly.

## Gates that did run

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` (5 projects) | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **182 passed**, 0 failed (was 179; +3) |
| Determinism trace | PASS | `Verify replay` — 30/30 checkpoints |
| Game scene wiring | PASS | `./run-game.sh --headless --quit`, 0 exceptions |
| Editor scene wiring | PASS | `./run-editor.sh --headless --quit`, 0 exceptions, tiles loaded |

The two headless runs matter more than usual here: `_Ready` is where the rig is constructed,
initialised and reframed, so they exercise the whole camera setup path including the init-order bug
below.

## Criteria

| # | Criterion | Result |
|---|---|---|
| 1 | Pan by middle-drag, arrows/WASD, edge-scroll | Built — **not exercised** |
| 2 | Clamped to bounds + `PanMarginCells` | Built, `IsoGrid.ClampFocus` |
| 3 | `PanMarginCells` is read, not merely declared | **PASS** — `CameraContractTests` fails the build if it goes dead again |
| 4 | Recentre key | Built, `Home` |
| 5 | Zoom finer than 1.5/notch inside 10–30 | Built — multiplicative 1.06, ~19 notches |
| 6 | Picking correct after pan/zoom | **UNVERIFIED** — needs a display |
| 7 | Camera never changes pitch or yaw | **PASS** — `TheCameraRigNeverTouchesPitchOrYaw` |
| 8 | Baselines byte-identical | **UNVERIFIED** — needs a display |
| 9 | Determinism unchanged | **PASS** |
| 10 | `iso-grid.md` and the editor spec become true | **PASS** — both updated |

## The bug the blocker exposed anyway

The editor constructed a `CameraRig` and then went straight to `RebuildEverything`, which calls
`Reframe` — but `Initialise` was never called, so the rig had no camera and no map. It would have
thrown on the first frame.

Found while diagnosing the display failure, not by a test. Fixed by initialising the rig from
`_draft.ToMapDef()` at construction, and the headless editor run now confirms the path.

`Reframe` was also simplified in the same pass: it originally called `ConfigureCamera` and then put
the zoom back, which is a re-frame pretending to be a nudge. It now only updates the map bounds and
re-clamps the existing focus — the zoom and the focus both survive a brush stroke because nothing
touches them.

## Design notes

**Multiplicative zoom.** The old flat 1.5 step is 5% of the range zoomed out and 15% zoomed in, so the
same notch lurched at one end and did nothing at the other. A 1.06 ratio feels identical everywhere.

**Vertical drag divides by `GroundCompression`.** A world step along `ScreenUp` only covers
sin(pitch) = 0.5 of the screen, so without the division a drag lags the cursor at half speed
vertically. This is the same 0.5 `FitOrthoSize` already uses.

**Edge-scroll is off in the editor.** Painting a border wall means holding the cursor at the screen
edge deliberately; a camera that slides away while you do it is worse than no panning. On in the
game, where it is the SimCity/Warcraft behaviour that was asked for.

**Pan speed scales with zoom**, so a keypress crosses the same fraction of what you can see at either
end of the range.

## Not Verified

Everything under "The blocker", plus:

| What | Why |
|---|---|
| Whether edge-scroll is pleasant or maddening in a window | Feel. Needs hands, not a frame. |
| `MapTargets` at 64×64 | The bands were tuned on 20×9 and 10×10. Large boards are now *reachable*, which makes this newly relevant — see the requirements' Downstream table. |
| Touch / mobile arrows | Out of scope by the stated assumption; the input layer is shaped so it can be added. |

## Branch Resolution

Held at `review`. It does not advance to `06-release` until a display is available and the four
capture-dependent criteria are checked.
