# Path Recompute — Requirements

**Slug:** `path-recompute` · **Status:** done · **Owner:** design-lead
*Workflow: WF-01. Would live at `production/01-requirements/path-recompute-requirements.md`.*

## In One Sentence

When the player places or sells a tower, creeps already on the board find the new best route to the
goal instead of walking their old one.

## Pillar Check

| Pillar | Supports / Neutral / Fights | Note |
|---|---|---|
| 1 · The maze is the game | **Supports** | This is the pillar. Without it, towers are turrets. |
| 2 · Legible at a glance | Neutral | Re-routing must be visible; that is the design stage's problem. |
| 3 · Deterministic, therefore fair | **Fights (resolvable)** | Recomputing mid-run is the most likely place to introduce non-determinism. Constraint added below. |
| 4 · Every loss is explainable | **Supports** | A player who mazes badly can see the route they created. |
| 5 · Small numbers, big decisions | **Supports** | Makes placement position matter as much as placement choice. |

## TD Checklist

| Question | Answer |
|---|---|
| **Player fantasy** | Shaping the route. The player draws the path the enemy walks, and sees it obey. |
| **Pathing** | This *is* the pathing feature. Every build and sell changes the walkable grid. |
| **Economy** | None directly. Indirectly, mazing raises the value of cheap blocking towers — flagged for balance. |
| **Wave pressure** | Raises the skill ceiling substantially. A good maze can double effective time-in-range. Balance must re-target after this ships. |
| **Failure state** | The player mazes into a corner: a long route they cannot cover. Also: attempting a build that would seal the lane, which must be refused rather than allowed. |

## Constraints

1. Determinism is not negotiable. Identical builds in identical order produce identical routes, on
   every platform. Any tie between equal-cost routes resolves the same way every time.
2. A build that would leave any spawn with no route to the goal is **refused at command time**, before
   the grid changes. The player is told why.
3. Creeps mid-move are not teleported. Re-routing takes effect from the cell they are entering.
4. The recompute must not visibly stall the game.

## Acceptance Criteria

1. Placing a tower that lengthens the route causes every creep on the board to follow the new route.
2. Selling a tower that shortens the route causes every creep on the board to follow the new route.
3. A build that would fully block a lane is refused, the grid is unchanged, and the player is told.
4. Two identical runs — same map, same build order, same tick counts — produce identical state hashes.
5. Where two routes have equal cost, all creeps choose the same one, and the same one on every run.
6. No creep changes direction while between cells; re-routing applies at the next cell boundary.
7. The recompute does not push any tick over the frame budget.

## Open Questions

None. *(In a real pass, an empty section is deleted rather than left as a heading — kept here to show
where it goes.)*
