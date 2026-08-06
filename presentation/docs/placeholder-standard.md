# Placeholder Asset Standard

Every visual in Gridfall starts as a placeholder and stays one until Ludo.ai output replaces it. This
document is the standard placeholders must meet — and, just as importantly, the ceiling they must not
exceed.

**A placeholder exists so the game can be played, tested, and balanced today.** It is not early art. It
is not a rough draft of the final asset. Time spent making a placeholder look good is time spent on
something that will be deleted.

## The budget

| Asset | Time budget | Hard ceiling |
|---|---|---|
| A tower | 45 min | 1 hour |
| A creep | 45 min | 1 hour |
| A terrain tile type | 20 min | 30 min |
| A projectile / effect | 15 min | 30 min |
| A HUD element | 30 min | 1 hour |

Over the ceiling means stop. Either the placeholder is trying to be art, or the thing genuinely needs a
real asset now and should go through the prompt pipeline instead.

## The five requirements

A placeholder must:

1. **Be procedural C#.** Built from Godot primitives and code — `BoxMesh`, `CylinderMesh`, `PrismMesh`,
   extruded polygons, gradient textures generated at runtime. No binary files, no imported art, no
   external dependency. It diffs in git and it is tunable by changing a number.
2. **Have a distinct silhouette.** This is the one aesthetic requirement, and it is not negotiable — see
   below.
3. **Take its palette slot** from `art-direction.md`. Player towers warm, creeps cool-to-hot by threat,
   the one reserved red for danger. Placeholder colors are the real colors.
4. **Read at default zoom.** If you cannot tell what it is at `Camera3D.Size = 18`, it fails, however
   good it looks close up.
5. **Sit behind `IUnitView`.** Placeholders are a third implementation alongside sprites and meshes
   ([ADR-0004](../../engine-systems/decisions/ADR-0004-view-asset-abstraction.md)), which is what lets
   a final asset drop in without touching gameplay code.

## The silhouette rule

**No two creep archetypes may share a silhouette. No two towers may share a silhouette.** In greyscale,
at default zoom, at wave-18 density, each one must be identifiable by shape alone.

This survives into the final art — it is pillar 2 — so getting it right in the placeholder is not
wasted work. It is the *only* placeholder work that carries forward, which is exactly why it is the one
thing worth caring about.

Practical vocabulary, cheap to build and easy to tell apart:

| Shape | Suggests | Used for |
|---|---|---|
| Tall thin prism | Precision, reach | Long-range towers |
| Squat wide cylinder | Bulk, area | Splash towers |
| Tapered hex prism | Cold, still | Support / slow towers |
| Low sphere | Speed | Fast creeps |
| Broad box | Toughness | Armored creeps |
| Stacked cones | Swarm | Groups that split |

Silhouette first, then proportion, then color. Ornament never — that is what the final asset is for.

## What a placeholder must NOT have

- Textures beyond a flat color or a two-stop gradient
- Detail geometry — no greebling, no bevels, no accessories
- Bespoke animation beyond the shared idle bob and hit flash
- Anything that took longer than the budget above
- Anything a reviewer might mistake for a design decision about the final look

That last one matters more than it sounds. A placeholder that looks *considered* invites feedback on
its art, and every minute of that conversation is wasted on both sides.

## Shared motion

Placeholders do not get individual animation. They share three behaviors, implemented once:

| Motion | Trigger | What |
|---|---|---|
| Idle bob | Always, for creeps | 2% vertical sine, phase offset by entity id |
| Hit flash | `CreepDamaged` | Whole-body tint to white for 80 ms |
| Death collapse | `CreepDied` | Scale to zero over 150 ms |

Towers do not animate at all except a muzzle flash quad on `TowerFired`. That is enough to read the
game, and no placeholder needs more.

## Naming and location

```
godot/Placeholders/
├── PlaceholderFactory.cs        maps content id → a placeholder view
├── Shapes.cs                    the shared primitive builders
└── Palette.cs                   the palette slots from art-direction.md
```

One `case` per content id in the factory. When a final asset arrives, the case changes to return a
`SpriteUnitView` or `MeshUnitView` and the placeholder code is deleted in the same commit — not left
behind "in case".

## The exit

A placeholder is replaced when:

1. Its prompt set exists in `presentation/prompts/` ([the prompt guide](ludo-prompt-guide.md))
2. The human has run it through Ludo.ai and tweaked the output
3. The asset is in the project and `IUnitView` returns the real implementation
4. Silhouette distinctness has been re-checked **on the final asset** — Ludo.ai output can converge
   toward a house style and quietly make two units look alike

Step 4 is the one people skip. The placeholder silhouettes were designed to be distinct; there is no
guarantee the generated art preserves that, and it is a human check.
