# View Layer Foundation — v1

**Slug:** `view-layer-foundation` · **Status:** done
**Verified at sim state:** `tick=90 hash=987dc81d2e55a6cd` (renderer attached, identical across runs)

## What Shipped

You can see the game. A Godot 4.6.3 project renders the board in 3D orthographic on the contract
angles, creeps walk it, towers stand on it, and the HUD reads gold, lives, and wave.

- `IsoGrid` — the projection contract from `docs/iso-grid.md` in code, once. Grid↔world, camera
  configuration, ray-to-ground picking, and a per-map ortho fit.
- `IUnitView` + `PlaceholderUnitView` + `PlaceholderFactory` — the ADR-0004 seam. Placeholders are a
  third implementation alongside sprites and meshes, so a Ludo.ai asset drops in by changing one
  `case`.
- `SimDriver` — fixed-timestep accumulator with a catch-up cap. Events are drained per tick inside the
  loop, not after it.
- `WorldRenderer` — one `ArrayMesh` for the board, rebuilt only when `PathSystem.Version` changes.
- `UnitRenderer` — interpolates between previous and current world positions, and drives hit flash and
  death collapse off the event stream.
- `Gridfall.Io` — the content loader extracted from Verify so the harness and the game read
  `content-data/` through one code path.
- **Shot mode** — `--shot <png> --shot-after N` freezes the sim after a fixed number of deterministic
  steps and renders with a fixed frame delta, producing a byte-reproducible capture.

## Player-Facing Change

The game has a face. Placing a tower changes where creeps walk, and now you can watch it happen.

Everything on screen is a placeholder under the hour budget: an arrow tower is a tall thin prism, a
cannon is a squat cylinder, a runner is a low sphere. The silhouettes are the only part meant to
survive into the final art.

## New Tuning Knobs

| Knob | Owner | Default set? |
|---|---|---|
| Terrain palette (5 slots) | presentation | Yes — tuned against a captured frame |
| Camera fit margin (`1.28`) | presentation | Yes — fits crossroads with room for the HUD |
| Idle bob amplitude / speed, hit flash, death collapse | presentation | Placeholder values from the standard |

## Follow-Ups Not Done

| Item | Workspace | Suggested slug |
|---|---|---|
| `SimStateView` — the read-only façade. The view currently refrains from writing by discipline, not by the type system | engine-systems | `sim-state-view` |
| Exercise input for real: click-to-build, sell, refusal, hover | presentation | `input-playtest` |
| Runner vs brute silhouette check, and a greyscale pass | presentation | `silhouette-audit` |
| Readability at wave-18 density, not four creeps | presentation | `density-readability` |
| Camera pan and zoom — the contract defines them, nothing implements them | presentation | `camera-controls` |
| Route overlay so mazing is visible while dragging | presentation | `route-overlay` |
| Visual regression: diff captures against the baseline in CI | tooling | `visual-baseline-check` |

## Docs Corrected to Match Reality

**Agents can see the game now.** Godot renders on this box against `DISPLAY=:10.0`, and shot mode makes
captures reproducible. `presentation/CONTEXT.md` and the iso-presentation workflow have been updated:
"compiles; not visually verified" is no longer an acceptable stopping point for anything a still frame
would show.

What a frame still cannot show — motion quality, feel, aesthetic judgment — stays
NOT-VERIFIABLE-BY-AGENT. The category shrank; it did not disappear.

Also added to the workflow, from a mistake made twice in this slice: **`md5sum` two captures before
concluding a visual change did nothing.**

## Known Not Verified

- No input has been exercised. Click-to-build, sell, the refusal message, and the hover preview are
  wired and compile, but nobody has clicked.
- Motion, interpolation smoothness, and the hit flash are unassessed — a still frame cannot show them.
- Only one creep archetype has been on screen.
- Readability was judged at four creeps, not at peak density.
