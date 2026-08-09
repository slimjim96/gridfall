# Direction — Themed Boards and a Shared Station Pool

> Board/terrain themes only — mountain, ocean, forest and friends. For what the game is *about*,
> start at [theme-direction.md](theme-direction.md).

**Set:** 2026-08-07 · **Owner:** design-lead · **Status:** in progress

The product direction the current run of slices is working toward. This file exists because the shape
was decided in conversation and would otherwise live nowhere — the release notes record what shipped,
not what it is for.

## The idea

Boards are **places**: mountains, ocean, forest, desert, underwater, space. Each has its own ground art,
starting as placeholders and finishing as Ludo.ai output. Stations are themed to the boards they appear
on.

## The decision that shapes everything downstream

"Stations designed for each board theme" splits three ways, and they are very different amounts of work.

| Option | What it means | Cost |
|---|---|---|
| Cosmetic only | One roster everywhere; themes change art | Zero balance cost. An ocean board plays exactly like a desert one |
| **Shared pool, themed subsets** | **One roster of ~8, balanced against each other. Each theme offers a different 5, with overlap** | **One balance problem. Theme becomes a deck choice** |
| Per-theme rosters | Each theme gets its own 5+ mechanically distinct stations | ~30 stations, six separate balance problems |

**Chosen: shared pool, themed subsets.**

> **Amended 2026-08-09 — the pool is ~10, and the primary axis is difficulty, not theme.** Asked for
> directly: up to ten options on the last advanced boards, three on an early one. Availability becomes a
> **progression curve**; two late boards of different themes can still offer different tens, so the
> shared-pool decision below stands and only its wording moves. The cost is that pillar 2 now binds
> harder than pillar 5 — ten silhouettes and ten hues, in a palette that already owns most of the warm
> spectrum. See [`station-pool-requirements`](../../production/01-requirements/station-pool-requirements.md).

Two reasons, and the second is the load-bearing one:

1. **Theme becomes a deck choice.** Desert plays differently from ocean because you get different
   *tools*, not different *numbers*. That is real identity without divergent balance.
2. **It is the only option pillar 5 survives.** "Eight stations whose combinations matter" beats "forty
   that differ by a stat line", and a new station must justify itself against the two it most resembles.
   Per-theme rosters would need thirty stations to each clear that bar.

The cost that made the decision non-obvious: it took **eleven balance passes to get one map balanced
with two stations**, and the project currently has exactly one balanced map. Six independent rosters was
never affordable; this keeps the count of things that must be balanced against each other at one.

## Sequence

| # | Slice | State |
|---|---|---|
| 1 | `map-themes` — a map declares its palette | **done** ([v1](../../production/06-release/map-themes-v1.md)) |
| 2 | `board-editor-2` — the editing components | **blocked**: needs to know which editing operations are actually painful |
| 3 | `tile-art-pipeline` — UVs, atlas, ground images as placeholders | next |
| 4 | `ludo-tile-prompts` — the prompt set, once one theme works end to end | after 3 |
| 5 | `station-pool` — grow the roster to **10**, availability by **board difficulty** | **requirements ready**, 2026-08-09 |
| 6 | `river-bridges` — water and spans across the height field, **view-only** | opened 2026-08-09 |

**The tile spec matters more than the tiles.** A Ludo prompt is only as good as the constraints it
carries — iso projection alignment, seam behaviour, and what has to stay readable at wave density. So
prompting comes *after* one theme works end to end in placeholders, never before. The durable artifact
is the prompt, not the image (`gridfall-priorities`).

## The constraint themes have already run into

`map-themes` hit this immediately and it will bind harder as the roster grows:

> **The roster owns most of the warm spectrum.** Khaki brute, orange-brown husk, two orange stations, one
> reserved red. A theme's ground must clear the hue band of every unit *and* both functional markers,
> which leaves warm themes very little room — the first `desert` camouflaged the brutes and the first
> `underwater` swallowed the goal marker.

This has a direct consequence for step 5. **Adding stations narrows the space themes can use**, and
adding themes narrows the space stations can use. They are competing for the same finite resource.

The unresolved question is which one gives way. Two candidate answers, neither taken yet:

- **Units keep fixed hues; themes work around them.** Current behaviour. Simple, and it gets harder
  with every station added.
- **Units get theme-aware palettes** (`themed-unit-palettes`) — an ocean board's stations shift cool
  together, preserving *relative* contrast rather than absolute hue. More faithful to the pillar
  ("silhouette carries identity, colour carries state") but it means a player cannot learn one colour
  and keep it.

Decide this before `station-pool` ships, not after. Deciding it late means re-picking every colour in the
game with six themes already authored.

## What is deliberately not in this direction

- **Per-theme visitor rosters.** Same argument as stations, same answer, not yet designed.
- **Themed wave tables.** `gauntlet-cliff` showed a wave table is a property of a map's *geometry*, not
  its look. A forest table and a desert table would be a coincidence, not a design.
- **A theme changing any rule.** Themes are art plus station availability. The moment underwater changes
  movement, it is a mechanic and needs its own slice and its own balance run.
