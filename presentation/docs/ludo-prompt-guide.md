# Ludo.ai Prompt Guide

How to write the prompts that produce Gridfall's final assets. The prompts are the durable artifact —
placeholders get deleted, generated images get regenerated, but a good prompt set is reusable and
produces a *consistent* result months apart.

Agents write prompts. **The human runs them and tweaks the output in an image editor.** Nothing in this
pipeline is automated end to end, and the prompt should be written knowing a person will iterate on it.

## Write both forms, until the format question closes

Ludo.ai's usable output format for this project is not yet established: 2D sprite sheets or 3D `.glb`.
[ADR-0004](../../engine-systems/decisions/ADR-0004-view-asset-abstraction.md) keeps both viable in
code, and this guide keeps both viable in prompts. **Every prompt set contains a sprite form and a mesh
form** until we know. It is a real cost, taken deliberately.

When the answer lands, delete the unused half of every prompt set in one pass and update this guide.

## The style anchor

Consistency across an asset set matters more than any single asset's quality. A roster of eight towers
that share a look beats eight individually better towers that do not.

Every prompt opens with the same anchor block, copied verbatim, never paraphrased:

> **Style anchor — Gridfall**
> Isometric game asset for a grid-based strategy game. Clean geometric forms, low detail, strong readable silhouette.
> Flat matte surfaces with soft ambient occlusion; no glossy highlights, no rim lighting, no text.
> Restrained palette, low saturation for terrain and structures, saturated accent only on the element
> that carries the unit's identity. Single unit centered on a transparent background, no ground plane,
> no shadow baked in, no scenery. Neutral studio lighting from the upper left.

Paraphrasing the anchor is the single most common way a set drifts. Copy it.

## Sprite form

```
[STYLE ANCHOR]

Subject: <one sentence — what it is and what it is for>
Form: <the silhouette, in shape language: "tall tapered hexagonal spire, narrow at the top">
Palette: <slot from art-direction.md, with the accent named>
View: isometric, camera 45° yaw and 30° pitch above horizontal, 2:1 dimetric projection
Output: single sprite, 256×256, transparent background, unit fills 80% of frame height
Negative: text, watermark, ground shadow, multiple objects, motion blur, glossy reflections
```

Notes that matter:

- **State the projection numerically.** "Isometric" alone gets you 30°, 35.264°, or a 2D fake, and they
  do not composite together. Gridfall is yaw 45° / pitch 30°, 2:1 dimetric
  ([`docs/iso-grid.md`](../../docs/iso-grid.md)).
- **One angle only.** Do not ask for a rotation sheet in the same prompt — quality drops. Generate the
  hero angle, and only if the unit visibly turns generate the other three as separate runs from the
  same prompt with the yaw changed.
- **No baked shadow.** The renderer casts its own. A baked shadow makes the unit look glued down and
  breaks when it moves.
- **80% of frame height** keeps the whole set scaled consistently. Without it, every asset comes back
  at a different apparent size and someone spends an afternoon in an image editor fixing it.

## Mesh form

```
[STYLE ANCHOR]

Subject: <same sentence as the sprite form>
Form: <same silhouette description>
Palette: <same slot and accent>
Output: 3D model, glTF (.glb), Y-up, origin at the base center, real-world scale where 1 unit = 1 grid cell
Topology: low poly, under 1500 triangles, single material, no subdivision
Textures: 1024×1024 albedo only, no normal map, no roughness map
Negative: ground plane, base pedestal, text, high-frequency surface detail
```

### Ludo.ai export settings

Not preferences. Each one falls out of a number in this engine.

| Setting | Use | Why |
|---|---|---|
| **File type** | **`.glb`** | `UnitAssets` globs `*.glb` and `MeshUnitView` loads it with `GltfDocument.AppendFromFile`. A `.gltf` with sidecar textures is not found and would not carry its maps. |
| **Max triangles** | **the 1k floor** | A tower is **25×68 px** on screen at default zoom and **37×100 px** at maximum zoom-in. 1,000 triangles is already more triangles than the silhouette has pixels. |
| **Adaptive decimation** | **push it hard**, then check | Interior detail is invisible at 37 px wide; silhouette is the whole readability budget. Verify in greyscale against a neighbouring tower — the check that already governs placeholders. |
| **PBR / Color / None** | **Color** | `MeshUnitView` overwrites roughness, metallic and specular on import to hold the flat-matte art direction. PBR maps would be generated and then discarded. |
| **Resolution** | **1024** | Already 10× linear oversampling at maximum zoom. 2048 is 20× — bytes and import time for texels no screen ever shows. |

