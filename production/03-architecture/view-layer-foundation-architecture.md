# View Layer Foundation — Architecture

**Slug:** `view-layer-foundation` · **Status:** done
**Supersedes for implementation:** the requirements file

## Shape

```
godot/                       Godot 4.6.3 project, net8.0, Godot.NET.Sdk
├── Main.tscn                one node, one script -- the scene is built in code
├── View/
│   ├── IsoGrid.cs           THE projection contract, in code. Constants live here once.
│   ├── SimDriver.cs         fixed-timestep accumulator; owns the Sim; exposes alpha
│   ├── WorldRenderer.cs     terrain mesh from MapDef, rebuilt only when the grid changes
│   └── UnitRenderer.cs      creeps/towers/projectiles; interpolation; event responses
├── Placeholders/
│   ├── IUnitView.cs         ADR-0004: placeholders, sprites, meshes all sit behind this
│   ├── PlaceholderUnitView.cs
│   ├── PlaceholderFactory.cs   content id -> a view
│   ├── Shapes.cs            procedural primitives
│   └── Palette.cs           the slots from art-direction.md
├── Hud/Hud.cs               gold, lives, wave
└── GameplayScene.cs         wires the above together in _Ready
```

## New project: `Gridfall.Io`

Core may not touch the filesystem — there is a test enforcing it — but both `Gridfall.Verify` and the
Godot project need to read `content-data/`. That loader existed only inside Verify.

`Gridfall.Io` (net8.0, references Core) now owns it. Verify and Godot both reference it, so there is
one loader rather than two that can disagree. This is the tooling scope's "reuse, never reimplement"
applied before the duplication happened rather than after.

## The boundary, concretely

| Direction | Mechanism |
|---|---|
| View → Sim | `sim.Enqueue(new BuildCommand(...))`. Applied in phase 1 of the next tick. Never inline. |
| Sim → View, continuous | Read `SimState` arrays each frame, interpolate by `alpha` |
| Sim → View, discrete | Walk `sim.Events` after each `Tick()` |

`alpha` is a `float` and lives entirely view-side. `Fix32.ToFloat()` is called only in `UnitRenderer`,
at the moment of placing a node.

## The accumulator

```csharp
_accumulator += delta;
while (_accumulator >= TickSeconds) { _sim.Tick(); DrainEvents(); _accumulator -= TickSeconds; }
float alpha = (float)(_accumulator / TickSeconds);
```

Events are drained **inside** the loop, per tick. Draining after the loop would lose a tick's events
whenever the accumulator catches up on two ticks in one frame — which is exactly the bug engine guide
05 warns about, and it only shows up under stutter.

A catch-up cap (`MaxCatchUpTicks = 5`) stops a long stall from spiralling. Hitting the cap drops game
time rather than freezing the frame; that is a view-side decision and the sim cannot tell.

## Interpolation

A creep's position comes from `cell + progress × heading`. Interpolating that directly breaks at a
cell boundary, where progress wraps 0.9 → 0.1 and the creep snaps backwards.

So the renderer keeps the **previous tick's world position per entity id** and lerps between that and
the current one. Cell transitions are then continuous, because both endpoints are already in world
space. Ids that vanished are dropped when their view is released.

## Rendering the terrain

One `ArrayMesh` for the whole board, rebuilt only when `PathSystem.Version` changes. Per-cell quads at
`y = 0`, coloured by `CellKind`, plus a slightly raised quad for cells a tower occupies.

Not one node per cell: 64×64 would be 4,096 nodes for a static board.

## What the view may read

`SimState` is public on `Sim`, so the view *can* reach anything. The discipline is that
`UnitRenderer` touches only positional and identity fields, and the HUD only reads `Gold`, `Lives`,
`WaveIndex`. Nothing writes.

`SimStateView` — the read-only façade engine guide 05 describes — is **not yet implemented**. That is a
real gap this slice does not close; see the verify report.

## Verify Plan

1. `dotnet build` on the whole solution, 0/0, including the Godot project against real GodotSharp.
2. `godot --headless --quit` — scene and script wiring load without error.
3. **Capture a frame and look at it.** Godot renders on this box (`DISPLAY=:10.0`), so the renderer can
   be run and screenshotted rather than only compiled.
4. Determinism with the renderer attached: same seed, same hashes as a headless run.
5. Purity: no `Godot` in Core; no assignment to `sim.State` anywhere in the view.

## Note on visual verification

Until now every presentation criterion was NOT-VERIFIABLE-BY-AGENT. That is no longer strictly true: a
frame can be captured and inspected. What that covers is **whether it draws, where things are, and
whether silhouettes read** — a smoke check.

It does not cover motion quality, feel, or aesthetic judgment, and it should not be used to claim
those. The category still exists; it is just smaller.
