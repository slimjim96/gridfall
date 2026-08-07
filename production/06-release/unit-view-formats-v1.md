# Unit View Formats — v1

**Slug:** `unit-view-formats` · **Status:** done · **Verified at trace:** unchanged

Ships the two `IUnitView` implementations
[ADR-0004](../../engine-systems/decisions/ADR-0004-view-asset-abstraction.md) specified and never
got. The ADR chose "one interface, both implementations" as insurance against an unknown asset
format; only the placeholder existed, so the insurance had been decided on and not bought.

## What Shipped

### Two views behind the existing interface

- **`SpriteUnitView`** — a camera-facing quad animated from horizontal sprite strips, one strip per
  clip, frame count inferred from the strip's shape.
- **`MeshUnitView`** — a glTF model loaded at runtime, animated by its own `AnimationPlayer`.
- **`UnitViewFactory`** — content id → whichever view the art supports, falling back to the
  placeholder. The whole of ADR-0004's decision in one function.

### A folder is a unit

`presentation/units/<content-id>/`, discovered at runtime. Drop a folder in and that unit stops being
a placeholder: no case to add, no registration, no Godot import step. The folder's **contents** pick
the format — a `.glb` gives you the mesh view, clip strips give you the sprite view, neither keeps the
placeholder.

Deliberately the same shape as the tile system, so there is one convention to learn rather than two.
ADR-0004 said "asset format becomes a per-entity data field"; a folder named for the content id
achieves the same thing — format is data, not architecture — while touching neither `content-data`
nor Core.

### Three things the view refuses to trust

- A generated `.glb` arrives with whatever material the generator chose, so `MeshUnitView`
  **normalises it** to the flat-matte art direction (roughness 1, metallic 0, specular off). One
  glossy metallic return would otherwise put the only specular highlight in the game on a tower.
- Tinting **duplicates** each surface's material rather than overriding it, so a damaged tower darkens
  instead of losing its texture, and tinting one tower does not tint every other of the same type.
- Level and damage cues use the **identical curve** in all three views. A player learns the cue once;
  it must not depend on which format a unit happens to ship in.

## The property this was really about

**Both formats occlude.** A creep walking behind either tower is hidden by it —
`presentation/docs/unit-formats-baseline.png` shows one creep cut in half by the sprite and another
half-hidden by the mesh, in a single frame.

For the mesh that is free; it is ordinary opaque geometry, which is what `iso-grid.md` chose 3D for.
For the sprite it hangs on one line: the material is **alpha-scissored, never alpha-blended**. Godot
writes no depth for a blended surface, and a surface that writes no depth hides nothing. A soft
anti-aliased sprite edge is therefore a *functional* defect, not a cosmetic one — which is why
`tower-frost-spire.md` asks for a hard alpha edge in the prompt rather than hoping for one.

The second subtlety: the sprite's pivot is `height / (2·cos(pitch))`, not `height / 2`. The art
already carries the camera's foreshortening, so the quad is a full billboard and maps 1:1 to the
screen — but its bottom edge then has to be placed in *screen* terms, and at 30° pitch the naive
value sinks every unit 15% into the board.

## Two defects, both found by looking at a frame

**The mesh rendered as a hollow funnel.** Inverted winding — glTF front faces are counter-clockwise
seen from *outside*, and the obvious vertex order winds clockwise from there, so Godot culled every
side and the top cap and left the inside of the far wall on screen. Invisible in glTF viewers, which
draw double-sided.

The fix was one line. The useful part is that the generator now **checks every triangle's winding
against its own vertex normal and refuses to write the file** — and the check was verified by
reintroducing the bug, not by trusting it.

**Fixtures would have silently replaced three committed baselines.** The first version put the test
assets in the production folder; the default shot seed builds arrow towers, so a fixture arrow tower
rewrites `board-baseline`, `sapper-baseline` and `repair-baseline` with throwaway art — in a slice
that was not meant to change how the game looks at all. Caught by checking the baseline instead of
assuming it.

> **Verification art has to be opt-in, or it stops being verification and becomes an art decision
> nobody made.**

Fixtures now live in `presentation/units-fixtures/`, loaded only via `--units`.

## Player-Facing Change

**None, and that is checked rather than claimed.** `presentation/units/` ships empty, so every unit
still draws its placeholder and `board-baseline.png` is byte-identical at
`18a4cfb97a0a6065dc621d5916ca2925`.

## Follow-Ups Not Done

| Item | Workspace | Slug |
|---|---|---|
| Run the frost spire bake-off and record the answer in ADR-0004 | presentation | `ludo-tile-prompts` / ADR update |
| Delete the losing half of the pipeline and every prompt set, once the answer lands | presentation | — |
| `IUnitView` still lives in the `Placeholders` namespace, which is now wrong — it is the general view contract | presentation | `unit-view-namespace` |
| Sprite `move` / `hit` / `death` and mesh clips beyond `fire` | presentation | with the first real asset |
| Export preset — `presentation/` is outside `res://`, so packing final art is still unproven | production | `release-export` |

## Known Not Verified

- **No real Ludo.ai output exists in either format.** Everything here was verified against fixtures
  built for the purpose. That is the point — the bake-off now has somewhere to land — but it means
  "works with generated art" is untested by definition.
- Whether either format looks *good*. The fixtures are ugly deliberately.
- Behaviour at peak wave density with real assets; the capture is two towers on one board.
