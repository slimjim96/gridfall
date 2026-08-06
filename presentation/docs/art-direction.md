# Art Direction

Load when making a visual judgment call. The projection math is not here — that is `docs/iso-grid.md`.

## The look

Clean geometric isometry. Readable solids, strong silhouettes, restrained palette. Gridfall reads as a
**board you are reasoning about**, not a battlefield you are watching. When a choice is between
spectacle and legibility, legibility wins — that is pillar 2, and it is not negotiable at wave 18.

Geometry is **procedural C#** wherever it can be: terrain, tiles, walls, tower bodies, projectiles.
Authored assets are reserved for things that carry identity and cannot be generated convincingly.
This keeps the repo free of binary churn and makes every visual a tunable constant.

## Palette

| Role | Use | Constraint |
|---|---|---|
| Terrain | Low-saturation, cool | Never competes with a unit for attention |
| Buildable | Terrain + a subtle lift | Must be distinguishable at default zoom without a legend |
| Path-only | Terrain, darker | Reads as "not yours" |
| Player towers | Warm, saturated | The only warm saturated things that belong to the player |
| Creeps | Cool-to-hot by threat | Hue carries threat tier; a player learns it in one run |
| Danger / rejection | One red, used nowhere else | Reserved. If red means three things, it means nothing |

Gradients are built in code (`Gradient` + `GradientTexture2D`), not shipped as images.

## Silhouette rules

1. **No two creep archetypes share a silhouette.** Not "similar with different colors" — different
   shapes. This is a hard rule; it fails review, not just taste.
2. Towers differ by base shape first, by ornament second.
3. At default zoom a unit occupies at least 24 screen pixels of distinct outline.
4. Colorblind check: every distinction that matters survives greyscale. If it does not, the distinction
   is carried by shape or motion instead.

## Motion and feel

- **Motion means state change.** Idle animation is minimal; a moving thing is a thing that just did
  something. Ambient motion competes with the signal.
- Interpolation between ticks is view-side and 33 ms wide — never let it imply a state the sim has not
  reached.
- Hit feedback is immediate and small. Screen shake is reserved for a leak, and nothing else.
- Every rejected action gets a visible refusal within one frame. A refused build that shows nothing
  reads as an unresponsive game.

## Audio hooks

Audio is driven off the `SimEvent` stream, same as VFX. One event, one cue, no polling. Cues are
ducked by priority: leak > wave start > build rejected > tower fire. Tower fire at wave 18 must not
drown out a leak.

## What NOT to do

- Don't add a binary asset without noting its source and license here.
- Don't rotate the camera off the contract angles for a shot. Every asset's implied lighting assumes
  them.
- Don't encode state in a particle effect alone — particles are transient, state is not.
- Don't introduce a second red.
