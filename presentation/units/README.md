# Unit Assets

Final station and visitor art. **`arrow-station` is real; everything else is still a placeholder.**

The first shipped asset is a single-frame WebP idle sprite, `arrow-station/idle.webp` — 662×662, one
frame, `frameCells` 1.353. It is the reason five committed baselines were re-recorded on 2026-08-08.
It arrived 768×768 with 53px of empty space under the base, which is why it hovered until it was run
through [`fit-sprite.sh`](#cropping-do-not-trim-to-all-edges).

Its alpha is **not** hard-edged — 0.71% of pixels sit at partial alpha, a one-pixel anti-aliased
fringe. That is survivable *here* because `SpriteUnitView` hardcodes `AlphaScissor` at 0.5 rather than
choosing a mode per asset, so depth write is never lost and the fringe is simply clipped. It still
costs a slightly ragged silhouette, and it is worth fixing at the source. See the sprite notes below.

```
presentation/units/
└── <content-id>/            ← must match a station or visitor id in content-data/
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

**The folder name must match a content id.** `arrow_station` or `arrowstation` matches nothing, resolves
to nothing, and the game quietly keeps drawing the placeholder — no error, because nothing is wrong
as far as the loader is concerned. `UnitAssetTests` fails the build instead.

## Cropping: do not "trim to all edges"

Run the tool instead. It is in-place, so commit first or pass `--dry-run`:

```bash
./fit-sprite.sh presentation/units/arrow-station --dry-run   # reports, writes nothing
./fit-sprite.sh presentation/units/arrow-station             # crops every clip in the folder
```

It prints the factor to multiply `frameCells` by. It deliberately does **not** edit `unit.json` —
keeping the on-screen size is usually right, but it is an art decision and the tool only knows how to
leave it where it was.

### Why the obvious crop is wrong

`SpriteUnitView` makes the quad a **square of side `frameCells`** and lifts it so the frame's **bottom
edge sits on the ground** at the cell centre. Three rules follow:

| Rule | What breaks if you ignore it |
|---|---|
| Frames stay **square** | Frame count is `width / height`. A strip trimmed to content is re-read as a different number of frames — 262×662 is not a tall sprite, it is a zero-frame one. |
| Subject **horizontally centred** | The frame's centre line is the cell centre. Trimming an asymmetric subject to its own bounds walks it off the tile. |
| Base **flush to the bottom edge** | Empty pixels below the base are float: the unit hovers `gap / side × frameCells` cells above the board. The shipped arrow station had 53px of it and hovered 0.11 cells. |

So the manual recipe, if you are doing it in Photoshop rather than with the tool: **trim to the
silhouette, then re-pad to a square canvas, subject centred horizontally, base flush to the bottom.**
Trim-to-all-edges alone gets the first two wrong.

### The one that only shows up in motion

The crop box must be computed **once across every frame of every clip in the folder** — never per
frame, never per file. Per frame pins the subject in place and the animation jitters; per file makes
the unit change size when it fires. `fit-sprite.sh` takes the union across the whole folder for
exactly this reason, which is also why you point it at a **unit directory**, not at a file.

## Sprites: two things that are not obvious

**`frameCells` in `unit.json` is the one number an image cannot carry.** The image says how many pixels
it is; it never says how big the thing is meant to be. It is the world size of one square frame, in
cells. There is a default so a bare folder works, but a sprite folder relying on it will render at
the wrong size — `UnitAssetTests` warns about that too.

**The art should have a hard alpha edge** — every pixel fully opaque or fully transparent. A sprite is
a quad in a 3D scene, and it only hides what is behind it if it writes depth, which requires
alpha-*scissor* rather than alpha-*blend*. The prompts ask for it up front because it cannot be
restored after the frames are cut.

> **Corrected 2026-08-08.** This section used to say a soft fringe "forces blending, blending disables
> depth write, and the station stops occluding visitors entirely". That is what happens in a renderer that
> picks a transparency mode per asset. It is not what happens here: `SpriteUnitView` hardcodes
> `AlphaScissor` at 0.5 and nothing switches it, so **occlusion is never at risk** and a soft fringe is
> merely clipped at the threshold. The shipped `arrow-station` has 0.71% soft pixels and occludes
> correctly. The cost is a ragged edge, not a broken one — still worth asking generators for a hard
> alpha, but it is a quality note, not a correctness one.

Frame count is inferred from the strip's shape (width ÷ height), so frames must be square and there
is no metadata to drift out of sync with the image.

## Meshes: what the view does not trust

A generated `.glb` arrives with whatever the generator felt like. `MeshUnitView` normalises the
material to the flat-matte art direction — roughness 1, metallic 0, specular off — rather than hoping,
because one glossy metallic return would put the only specular highlight in the game on a station.

It also duplicates each material before tinting, so a damaged station darkens instead of losing its
texture, and so tinting one station does not tint every other station of the same type.

Scale and origin come from the asset: **1 unit = 1 cell, origin at the base centre.** Nothing corrects
for a model that ignores that.

## Fixtures

`../units-fixtures/` holds throwaway assets — one sprite, one mesh — that exist to verify the two view
implementations. They are **not** loaded by default, deliberately: the default shot seed builds arrow
stations, so a fixture arrow station would silently invalidate three committed visual baselines.

```bash
./run-game.sh --units presentation/units-fixtures --shot-seed formats --shot /tmp/x.png --shot-after 40
```

## The format question is still open

[ADR-0004](../../engine-systems/decisions/ADR-0004-view-asset-abstraction.md) keeps both viable
because Ludo.ai's usable output is not yet known. Both implementations now exist and are verified, so
the bake-off in [`../prompts/station-frost-spire.md`](../prompts/station-frost-spire.md) has somewhere to
land. When the answer arrives, record it in the ADR and delete the losing half — of the prompts *and*
of this pipeline.