**Why the triangle budget is tight and not fussiness.** Peak density is real: `crossroads` spawns
**147 creeps at wave 12** and has **76 buildable cells**, so the worst case is order 150 units on
screen at once. At the 1k floor that is 0.15 M triangles per frame; at the 200k ceiling it is **30 M**,
which no amount of GPU makes sensible — and the dev machine here renders on `llvmpipe`, in software,
with no GPU at all.

**Colour, not PBR, has a consequence worth knowing.** With albedo only, any ambient occlusion has to
be *baked into the albedo*. That is correct here — units are lit by the scene's one directional light
(terrain is unshaded, units are not), so baked AO plus real-time directional is exactly how the
placeholders already read.

**A static `.glb` is fine to ship.** `MeshUnitView.PlayClip` ignores a clip the asset does not have,
per `IUnitView`. So a model with no animation works today and simply does not animate; clips can be
added later with no code change. Do not hold up the roster waiting for animated export.

- **Origin at the base center, Y-up.** Gridfall places units by grid cell; an origin anywhere else means
  every asset needs a per-asset offset, which is exactly the kind of thing that gets lost.
- **1 unit = 1 cell.** Assets arriving at arbitrary scale is the second most common import problem.
- **Albedo only.** The art direction is flat matte; normal maps fight it and cost import time.
- **"No base pedestal"** matters — generators love adding a little plinth, and it breaks the tile grid.

## Animation prompts

Written per clip, not per asset, and referencing the base asset by name.

```
[STYLE ANCHOR]

Base asset: frost-spire (see tower-frost-spire.md)
Clip: fire
Description: <what happens, in beats>
Beats: anticipation 3 frames, action 2 frames, settle 4 frames
Loop: no
Timing: 9 frames at 30 fps = 300 ms
Output (sprite): horizontal strip, 9 frames, 256×256 each, transparent, consistent registration
Output (mesh): single glTF animation clip named "fire", 9 keyframes, no root motion
Negative: camera movement, background change, scale change between frames
```

Rules with teeth:

- **Timing in ticks, not vibes.** The sim runs at 30 Hz; a clip's duration should be a whole number of
  frames at 30 fps so it lands on tick boundaries. 300 ms = 9 frames = 9 ticks.
- **"Consistent registration"** on sprite strips. Frames that drift relative to each other produce a
  unit that jitters, and it is very hard to fix after the fact.
- **"No root motion"** on mesh clips. Position comes from the simulation, always. An animation that
  moves the model desynchronizes the visual from where the creep actually is.
- **Name the clip exactly** as the view layer expects: `idle`, `move`, `fire`, `hit`, `death`.

### The standard clip set

| Clip | Who | Loop | Typical | Triggered by |
|---|---|---|---|---|
| `idle` | Everything | yes | 30 frames / 1 s | default state |
| `move` | Creeps | yes | 15 frames | movement, phase 4 |
| `fire` | Towers | no | 9 frames | `TowerFired` |
| `hit` | Creeps | no | 3 frames | `CreepDamaged` |
| `death` | Creeps | no | 12 frames | `CreepDied` |

Do not invent clips outside this set without adding them to `IUnitView` first — an animation nothing
can trigger is an asset nobody will ever see.

## Iterating

The human runs the prompt and gets something 70% right. That is the expected outcome.

- **Change one thing per iteration**, same as a balance pass. Prompts drift unpredictably when you
  change three clauses at once.
- **Record what worked** in the prompt file, in the `## Iteration Log` section. A prompt that took five
  tries is a prompt worth writing down properly, and the fifth version is the one that goes in the file.
- **Tweaking in an image editor is expected**, not a failure. Note in the log what needed manual fixing
  — if the same fix recurs across the set, it belongs in the prompt or in the style anchor.
- **Regenerate the whole set** when the anchor changes. Mixing anchor versions within a set is how a
  roster stops looking like a roster.

## Where prompts live

```
presentation/prompts/
├── README.md                    index + the current style anchor
├── _template.md                 copy this
├── tower-frost-spire.md         one file per asset, sprite + mesh + all clips
└── creep-runner.md
```

One file per asset, containing every form and every clip for that asset. Not one file per prompt — when
you regenerate, you want the whole asset in front of you.
