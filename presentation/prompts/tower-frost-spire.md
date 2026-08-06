# Frost Spire — Asset Prompts

**Content id:** `frost-spire` · **Kind:** tower
**Placeholder:** tapered hexagonal prism, cool-blue vertical gradient, ~1.6 cells tall, 0.5 cells wide
**Status:** written — example set, not yet run through Ludo.ai

## Identity

A support tower that does no damage. It chills the ground around it and slows everything that walks
through. The feeling is **cold, still, and slightly unsettling** — it does not shoot, it does not
track, it simply makes the air near it hostile.

**Silhouette it must keep:** tall, narrow, tapering to a point. Taller and thinner than every other
tower in the roster.
**Must not be confusable with:** the arrow tower (also tall, but square-shouldered with a visible head)
or the beacon (also narrow, but with a wide base).

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
> point. Three or four stacked ice segments with visible horizontal seams. Slightly wider hexagonal
> footing, no pedestal.
> Palette: desaturated pale blue-grey body; one saturated cyan accent in the upper segment, as if lit
> from within.
> View: isometric, camera 45° yaw and 30° pitch above horizontal, 2:1 dimetric projection
> Output: single sprite, 256×256, transparent background, unit fills 80% of frame height
> Negative: text, watermark, ground shadow, multiple objects, motion blur, glossy reflections,
> snow on the ground, icicles, crystals radiating outward

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
> point. Three or four stacked ice segments with visible horizontal seams. Slightly wider hexagonal
> footing, no pedestal.
> Palette: desaturated pale blue-grey body; one saturated cyan accent in the upper segment.
> Output: 3D model, glTF (.glb), Y-up, origin at the base center, 1 unit = 1 grid cell.
> The model is 1.6 units tall and 0.5 units wide.
> Topology: low poly, under 1500 triangles, single material, no subdivision
> Textures: 512×512 albedo only, no normal map, no roughness map
> Negative: ground plane, base pedestal, text, high-frequency surface detail, radiating crystals

---

## Animation clips

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

"The tower never turns" is stated in every clip on purpose. It is the tower's whole character, and it is
the first thing a generator will take away.

---

## Notes for the human

- The pulse timing is deliberate: 300 ms is 9 ticks, and the tower's cooldown is 1.2 s = 36 ticks, so
  the fire clip finishes well before the next one starts. Do not stretch it.
- If the generated asset comes back with radiating spikes, that is the known failure for this prompt.
  Regenerate rather than editing them out — spikes usually mean the rest of the proportions drifted too.
- Check the silhouette against the arrow tower in greyscale before accepting it. That check is the whole
  reason the placeholder was shaped the way it was.

---

## Iteration Log

| Date | Change | Result |
|---|---|---|
| — | Not yet run | This is an example set written to establish the format |
