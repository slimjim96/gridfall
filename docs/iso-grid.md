# The Isometric Grid Contract

Stable reference. Load on demand. Owned by `presentation`, depended on by everyone.

This is the single definition of how a simulation coordinate becomes something on screen. If you need
to change it, change it **here first** — then tell every layer that reads it.

## Simulation space

The sim knows only integers.

- A cell is `(x, y)`, both `int`, origin at the north corner, `x` increasing south-east, `y` south-west.
- Maps are up to 64×64. Cell state is a byte: buildable, path-only, blocked, spawn, goal.
- Sub-cell positions (a creep walking between cells) are `Fix32` in cell units, never floats,
  never pixels. `(3, 4) + (0.5, 0)` is a creep halfway along the +x edge of cell (3,4).

**The sim has no concept of screen space.** Nothing in `Gridfall.Core` mentions pixels, cameras, or Z.

## Presentation space

Gridfall renders in **Godot 3D with an orthographic camera**, not in 2D sprites. The reason is depth
sorting: in 3D the z-buffer handles overlapping towers, creeps, and terrain for free, which is the
single largest source of bugs in 2D isometric renderers.

| Constant | Value | Where |
|---|---|---|
| Cell size | `1.0` world unit | `IsoGrid.CellSize` |
| Camera projection | Orthographic | `Camera3D.projection` |
| Camera yaw | `45°` | `IsoGrid.CameraYaw` |
| Camera pitch | `-30°` | `IsoGrid.CameraPitch` |
| Ortho size (default zoom) | `18.0` | `IsoGrid.DefaultOrthoSize` |

Yaw 45° + pitch 30° gives a **2:1 dimetric** silhouette on screen — the classic isometric look, and the
one that keeps tile diamonds twice as wide as they are tall. True isometric (pitch 35.264°) is *not*
used: 2:1 lands on clean pixel ratios and reads better at small tile sizes.

## The mapping

```csharp
// grid → world (the ground plane is XZ; Y is height)
Vector3 GridToWorld(int x, int y, float height = 0f)
    => new Vector3(x * CellSize, height, y * CellSize);

// world → grid, for picking
Vector2I WorldToGrid(Vector3 w)
    => new Vector2I(Mathf.FloorToInt(w.X / CellSize), Mathf.FloorToInt(w.Z / CellSize));
```

Sub-cell sim positions convert by `(float)fix` **only at the boundary**, inside the view layer.

## Picking

Screen → grid is a ray cast, not an inverse projection formula:

1. `camera.ProjectRayOrigin(mousePos)` and `camera.ProjectRayNormal(mousePos)`
2. Intersect with the ground plane `y = 0` (`Plane.Up` at 0) — analytic, no physics query
3. `WorldToGrid` the hit point
4. Clamp to the map bounds; out of bounds is a valid answer ("no cell"), not an error

Never pick with a `PhysicsDirectSpaceState3D` query per frame. The plane intersection is exact and free.

## Depth and layering

3D handles depth. The two places you still think about it:

- **Ground decals** (build previews, range circles) sit at `y = 0.01` to avoid z-fighting.
- **UI-in-world** (health bars, damage numbers) render unshaded on a separate layer and always face
  the camera.

If Gridfall is ever ported to a 2D sprite renderer, the depth-sort key is `(x + y)`, ascending, with
ties broken by entity id. Do not use world Y.

## Camera behavior

- Pan is clamped to the map bounds plus a two-cell margin. **Not built yet** — `PanMarginCells`
  is declared and read by nothing; see `production/01-requirements/camera-pan-zoom-requirements.md`.
  Until it is, any board where width + height exceeds ~59 is cropped with no way to reach the rest.
- Zoom changes `Camera3D.Size` between `10.0` and `30.0`. It never changes the pitch or yaw — rotating
  the camera off the contract angles breaks every art asset's implied lighting direction.
- Rotation, if it is ever added, snaps to the four 90° yaws. No free rotation.

## Readability budget

At peak wave density the design calls for, a player must be able to distinguish creep archetypes at
default zoom. That means silhouette first, color second: **no two archetypes share a silhouette**.
This is a `presentation` acceptance criterion, and it is checked by a human, not by an agent.
