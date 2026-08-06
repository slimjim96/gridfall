# [Asset Name] — Asset Prompts

**Content id:** `[id]` · **Kind:** tower / creep / terrain / projectile / HUD
**Placeholder:** `[what the placeholder is, so the generated asset can be compared to it]`
**Status:** written / generated / tweaked / in-game

## Identity

One or two sentences: what this thing is, what it does in the game, and the one feeling it should
give. This is the section that keeps the prompts honest — everything below should serve it.

**Silhouette it must keep:** [the shape language from the placeholder]
**Must not be confusable with:** [the two nearest assets]

---

## Sprite form

> **Style anchor — Gridfall**
> Isometric tower defense game asset. Clean geometric forms, low detail, strong readable silhouette.
> Flat matte surfaces with soft ambient occlusion; no glossy highlights, no rim lighting, no text.
> Restrained palette, low saturation for terrain and structures, saturated accent only on the element
> that carries the unit's identity. Single unit centered on a transparent background, no ground plane,
> no shadow baked in, no scenery. Neutral studio lighting from the upper left.
>
> Subject:
> Form:
> Palette:
> View: isometric, camera 45° yaw and 30° pitch above horizontal, 2:1 dimetric projection
> Output: single sprite, 256×256, transparent background, unit fills 80% of frame height
> Negative: text, watermark, ground shadow, multiple objects, motion blur, glossy reflections

---

## Mesh form

> **Style anchor — Gridfall**
> [same block, verbatim]
>
> Subject: [same sentence as the sprite form]
> Form: [same silhouette description]
> Palette:
> Output: 3D model, glTF (.glb), Y-up, origin at the base center, 1 unit = 1 grid cell
> Topology: low poly, under 1500 triangles, single material, no subdivision
> Textures: 512×512 albedo only, no normal map, no roughness map
> Negative: ground plane, base pedestal, text, high-frequency surface detail

---

## Animation clips

### `[clip name]`

> **Style anchor — Gridfall**
> [same block, verbatim]
>
> Base asset: [id]
> Clip: [name]
> Description:
> Beats:
> Loop: yes / no
> Timing: [N] frames at 30 fps = [N × 33] ms
> Output (sprite): horizontal strip, [N] frames, 256×256 each, transparent, consistent registration
> Output (mesh): single glTF animation clip named "[name]", [N] keyframes, no root motion
> Negative: camera movement, background change, scale change between frames

---

## Iteration Log

| Date | Change | Result |
|---|---|---|
| | | |

Record what needed fixing by hand. A fix that recurs across the set belongs in the prompt or the anchor.
