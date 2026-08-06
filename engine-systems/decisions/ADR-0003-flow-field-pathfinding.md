# ADR-0003 — Use a Flow Field Rather Than Per-Unit A*

**Status:** accepted
**Date:** 2026-08-06 · **Raised by:** `core-foundation`

Promoted from the worked example, where it was written as an illustration. The `core-foundation` slice
implemented it, so it is a real decision now. The example copy at
[`_examples/path-recompute/03-architecture-adr-0003.md`](../../_examples/path-recompute/03-architecture-adr-0003.md)
is kept as teaching material and marked as superseded by this file.

## Context

Pillar 1 makes the maze the game, so the walkable grid changes constantly and every creep on the board
must re-route. The worst case is a build during a dense wave: 300 creeps, a 64×64 grid, inside an 8 ms
tick budget at 30 Hz.

Movement is four-directional with uniform cost — no diagonals, no terrain speed modifiers. That is what
makes the shortest-path problem solvable by plain BFS, with no heuristic and no priority queue.

## Options

### A. Per-unit A*, recomputed on grid change

Each creep runs A* to the goal when the grid changes. Standard, well understood, and it handles
per-unit variation naturally — a flying creep, or one that fears a particular tower, can weight the
grid differently.

Cost: 300 searches over the same grid, each allocating an open set and a came-from map. At ~200
expanded nodes per search that is ~60,000 node expansions in a single tick, with allocation churn, and
a priority queue whose tie-breaking has to be made deterministic by hand. Most of that work is
redundant — 300 creeps solving the same grid for the same goal.

### B. One flow field, reverse BFS from the goal

One O(cells) pass builds a per-cell direction. Every creep then reads one array element to know where
to step. 4,096 cell visits replaces 60,000 node expansions, and the cost is independent of creep count.

Cost: every creep must want the same destination and treat the grid the same way. Per-unit variation
needs a second field, not a tweak.

### C. Hierarchical A* with a portal graph

Precompute a coarse graph, path over it, refine locally. Scales to far larger maps than either.

Cost: substantially more machinery — portal maintenance on every grid change, two levels of
tie-breaking to make deterministic, and a much larger surface for a determinism bug to hide in.
Gridfall maps are capped at 64×64.

## Decision

Chose **B**.

Deciding factor: **cost is independent of creep count.** Wave density is the axis the game scales
along, and it is the axis A* degrades on fastest. The flow field makes the worst case — a build during
the densest wave — cost the same as the best case.

The determinism argument reinforced it but did not decide it: a BFS with a fixed neighbour visit order
has exactly one tie-break rule, in one place, four lines long. A* has one in the priority queue, one in
the open-set ordering, and one in the heuristic comparison.

## As Implemented

`Gridfall.Core/Path/PathSystem.cs`. Three flat arrays plus a preallocated ring buffer; nothing
allocates after construction. Two lines carry the whole determinism story, and both have a comment in
the source saying not to touch them:

- `Directions.Offsets` is a fixed N, E, S, W array. The order decides which of two equal-cost routes
  wins.
- `if (dist[n] != NoDistance) continue;` — **first assignment wins.** A cell reached again at equal
  distance is left alone.

The second one is the defect the worked example describes, and it is guarded by
`FlowField_MatchesFirstAssignmentWinsReference`, which compares against a reference implementation
carrying the bug as a switch — plus a second test asserting the fixture actually distinguishes the two,
so the check cannot go vacuous.

Measured on the 20×9 `crossroads` map with 62 towers: 0.0034 ms average, 0.0373 ms worst per tick. The
64×64 / 300-creep case the budget is written for has not been measured — there is no map that size yet.

## Consequences

### Good
- Pathing is O(cells), not O(creeps × path). Wave density stops being a pathing concern.
- The block check is the same BFS on a scratch grid, so the refusal and the drag preview cannot
  disagree — they are the same code.
- No allocation in the tick loop.
- One tie-break rule, in one place, trivially auditable.
- `MovementSystem` needs no per-creep path storage at all.

### Bad
- Per-unit path variation needs a **second flow field**, not a per-creep tweak. Flyers, or creeps that
  avoid a specific tower, cost a field each — 24 KB and one more O(cells) pass per dirty tick.
- The whole field recomputes when one cell changes. Fine at 4,096 cells; not fine at 512×512.
- Distances are integers, so weighted terrain would need a different algorithm entirely.

### Forecloses
- Diagonal movement, without moving to Dijkstra or a vector flow field.
- Per-creep pathing personality as a cheap addition. It is now a field-count decision, made
  deliberately, and design should hear that before planning a game around it.
- Maps meaningfully larger than 64×64 without revisiting this ADR.
