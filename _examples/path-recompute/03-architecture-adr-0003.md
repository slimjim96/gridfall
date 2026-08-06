# ADR-0003 — Flow Field Pathfinding Over Per-Unit A*

**Status:** superseded — promoted to
[`engine-systems/decisions/ADR-0003-flow-field-pathfinding.md`](../../engine-systems/decisions/ADR-0003-flow-field-pathfinding.md)
on 2026-08-06, when the `core-foundation` slice implemented it.
**Raised by:** `path-recompute` · *Workflow: WF-X3*

> Kept here as teaching material: this is what an ADR looks like when it is written during stage 03,
> before anyone has built the thing. The real one records what was actually built and what it measured.
> Read this for the shape; read that one for the truth.

## Context

Pillar 1 makes the maze the game, so the walkable grid changes constantly and every creep on the board
must re-route. The worst case is a build during a dense wave: 300 creeps, a 64×64 grid, inside an 8 ms
tick budget at 30 Hz.

Movement is four-directional with uniform cost — no diagonals, no terrain speed modifiers. That
matters: it makes the shortest-path problem solvable by plain BFS, with no heuristic and no priority
queue.

## Options

### A. Per-unit A*, recomputed on grid change

Each creep runs A* to the goal when the grid changes. Standard, well understood, and it handles
per-unit variation naturally — a flying creep or one that fears a tower can weight the grid differently.

Cost: 300 searches on the same grid, each allocating an open set and a came-from map. At ~200 expanded
nodes per search that is ~60,000 node expansions in a single tick, with allocation churn, and a
priority queue whose tie-breaking must be made deterministic by hand. Most of that work is redundant —
300 creeps solving the same grid for the same goal.

### B. One flow field, reverse BFS from the goal

One O(cells) pass builds a per-cell direction. Every creep then reads one array element to know where
to step. 4,096 cell visits replaces 60,000 node expansions, and cost is independent of creep count —
1,000 creeps would be no more expensive than one.

Cost: every creep must want the same destination and treat the grid the same way. Per-unit variation
needs a second field, not a tweak.

### C. Hierarchical A* with a portal graph

Precompute a coarse graph, path over it, refine locally. Scales to much larger maps than either.

Cost: substantially more machinery — portal maintenance on every grid change, two levels of
tie-breaking to make deterministic, and a much larger surface for a determinism bug to hide in. Gridfall
maps are capped at 64×64.

## Decision

Chose **B**.

Deciding factor: **cost is independent of creep count.** Wave density is the axis the game scales along,
and it is the one axis where A* degrades fastest. The flow field makes the worst case — a build during
the densest wave — cost the same as the best case.

The determinism argument reinforces it but did not decide it: a BFS with a fixed neighbor visit order
has exactly one tie-break rule, in one place, and it is four lines long. A* has one in the priority
queue, one in the open-set ordering, and one in the heuristic comparison.

## Consequences

### Good
- Pathing cost is O(cells), not O(creeps × path). Wave density stops being a pathing concern.
- The block check is the same BFS on a scratch grid — the refusal and the drag preview cannot disagree,
  because they are the same code.
- No allocation in the tick loop; three flat arrays and a preallocated ring buffer.
- One tie-break rule, in one place, trivially auditable.
- `MovementSystem` gets simpler: no per-creep path storage at all.

### Bad
- Per-unit path variation needs a **second flow field**, not a per-creep tweak. Flyers, or creeps that
  avoid a specific tower, each cost a field.
- The whole field recomputes even when one cell changed. At 4,096 cells that is fine; at 512×512 it
  would not be.
- Distances are integers, so weighted terrain would need a different algorithm entirely.

### Forecloses
- Diagonal movement, without moving to Dijkstra or a vector flow field.
- Per-creep pathing personality — "this creep hates that tower" — as a cheap addition. It is now a
  field-count decision, made deliberately.
- Maps meaningfully larger than 64×64 without revisiting this ADR.
