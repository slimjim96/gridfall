# Frost Spire — Asset Prompts

**Content id:** `frost-spire` · **Kind:** tower
**Placeholder:** `Shapes.TaperedSpire(0.26f, 1.60f)` — tapered hexagonal prism, 1.60 cells tall,
0.52 cells wide, narrowing to a quarter-width top. Cool blue, `Palette.TowerFrost` = `6fc7d9`.
**Status:** written — **this is the format bake-off set.** Not yet run through Ludo.ai.

## Identity

A support tower that does no damage. It chills the ground around it and slows everything that walks
through. The feeling is **cold, still, and slightly unsettling** — it does not shoot, it does not
track, it simply makes the air near it hostile.

**Silhouette it must keep:** tall, narrow, tapering to a point. Taller and thinner than every other
tower in the roster.
**Must not be confusable with:** the arrow tower (also tall, but square-shouldered with a visible
head) or the beacon (also narrow, but with a wide base).

---

## What this set is for

This asset was picked to **close the question ADR-0004 deliberately left open**: does Gridfall's final
art come back as 2D sprite sheets or 3D `.glb`?

Frost spire is the right test case, not an arbitrary one. It is the **tallest and thinnest** thing in
the roster, so it is the asset that most needs to occlude creeps walking behind it, and the one whose
silhouette is least forgiving of a projection that is slightly off. If a format works here it works
for the squat towers; the reverse is not true.

### The fairness rule

**`Subject`, `Form` and `Palette` are word-for-word identical in the two blocks below**, deliberately.
So are the three asset-specific negatives — *snow on the ground, icicles, crystals radiating outward*.

Everything that differs is format, not design: the `View` / `Output` / `Topology` / `Textures` lines,
plus the boilerplate half of `Negative`, which the guide specifies per form and which cannot be shared
— a mesh cannot have a watermark and a sprite cannot have a ground plane.

Diff those three lines before running. If they have drifted apart you are no longer comparing two
formats, you are comparing two different towers, and the result decides nothing.

> The previous version of this file had already drifted: the mesh `Palette` had lost the *"as if lit
> from within"* clause that the sprite carried, and the mesh negatives were missing two of the three
> anti-spike clauses. Small enough to miss by eye, big enough to hand you a mesh with a duller accent
> than the sprite and a conclusion about the wrong thing.

### Run them in one sitting

Same day, same Ludo.ai settings, back to back. A week apart with a model update in between is not a
comparison either.

---

## Sprite form

> **Style anchor — Gridfall**
> Isometric tower defense game asset. Clean geometric forms, low detail, strong readable silhouette.
> Flat matte surfaces with soft ambient occlusion; no glossy highlights, no rim lighting, no text.
> Restrained palette, low saturation for terrain and structures, saturated accent only on the element
> that carries the unit's identity. Single unit centered on a transparent background, no ground plane,
> no shadow baked in, no scenery. Neutral studio lighting from the upper left.
>
> Subject: a frost tower that slows nearby enemies — a still, silent spire of layered ice, no weapon,
> no moving parts.
> Form: tall tapered hexagonal spire, roughly three times as tall as it is wide, narrowing to a blunt
> point about a quarter of the base width. Three or four stacked ice segments with visible horizontal
> seams. Slightly wider hexagonal footing, no pedestal.
> Palette: desaturated pale blue-grey body; one saturated cyan accent in the upper segment, as if lit
> from within.
> View: isometric, camera 45° yaw and 30° pitch above horizontal, 2:1 dimetric projection
> Output: single sprite, 256×256, transparent background, unit fills 80% of frame height.
> Hard alpha edge — every pixel fully opaque or fully transparent, no soft anti-aliased fringe.
> Negative: text, watermark, ground shadow, multiple objects, motion blur, glossy reflections,
> snow on the ground, icicles, crystals radiating outward

**Why "hard alpha edge" is in there, and why it is not optional.** A sprite in this engine is a quad
in a 3D scene. To hide a creep walking behind it, it has to write to the depth buffer, and a quad
only writes depth if it is alpha-*scissored* rather than alpha-*blended*. A soft anti-aliased fringe
forces blending, blending disables depth write, and the tower silently stops occluding anything —
which is the exact property this bake-off exists to protect. Ask for the hard edge up front; it
cannot be added afterwards without re-cutting every frame.

The last three negatives are specific to this asset: generators reliably add radiating crystal spikes
to anything described as ice, and spikes destroy the narrow silhouette this tower is built around.

---

## Mesh form

