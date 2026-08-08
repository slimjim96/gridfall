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

**Height means range (2026-08-08).** Across the tower roster, a taller silhouette reaches further —
one rule, learnable in a single game. It replaced an inconsistent vocabulary where the *cheap starter*
was the tallest thing on the board and also had the longest reach, which read as "this covers
everything" and led to under-building. Cost tracks it too: taller, dearer, further.

| Shape | Suggests | Used for |
|---|---|---|
| Short thin prism | Cheap, close in | The starter tower |
| Tall wide cylinder | Bulk, and the longest reach | Expensive damage towers |
| Tapered hex prism | Cold, still | Support / slow towers |
| Low sphere | Speed | Fast creeps |
| Broad box | Toughness | Armored creeps |
| Stacked cones | Swarm | Groups that split |
| Inverted wedge, point down | A drill, a demolisher | Creeps that attack structures |

Silhouette first, then proportion, then color. Ornament never — that is what the final asset is for.

**Build the wedge tall and narrow.** The camera looks down, so a wide low cone shows almost nothing but
its top face and reads as a flat plate. The first sapper was 0.22 × 0.40 and became a red tile; 0.17 ×
0.58 reads as the drill it is meant to be. Any point-down shape has the same trap.

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
| Hit flash | `CreepDamaged`, `TowerDamaged` | Whole-body tint to white for 80 ms |
| Death collapse | `CreepDied`, `TowerDestroyed` | Scale to zero over 150 ms |

Towers otherwise animate only a muzzle flash quad on `TowerFired`. That is enough to read the game, and
no placeholder needs more.

## Persistent state is not a clip

A clip is an *event*. Anything that stays true — a tower's level, a tower's remaining health — must be
a property on `IUnitView`, because a clip replays on reload and does not survive the view being
recreated. Two exist:

| State | Channel | Why that channel |
|---|---|---|
| `SetLevel` | Taller and brighter | Height survives greyscale; level is a *gain* so it reads brighter |
| `SetHealthFraction` | Darker and redder | Level already owns height and brightness-up, so damage cannot use silhouette without contradicting it |

Damage uses **two** channels on purpose. Darkening is the one that survives greyscale; the red is a
redundant second signal, not the only one. A tower at 28% health is unmistakable beside a healthy one
in `sapper-baseline.png`.

Every player-visible state needs a visible representation, and destruction makes this load-bearing: a
tower that vanishes with no prior warning is exactly the unexplainable loss pillar 4 forbids.

## Naming and location

```
godot/Placeholders/            ← only things that ARE placeholders
├── PlaceholderFactory.cs        maps content id → a placeholder view
├── PlaceholderUnitView.cs       the shared bob / flash / collapse motions
└── Shapes.cs                    the shared primitive builders

godot/View/                    ← the view layer's shared vocabulary
├── Palette.cs                   the palette slots from art-direction.md
├── TerrainTheme.cs              the colour ramps
└── Units/IUnitView.cs           the contract all three views implement
```

`Palette` and `IUnitView` used to live under `Placeholders/` and do not belong there: the palette is
the art direction, and the interface is the general view contract that *final* assets implement too.
A namespace whose name asserts "these are placeholders" has to be true of everything in it, or it
stops carrying information.

One `case` per content id in the factory — and a final asset no longer needs that case edited at all.
Dropping a folder into `presentation/units/<content-id>/` makes `UnitViewFactory` stop reaching the
placeholder branch on its own, so the placeholder is superseded rather than deleted, and comes back
if the folder moves away.

## The exit

A placeholder is replaced when:

1. Its prompt set exists in `presentation/prompts/` ([the prompt guide](ludo-prompt-guide.md))
2. The human has run it through Ludo.ai and tweaked the output
3. The asset is in the project and `IUnitView` returns the real implementation
4. Silhouette distinctness has been re-checked **on the final asset** — Ludo.ai output can converge
   toward a house style and quietly make two units look alike

Step 4 is the one people skip. The placeholder silhouettes were designed to be distinct; there is no
guarantee the generated art preserves that, and it is a human check.
