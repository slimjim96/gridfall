# ADR-0004 — Put One View Interface Behind Both Sprite and Mesh Assets

**Status:** accepted · **implemented 2026-08-07**
**Date:** 2026-08-06 · **Raised by:** asset pipeline planning

> **Both implementations now exist** (`unit-view-formats`): `SpriteUnitView` and
> `MeshUnitView`, selected by folder convention under `presentation/units/`. Until then only
> the placeholder had been built, so the insurance this ADR bought had been decided on and
> not actually bought.
>
> **The format question is still open.** The bake-off that closes it is
> `presentation/prompts/tower-frost-spire.md`; when it returns an answer, record it here and
> delete the losing half of the pipeline and of every prompt set.

## Context

All gameplay art in Gridfall is currently a **placeholder**: procedural C# geometry, minimal detail,
built to make the game testable rather than to look like anything. Final assets will be produced with
**Ludo.ai** and tweaked in an image editor.

What Ludo.ai will hand back is not yet settled. It may be 2D sprite sheets rendered at the game's iso
angles, or 3D models as `.glb`. The same question is open on the Scrap Escape project and has not been
resolved there either.

The renderer is Godot 3D with an orthographic camera ([`docs/iso-grid.md`](../../docs/iso-grid.md)), so
both are viable: a mesh is a mesh, and a sprite is a textured quad that faces the camera. But gameplay
code — spawning a creep, showing a tower, playing a death animation — must not have to know which.

## Options

### A. Commit to sprites now

Pick 2D, build the billboard path only, and write every Ludo.ai prompt as sprite-sheet art. Simplest
possible view layer: one quad type, one shader, trivially cheap.

If Ludo.ai turns out to produce good `.glb` output, the whole view layer and every prompt written so
far need reworking — and 3D assets would let the camera zoom and the lighting be consistent for free.

### B. Commit to meshes now

Pick 3D. Real lighting, free depth, no per-angle art. Matches how Scrap Escape's procedural geometry
already works.

If Ludo.ai's strength turns out to be 2D art — which is the more common case for these tools — the
prompts produce assets that cannot be used without a modeller in the loop.

### C. One interface, both implementations

Define `IUnitView` with the small set of operations gameplay actually needs. Ship
`SpriteUnitView` (billboarded quad, sprite-sheet animation) and `MeshUnitView` (`.glb`, `AnimationPlayer`)
behind it. The placeholder factory is a third implementation. Asset format becomes a per-entity data
field, not an architectural commitment.

Cost: one interface, one factory, two implementations to keep working, and prompts written in both
forms until the question is settled.

## Decision

Chose **C**.

Deciding factor: **the answer is unknown and the cost of being wrong is asymmetric.** The abstraction is
a small interface and a factory — perhaps a day. Choosing wrong costs a rewrite of the view layer plus
every prompt written before the discovery. When one option is cheap insurance against an expensive,
genuinely uncertain outcome, buy the insurance.

The secondary benefit decided nothing but is worth stating: the placeholder path is now just a third
implementation, so "placeholder" stops being a temporary hack and becomes a supported mode we can keep
using for new content indefinitely.

## Consequences

### Good
- Ludo.ai's output format stops being a blocking question. Try it, see what comes back, plug it in.
- Placeholders and finals coexist. A half-arted build is a normal state, not a broken one.
- Asset format is per-entity: the eight towers can be meshes while a new creep is still a placeholder.
- Prompts are written in both forms, so no prompt work is wasted either way.
- Mixed-format art is possible on purpose — sprite creeps against mesh terrain is a legitimate style.

### Bad
- Two view implementations to maintain, and a real risk that only one is exercised in practice.
- The interface is a lowest common denominator: sprite-only tricks (per-frame pivot offsets) and
  mesh-only tricks (skeletal attachment points) both need explicit escape hatches.
- Animation semantics differ — sprite frames versus clips — so the interface must express "play the
  death animation" without knowing what that means. Duration comes from the asset, not the caller.
- Prompt sets are written twice until the format question is closed. Deliberate cost.

### Forecloses
- Nothing structural. When the format is settled, the unused implementation is deleted and the
  interface either stays as a thin seam or gets inlined. This ADR is expected to be superseded, and
  that is a success condition rather than a failure.
