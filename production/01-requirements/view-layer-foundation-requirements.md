# View Layer Foundation — Requirements

**Slug:** `view-layer-foundation` · **Status:** done · **Owner:** design-lead

## In One Sentence

You can see the board, watch a wave walk it, and click a cell to place a tower.

## Pillar Check

| Pillar | Supports / Neutral / Fights | Note |
|---|---|---|
| 1 · The maze is the game | **Supports** | Placing a tower and watching the route change is the core loop made visible for the first time. |
| 2 · Legible at a glance | **Supports** | This slice establishes the silhouette vocabulary everything later inherits. |
| 3 · Deterministic, therefore fair | Neutral — **must not harm** | The view reads state and queues commands. If it can affect the simulation, this slice has failed. |
| 4 · Every loss is explainable | Partial | Damage and death are visible. A full "why did that leak" readout is later work. |
| 5 · Small numbers, big decisions | Neutral | No new content. |

## TD Checklist

| Question | Answer |
|---|---|
| **Player fantasy** | Seeing the board you are shaping. Until now the game exists only as hashes. |
| **Pathing** | Not changed. The view reads the flow field; it never computes one. |
| **Economy** | Not changed. Gold and lives are displayed, not altered. |
| **Wave pressure** | Not changed. |
| **Failure state** | Lives reaching zero becomes visible. The view does not decide it — `EconomySystem` does. |

## Constraints

1. The view **reads** simulation state and **queues** commands. It mutates nothing. `SimStateView` has
   no setters; that is a compile-time fact, not a review convention.
2. Every constant describing the projection comes from `docs/iso-grid.md`. None is hardcoded twice.
3. All art is placeholder under `presentation/docs/placeholder-standard.md`: procedural C#, no binary
   assets, distinct silhouettes, hour budget.
4. `Fix32 → float` happens only at the boundary, for rendering. Interpolated positions never re-enter
   Core.
5. The simulation advances on a fixed-timestep accumulator, never on frame delta.

## Acceptance Criteria

1. The map renders: buildable, path-only, blocked, spawn, and goal cells are visually distinguishable.
2. Creeps appear on spawn, move along their route, and disappear on death or leak.
3. Towers appear where they were built.
4. Clicking a buildable cell queues a `BuildCommand`; the tower appears on the next tick.
5. Clicking a cell that would seal the lane shows a visible refusal, and no tower appears.
6. The HUD shows gold, lives, and wave, and they change when the simulation changes them.
7. Creep motion is smooth between ticks — the renderer interpolates rather than stepping at 30 Hz.
8. Running the same seed twice produces identical simulation hashes **with the renderer attached**.
9. No `Godot` type appears in `Gridfall.Core`, and the view assembly never writes simulation state.
10. Creep and tower archetypes are distinguishable by silhouette alone, in greyscale, at default zoom.

## Open Questions

None blocking. Criterion 10 is a judgment call that needs a human eye even now that frames can be
captured — see the note on visual verification in the design spec.
