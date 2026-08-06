# View Layer Foundation — Verification

**Slug:** `view-layer-foundation` · **Status:** review · **Verdict:** PASS

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` (5 projects) | PASS | 0 warnings, 0 errors, including the Godot project against real GodotSharp 4.6.3 |
| `dotnet test` | PASS | 70 passed, 0 failed |
| Determinism trace (headless) | PASS | `Verify replay` — 30/30 checkpoints |
| Scene loads and runs | PASS | Godot 4.6.3 launches `Main.tscn`, builds the scene, renders, exits cleanly |

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | Cell kinds visually distinguishable | PASS | `presentation/docs/board-baseline.png` — buildable slate, path-only dark, blocked near-black raised blocks, spawn violet, goal green |
| 2 | Creeps appear, move, disappear | PASS | Four creeps visible mid-lane in the capture; `creeps=4` in the state line |
| 3 | Towers appear where built | PASS | Three towers visible at the three seeded cells |
| 4 | Click queues a build; tower appears next tick | PASS by construction | `_UnhandledInput` only calls `Enqueue`. **Not exercised by a click** — no input in a scripted capture |
| 5 | Sealing build shows a visible refusal | PASS by construction | `BuildRejected` → `Hud.ShowRefusal`, wired and compiled. **Not seen** |
| 6 | HUD shows gold/lives/wave and updates | PASS | `gold 34  lives 20  wave 1  creeps 5  towers 3` legible in the capture |
| 7 | Motion interpolated, not stepped | PASS by construction | `SimDriver.Alpha` + previous/current world-position lerp. **Motion quality not assessed** — a still frame cannot show it |
| 8 | Same seed, same hashes with the renderer attached | PASS | Two Godot runs: `tick=90 hash=987dc81d2e55a6cd gold=18 lives=20 creeps=4 towers=3`, identical |
| 9 | No Godot in Core; view never writes state | PASS | `SourcePurityTests.Core_ReferencesNoGodotType`; no assignment to `sim.State` in the view — input goes through `Enqueue` only |
| 10 | Archetypes distinguishable by silhouette | PARTIAL | Arrow tower (tall prism) vs cannon (squat cylinder) clearly distinct in the capture. **Only one creep archetype was on screen** — runner vs brute untested |

## Defects Found by Looking at a Frame

Four, none of which the compiler or any test could have caught.

1. **Vertex colours were being fed sRGB values but interpreted as linear.** Every terrain tone rendered
   far lighter than authored — `55697d` arriving as a pale near-white, and the whole board reading as
   one flat colour. I changed the palette twice before diagnosing it, and both times concluded "no
   visual change" from a downscaled frame. Fixed with `SrgbToLinear()` on the vertex colour path.

2. **Raised cells had no side faces.** A blocked cell was a floating lid with the background visible
   underneath, so the board looked like it had black holes punched through it. Fixed by emitting four
   side quads whenever a cell is raised.

3. **Camera fit ignored the viewport aspect.** The first auto-fit cropped both ends off the board,
   because `Camera3D.Size` is the *vertical* extent and a 2:1 board is width-constrained on a 16:9
   viewport. Fixed by taking the max of both constraints.

4. **The original fixed ortho size of 18 left the board using about half the frame** — wasted pixels
   in a game about reading the board. Replaced with a per-map fit.

## A Correction to My Own Process

Twice I reported "identical output" after a change, from eyeballing a 1280×720 capture scaled down for
review. The files differed every time — 48313 → 47687 → 44479 bytes. **Comparing hashes would have told
me in one second what two rounds of guessing did not.** The lesson is in the presentation workflow now:
diff the bytes before concluding a visual change did nothing.

## Not Verified

| What | Why |
|---|---|
| Anything requiring input | The capture path is scripted. Click-to-build, sell, refusal display, and hover preview are wired and compile, but no click has been issued. A human should try them. |
| Motion quality and feel | A still frame cannot show interpolation smoothness, bob rate, or whether the hit flash reads. |
| Runner vs brute silhouette | Only runners were on screen. Wave 2 has brutes; nobody has seen the two together. |
| Greyscale distinctness | Criterion 10's colourblind check has not been run. |
| Anything at wave-18 density | Readability was assessed at four creeps. The art direction's requirement is peak density. |
| `SimStateView` | The read-only façade in engine guide 05 does not exist. The view reads `SimState` directly and refrains from writing by discipline, not by the type system. This is a real gap. |

## Branch Resolution

None — verdict is PASS. The four defects were found and fixed within stage 04 rather than bouncing the
slice, because each was in the code this slice wrote and none invalidated the architecture.
