# Path Recompute — Architecture

**Slug:** `path-recompute` · **Status:** done · **Supersedes for implementation:** the design spec
*Workflow: WF-03. Would live at `production/03-architecture/path-recompute-architecture.md`.*

## Systems Touched

| System | New / Changed / Affected | Tick phase |
|---|---|---|
| `CommandSystem` | Changed — build/sell now run the block check before mutating the grid | 1 |
| `PathSystem` | **New** — owns the flow field and the dirty flag | 2 |
| `MovementSystem` | Changed — reads the flow field instead of a per-creep path list | 4 |
| `TargetingSystem` | Affected — unchanged code, but creep positions now change differently | 5 |
| `SimState.Hash` | Changed — must cover the flow field's version counter | 9 |

## Data

```
PathSystem
  byte[]  _cost        // 64×64, walkable cost per cell; 255 = blocked
  byte[]  _flow        // 64×64, direction index 0-7 per cell, 8 = goal, 15 = unreachable
  ushort[] _dist       // 64×64, integer distance-to-goal, for the preview and for tie-breaks
  bool    _dirty       // set by CommandSystem, cleared by PathSystem in phase 2
  ushort  _version     // increments on every successful recompute; part of the state hash
```

Three flat arrays of 4,096 entries: ~24 KB per lane. No allocation after init — the BFS reuses a
preallocated ring buffer of 4,096 `ushort` indices.

`MovementSystem` no longer stores a path per creep. A creep needs only its cell and its sub-cell
offset; the direction comes from `_flow[cell]`. That removes 300 per-creep path lists and the
allocation churn of rebuilding them.

## Algorithm

**Flow field by reverse BFS from the goal.** O(cells), not O(creeps × path length).

1. Seed a queue with the goal cell at distance 0.
2. Pop, examine the four orthogonal neighbors **in a fixed direction order: N, E, S, W**.
3. An unvisited walkable neighbor gets `dist = current + 1` and a flow direction pointing back at the
   current cell. Push it.
4. Cells never reached keep `flow = 15` (unreachable).

Diagonal movement is not supported, which keeps costs uniform and makes plain BFS correct — no
Dijkstra, no A*, no heuristic to get wrong. See [ADR-0003](03-architecture-adr-0003.md).

**Tie-breaking.** Two routes of equal length are common on a grid. The fixed N, E, S, W visit order in
step 2 makes the first-assigned direction win, deterministically, every time. **This is the entire
determinism story for pathing** — a different visit order is a different game.

**The block check** (phase 1, before the grid mutates): apply the candidate cost change to a scratch
copy, run the BFS, and check every spawn has `dist < unreachable`. On failure, discard the scratch,
emit `BuildRejected`, leave the real grid untouched. One extra BFS on build attempts only — not per
tick.

**Recompute trigger:** `_dirty` is set by a successful build or sell in phase 1 and consumed in phase 2
of the same tick. A tick with no grid change does no pathing work at all.

**Creeps mid-cell:** `MovementSystem` reads `_flow` only when a creep crosses a cell boundary. A creep
between cells keeps its current heading until it arrives. This satisfies criterion 6 with no extra
state.

## Determinism Checklist

| Check | Result |
|---|---|
| No floats in Core | Pass — distances are `ushort`, sub-cell offsets are `Fix32` |
| No `Random` / `DateTime` / wall-clock | Pass — nothing here is random |
| No `Dictionary` / `HashSet` iteration | Pass — flat arrays and a ring buffer only |
| Ties broken by a fixed rule | Pass — N, E, S, W visit order, then lowest cell index |
| No parallelism | Pass — BFS is single-threaded |
| State hash covers new state | **Changed** — `_version` added to the hash. `_flow` itself is derived and does not need hashing, but the version proves a recompute happened when one should have |

## Boundary — What the View Sees

| Read | SimEvent |
|---|---|
| `PathSystem.FlowAt(cell)` — for the route overlay | `PathRecomputed(version)` |
| `PathSystem.PreviewRoute(candidateCell)` — dry-run BFS for the drag preview | `BuildRejected(cell, reason)` |
| `PathSystem.DistanceAt(cell)` — for route-length UI | `BuildPlaced(cell, towerId)` |

`PreviewRoute` runs on the scratch copy and mutates nothing. The view may call it every frame while
dragging; at ~4,096 cells that is cheap, and it is the same code path as the block check, so the
preview cannot disagree with the refusal.

## Verify Plan

1. Trace diff: two runs, same build order, identical per-tick hashes.
2. Equal-cost tie test: a symmetric map with two identical routes — all creeps take the same one, and
   the same one across 50 runs.
3. Block test: attempt the sealing build on each of three maps; assert refusal, unchanged grid, event
   emitted.
4. Mid-cell test: build while a creep is at sub-cell offset 0.5; assert it completes the crossing
   before turning.
5. Perf: worst case (64×64, all four lanes dirty in one tick) stays inside the 8 ms budget.

## ADRs

- [ADR-0003 — Flow Field Pathfinding Over Per-Unit A*](03-architecture-adr-0003.md)
