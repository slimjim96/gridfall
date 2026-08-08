# Camera Pan & Zoom — Verification

**Slug:** `camera-pan-zoom` · **Status:** done · **Verdict:** PASS

## The display outage, and what it cost

Mid-slice both X sockets died — `:0` and `:10` unreachable, `XDG_RUNTIME_DIR` unset, Godot falling
through X11 → Wayland → "Unable to create DisplayServer". On this VM the display belongs to the RDP
session (`board-editor-guide.md` §Troubleshooting).

This report was first filed with four criteria **unverified** rather than passed. The session was
reconnected and all four have now been checked; what follows is the completed run. Recorded rather
than tidied away, because "held at review until it could actually be verified" is the outcome that
mattered.

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

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | Pan by middle-drag, arrows/WASD, edge-scroll | PASS | 64×64 capture shows the camera deep inside a board far larger than the viewport |
| 2 | Clamped to bounds + `PanMarginCells` | PASS by construction | `IsoGrid.ClampFocus`, applied on every move |
| 3 | `PanMarginCells` is read, not merely declared | PASS | `CameraContractTests` fails the build if it goes dead again |
| 4 | Recentre key | PASS | `Home` |
| 5 | Zoom finer than 1.5/notch inside 10–30 | PASS | Multiplicative 1.06, ~19 notches; capture shows `orthoSize=21.15` after 6 notches from 30 |
| 6 | **Picking correct after pan and zoom** | **PASS — 4096/4096** | See below |
| 7 | Camera never changes pitch or yaw | PASS | `TheCameraRigNeverTouchesPitchOrYaw` |
| 8 | **Baselines byte-identical** | **PASS** | `board`, `unit-formats`, `editor-tiles` all unchanged |
| 9 | Determinism unchanged | PASS | `replay` 30/30; capture hashes `b9c3bc7c95e6f726` / `e8468a5c83dd11d6` |
| 10 | `iso-grid.md` and the editor spec become true | PASS | Both updated |
| 11 | A 64×64 board is usable end to end | PASS | Panned, zoomed, validated, rendered |

### Criterion 6, measured rather than eyeballed

Picking follows the camera "for free" through `ProjectRayOrigin` — exactly the kind of claim nobody
checks. So it was checked exhaustively instead of by clicking: on a **64×64** board, after panning
through the rig's real public API (a synthetic middle-drag of 220×−150 px) and six zoom notches,
every cell centre was projected to screen with `UnprojectPosition` and fed back through
`IsoGrid.TryPick`.

```
pick round-trip after pan+zoom: 64x64 ok=4096 bad=0 orthoSize=21.15
```

**4096 of 4096.** The harness was temporary and has been reverted; the tree is clean.

### Criterion 8, the one that could have broken everything

Six committed captures depend on the board being framed identically every run. All byte-identical
after the rig landed, which is what proves `Locked` is honoured rather than merely present.

## The bug the outage exposed anyway

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

| What | Why |
|---|---|
| Whether edge-scroll is pleasant or maddening in a window | Feel. Needs hands, not a frame. |
| `MapTargets` at 64×64 | The bands were tuned on 20×9 and 10×10. Large boards are now *reachable*, which makes this newly relevant — see the requirements' Downstream table. |
| Touch / mobile arrows | Out of scope by the stated assumption; the input layer is shaped so it can be added. |

## A prediction that came true immediately

The requirements' Downstream table warned that `MapTargets` bands were tuned on 20×9 and 10×10 boards
and would be wrong at scale. The 64×64 test board reported **89% buildable** (target 35–55%) and an
**unmazed path of 63** (target 18–30) — two warnings on a board that is not obviously bad, just large.

Large boards are now reachable, so those bands need to become size-relative. Not this slice.
Follow-up: `map-targets-at-scale`.

## Branch Resolution

None. Verdict is PASS; the slice was held at `review` through the outage and released once the
capture-dependent criteria could actually be run.