> **Style anchor — Gridfall**
> Isometric tower defense game asset. Clean geometric forms, low detail, strong readable silhouette.
> Flat matte surfaces with soft ambient occlusion; no glossy highlights, no rim lighting, no text.
> Restrained palette, low saturation for terrain and structures, saturated accent only on the element
> that carries the unit's identity. Single unit centered on a transparent background, no ground plane,
> no shadow baked in, no scenery. Neutral studio lighting from the upper left.
>
> Subject: a frost tower that slows nearby enemies — a still, silent spire of layered ice, no weapon,
> no moving parts.
> Form: tall tapered hexagonal spire, roughly three times as tall as it is wide, narrowing to a blunt
> point about a quarter of the base width. Three or four stacked ice segments with visible horizontal
> seams. Slightly wider hexagonal footing, no pedestal.
> Palette: desaturated pale blue-grey body; one saturated cyan accent in the upper segment, as if lit
> from within.
> Output: 3D model, glTF (.glb), Y-up, origin at the base center, real-world scale where 1 unit =
> 1 grid cell. The model is 1.60 units tall and 0.52 units wide.
> Topology: low poly, under 1500 triangles, single material, no subdivision
> Textures: 1024×1024 albedo only, no normal map, no roughness map
> Negative: ground plane, base pedestal, text, high-frequency surface detail, motion blur, glossy
> reflections, snow on the ground, icicles, crystals radiating outward

**Ludo.ai export settings for this run** — `.glb`, max triangles at the **1k floor**, adaptive
decimation pushed hard, **Color** (not PBR), **1024** resolution. Each falls out of a number in the
engine rather than a preference; the arithmetic is in
[`ludo-prompt-guide.md`](../docs/ludo-prompt-guide.md) §Ludo.ai export settings. In short: a tower is
37×100 px at maximum zoom, peak density is order 150 units on screen, and `MeshUnitView` overwrites
the PBR channels on import anyway.

**A static model is a valid return.** `PlayClip` ignores clips the asset does not have, so a `.glb`
with no animation passes every check below except the animation ones — which are not part of the
bake-off. Do not spend iterations chasing animated export before the format is settled.

---

## Acceptance checks

Run these on both returns. **Measure; do not eyeball.** Every number here comes from
[`docs/iso-grid.md`](../../docs/iso-grid.md) and the placeholder it replaces.

### Both formats

| # | Check | Fail looks like |
|---|---|---|
| 1 | Taller and thinner than the arrow tower **in greyscale** | Two towers you cannot tell apart with the colour off |
| 2 | Tapers to roughly a quarter of its base width | A blunt column, or a needle |
| 3 | No radiating spikes | The known failure for this prompt — regenerate, do not edit |
| 4 | No pedestal, no baked ground shadow | A plinth that breaks the tile grid; a shadow that slides when it moves |
| 5 | Exactly one saturated element, in the upper segment | Cyan everywhere, so nothing reads as the accent |

### Sprite only

| # | Check | How |
|---|---|---|
| S1 | **Projection is genuinely 45° / 30°, 2:1** | The footing's ellipse must be **twice as wide as it is tall**. Anything else and it will never composite with the terrain — this is the single most likely way the sprite form fails. |
| S2 | **On-screen proportion is 2.67 : 1, not 3 : 1** | The *object* is 3.1× taller than wide; the −30° pitch foreshortens vertical to `cos(30°) = 0.866`, so `1.60 × 0.866 ÷ 0.52 = 2.67`. A sprite measuring 3:1 is too tall and will sit wrong beside the terrain. **Do not "correct" it to 3:1.** |
| S3 | Height ≈ one cell diamond's full width | Convenient sanity check: `1.60 × 0.866 = 1.386` against a cell's on-screen width of `1.414`. They should be within a few percent. |
| S4 | Hard alpha edge | Zoom to 800% on the outline. Any partially-transparent fringe pixels = it will not occlude. |

### Mesh only

| # | Check | How |
|---|---|---|
| M1 | Origin at base centre, Y-up | Import and drop at the world origin. If it sinks or floats, every asset needs a per-asset offset — the thing that always gets lost. |
| M2 | Scale is 1 unit = 1 cell | Should arrive **1.60 tall**. Arbitrary scale is the second most common import problem. |
| M3 | Under 1500 triangles, single material | Check the import report, not the vibe. Exported at the 1k floor there is no reason to be near this. |
| M4 | Albedo only, 1024² | Export as **Color**, not PBR: `MeshUnitView` overwrites roughness/metallic/specular on import, so PBR maps are generated and discarded. Any AO must be baked into the albedo. |
| M5 | Geometry is clean, not a blob | Photogrammetry-style mush is unusable at this poly budget regardless of how it looks in the preview |

