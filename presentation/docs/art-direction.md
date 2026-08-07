# Art Direction

Load when making a visual judgment call. The projection math is not here — that is `docs/iso-grid.md`.
What a placeholder may and may not be is in [`placeholder-standard.md`](placeholder-standard.md); how
final assets get generated is in [`ludo-prompt-guide.md`](ludo-prompt-guide.md). This file is the
aesthetic those two answer to, and it applies to both.

## The look

Clean geometric isometry. Readable solids, strong silhouettes, restrained palette. Gridfall reads as a
**board you are reasoning about**, not a battlefield you are watching. When a choice is between
spectacle and legibility, legibility wins — that is pillar 2, and it is not negotiable at wave 18.

Geometry is **procedural C#** wherever it can be: terrain, tiles, walls, tower bodies, projectiles.
This keeps the repo free of binary churn and makes every visual a tunable constant.

**Everything on screen today is a placeholder**, and that is the intended state for a while. Final
assets are generated with Ludo.ai and tweaked by hand; placeholders exist so the game can be played and
balanced before any of that happens. Both are legitimate, both coexist, and a half-arted build is
normal rather than broken.

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

## Visual baselines

`board-baseline.png` and `editor-baseline.png` are byte-reproducible captures used as visual regression
references. Refresh with:

```bash
godot-mono --path godot -- --shot presentation/docs/board-baseline.png --shot-after 40
```

Both are current as of 2026-08-06, re-captured after the `tower-upgrades` seed change and verified
byte-reproducible across two runs. `board-baseline.png` deliberately contains a level-2 tower beside a
level-1 one, so the upgrade cue has something to be compared against.

> The display on this VM belongs to the **RDP session**. When you disconnect, `DISPLAY=:10` keeps
> pointing at an X server that no longer exists and captures stop working — the launchers say so in
> one line rather than leaving you to read Godot's error.

## What NOT to do

- Don't polish placeholders. They have an hour budget; see `placeholder-standard.md`.
- Don't add a binary asset without noting its source and license here.
- Don't rotate the camera off the contract angles for a shot. Every asset's implied lighting assumes
  them.
- Don't encode state in a particle effect alone — particles are transient, state is not.
- Don't introduce a second red.
