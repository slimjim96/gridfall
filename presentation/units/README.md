# Unit Assets

Final tower and creep art. **Empty on purpose** — nothing here yet, so every unit still draws its
procedural placeholder.

```
presentation/units/
└── <content-id>/            ← must match a tower or enemy id in content-data/
    ├── model.glb            ← mesh format
    │   …or…
    ├── idle.png             ← sprite format: one horizontal strip per clip
    ├── fire.webp            ← .png or .webp, mixed freely
    └── unit.json            ← { "frameCells": 1.57 }
```

Drop a folder in and that unit stops being a placeholder. There is no case to add and nothing to
register — same convention as [`../tiles/`](../tiles/README.md), and implemented in
[`godot/View/Units/UnitAssets.cs`](../../godot/View/Units/UnitAssets.cs).

## Which format you get

The folder's contents decide, not a config field:

| Folder holds | View | Notes |
|---|---|---|
| a `.glb` | `MeshUnitView` | Wins if both are present, and says so in the console |
| `idle.png` / `idle.webp`, … | `SpriteUnitView` | Strips named for the clip |
| neither | placeholder | Unchanged behaviour |

**`.png` and `.webp` are equally first-class**, and a folder may mix them —
`SpriteUnitView` loads through `Image.LoadFromFile`, which reads both. If one clip
somehow has both, the `.png` wins and the console says so. Until 2026-08-08 the
loader globbed `*.png` only, so a `.webp` was not *rejected*, it was **invisible**:
the folder reported "no .glb and no standard clip strips" exactly as though it
were empty.

Clip names are fixed — `idle`, `move`, `fire`, `hit`, `death`. A strip named anything else loads but
can never be triggered, so it is reported and skipped. `idle` and `move` loop; the rest are one-shot
and hand back to `idle`.

**The folder name must match a content id.** `arrow_tower` or `arrowtower` matches nothing, resolves
to nothing, and the game quietly keeps drawing the placeholder — no error, because nothing is wrong
as far as the loader is concerned. `UnitAssetTests` fails the build instead.

## Sprites: two things that are not obvious

**`frameCells` in `unit.json` is the one number an image cannot carry.** The image says how many pixels
it is; it never says how big the thing is meant to be. It is the world size of one square frame, in
cells. There is a default so a bare folder works, but a sprite folder relying on it will render at
the wrong size — `UnitAssetTests` warns about that too.

**The art must have a hard alpha edge** — every pixel fully opaque or fully transparent. A sprite is
a quad in a 3D scene, and it only hides what is behind it if it writes depth, which requires
alpha-*scissor* rather than alpha-*blend*. One soft anti-aliased fringe forces blending, blending
disables depth write, and the tower stops occluding creeps entirely. It cannot be fixed after the
frames are cut, which is why the prompts ask for it up front.

Frame count is inferred from the strip's shape (width ÷ height), so frames must be square and there
is no metadata to drift out of sync with the image.

## Meshes: what the view does not trust

A generated `.glb` arrives with whatever the generator felt like. `MeshUnitView` normalises the
material to the flat-matte art direction — roughness 1, metallic 0, specular off — rather than hoping,
because one glossy metallic return would put the only specular highlight in the game on a tower.

It also duplicates each material before tinting, so a damaged tower darkens instead of losing its
texture, and so tinting one tower does not tint every other tower of the same type.

Scale and origin come from the asset: **1 unit = 1 cell, origin at the base centre.** Nothing corrects
for a model that ignores that.

## Fixtures

`../units-fixtures/` holds throwaway assets — one sprite, one mesh — that exist to verify the two view
implementations. They are **not** loaded by default, deliberately: the default shot seed builds arrow
towers, so a fixture arrow tower would silently invalidate three committed visual baselines.

```bash
./run-game.sh --units presentation/units-fixtures --shot-seed formats --shot /tmp/x.png --shot-after 40
```

## The format question is still open

[ADR-0004](../../engine-systems/decisions/ADR-0004-view-asset-abstraction.md) keeps both viable
because Ludo.ai's usable output is not yet known. Both implementations now exist and are verified, so
the bake-off in [`../prompts/tower-frost-spire.md`](../prompts/tower-frost-spire.md) has somewhere to
land. When the answer arrives, record it in the ADR and delete the losing half — of the prompts *and*
of this pipeline.