---

## The decision

Feeds [ADR-0004](../../engine-systems/decisions/ADR-0004-view-asset-abstraction.md), which is
currently "one interface, both implementations, question open".

| Result | What it means |
|---|---|
| Mesh passes M1–M5 | **Choose mesh.** Occlusion, lighting and zoom come free; no per-angle art if the camera ever gains the 90° yaw snapping `iso-grid.md` leaves open. |
| Mesh fails, sprite passes S1–S4 | **Choose sprite.** Buildable, and the reason ADR-0004 bought the insurance. Budget for hard-edged alpha and accept the camera can never rotate. |
| Both pass | Generate the **`fire` clip** in both and decide there — animation is where the formats diverge most (frame registration vs. root motion), and it is the cost you pay on every asset thereafter. |
| Neither passes | Not a format answer. The prompt or the anchor is wrong; iterate before concluding anything about Ludo.ai. |

Whichever wins: **record it in ADR-0004 and delete the losing half of every prompt set in one pass**,
per `ludo-prompt-guide.md`. Carrying both forms is a real cost taken deliberately, and it should stop
the day the answer lands.

> **Do not generate the whole roster off this result.** One asset closes the format question. It does
> not prove the anchor holds across eight towers — that is a separate pass.

---

## Animation clips

**Do not generate these for the bake-off.** Two base assets settle the format question; clips double
the work for no extra signal unless both base forms pass, in which case `fire` is the tiebreaker.

### `idle`

> **Style anchor — Gridfall**
> [anchor block, verbatim]
>
> Base asset: frost-spire
> Clip: idle
> Description: the cyan accent in the upper segment pulses slowly, like a slow breath. Nothing else
> moves — the spire is perfectly still. The stillness is the character.
> Beats: brighten 15 frames, dim 15 frames
> Loop: yes
> Timing: 30 frames at 30 fps = 1000 ms
> Output (sprite): horizontal strip, 30 frames, 256×256 each, transparent, consistent registration
> Output (mesh): single glTF animation clip named "idle", 30 keyframes, no root motion
> Negative: camera movement, background change, scale change between frames, rotation, swaying

### `fire`

> **Style anchor — Gridfall**
> [anchor block, verbatim]
>
> Base asset: frost-spire
> Clip: fire
> Description: the tower applies its slow. The cyan accent flares sharply, a single ring of frost
> pulses outward from the footing, and it settles back. The spire itself never moves or turns.
> Beats: anticipation 3 frames (accent gathers), action 2 frames (flare and ring), settle 4 frames
> Loop: no
> Timing: 9 frames at 30 fps = 300 ms
> Output (sprite): horizontal strip, 9 frames, 256×256 each, transparent, consistent registration
> Output (mesh): single glTF animation clip named "fire", 9 keyframes, no root motion
> Negative: camera movement, background change, scale change between frames, projectile leaving frame,
> the tower rotating toward a target

"The tower never turns" is stated in every clip on purpose. It is the tower's whole character, and it
is the first thing a generator will take away.

---

## Notes for the human

- **The engine cannot display either return yet.** `PlaceholderUnitView` is currently the only
  implementation of `IUnitView` — ADR-0004 specified `SpriteUnitView` and `MeshUnitView` but neither
  was built, because there was nothing to put in them. Every check above is done **on the returned
  file**, in an image editor or a glTF viewer. Seeing it in Gridfall is a follow-up slice
  (`unit-view-formats`), and it is worth doing *after* the format is chosen, not before — building
  both to compare them is the work the comparison exists to avoid.
- The pulse timing is deliberate: 300 ms is 9 ticks, and the tower's cooldown is 1.2 s = 36 ticks, so
  the fire clip finishes well before the next one starts. Do not stretch it.
- If the generated asset comes back with radiating spikes, that is the known failure for this prompt.
  Regenerate rather than editing them out — spikes usually mean the rest of the proportions drifted.
- Check the silhouette against the arrow tower in greyscale before accepting it. That check is the
  whole reason the placeholder was shaped the way it was.
- **Change one thing per iteration**, and log it below. A prompt that took five tries is worth
  writing down properly, and the fifth version is the one that goes in the file.

---

## Iteration Log

| Date | Change | Result |
|---|---|---|
| — | Not yet run | Format bake-off set, sharpened 2026-08-07 |
