# 06 · Pathing

Pillar 1 says the maze is the game, so the walkable grid changes constantly and every creep must
re-route. Gridfall does this with **one flow field**, not per-creep pathfinding. See
[ADR-0003](../../engine-systems/decisions/ADR-0003-flow-field-pathfinding.md).

Implemented in `Gridfall.Core/Path/PathSystem.cs`. The code below is the real thing, lightly trimmed.

## The data

```csharp
sealed class PathSystem
{
    readonly byte[]   _cost;      // 64×64. Walkable cost per cell; 255 = blocked
    readonly byte[]   _flow;      // 64×64. Direction index 0-3; 8 = goal; 15 = unreachable
    readonly ushort[] _dist;      // 64×64. Integer steps to the goal
    readonly int[]    _queue;     // preallocated BFS ring buffer, capacity == cell count
    readonly byte[]   _scratch;   // cost copy for the block check — never the live array

    bool   _dirty;                // set in phase 1, consumed in phase 2. Nowhere else.
    ushort _version;              // ++ per recompute. Hashed.
}
```

~24 KB per lane. Nothing allocates after construction.

## Building the field

Reverse BFS from the goal. Uniform cost and four-way movement make plain BFS correct — no Dijkstra, no
heuristic, nothing to get subtly wrong.

```csharp
static readonly Vector2I[] Neighbors = { North, East, South, West };   // ORDER IS LOAD-BEARING

void Build()
{
    Fill(_flow, Unreachable);
    Fill(_dist, ushort.MaxValue);

    Enqueue(_goal); _dist[Index(_goal)] = 0; _flow[Index(_goal)] = GoalMarker;

    while (TryDequeue(out int cell))
    {
        for (int d = 0; d < 4; d++)                 // N, E, S, W — always this order
        {
            int n = cell + Offset[d];
            if (!InBounds(n) || _cost[n] == Blocked) continue;
            if (_dist[n] != ushort.MaxValue) continue;   // ALREADY SET — LEAVE IT ALONE

            _dist[n] = (ushort)(_dist[cell] + 1);
            _flow[n] = Opposite(d);                 // point back toward the goal
            Enqueue(n);
        }
    }
    _version++;
}
```

Two lines carry the entire determinism story for pathing:

1. **`Neighbors` is a fixed array in N, E, S, W order.** Not an enum iteration, not a direction set.
   The order decides which of two equal-cost routes wins.
2. **`if (_dist[n] != ushort.MaxValue) continue;`** — first assignment wins. A cell reached again at
   equal distance is *left alone*. Overwriting here is deterministic but produces a field where the
   chosen route depends on the frontier shape rather than the stated rule, and creeps split across
   equal routes. That exact bug is the failed criterion in the worked example
   (`_examples/path-recompute/05-report-fail.md`) — it is easy to write and invisible on symmetric maps.

Do not "optimize" either line. Both have a comment in the source saying so.

## When it runs

`_dirty` is set by a successful build or sell in phase 1, and consumed in phase 2 of the same tick. A
tick with no grid change does no pathing work at all — which is why the field being O(cells) rather
than O(creeps) costs nothing on the 99% of ticks where nothing was built.

One flag for the whole grid, not per lane. Per-lane flags were measured at 0.3 ms of savings on the
four-lane map and rejected as not worth the state.

## The block check

Pillar-critical: a build that would leave any spawn with no route is **refused before the grid
changes**.

```csharp
public bool WouldRemainConnected(Vector2I cell)
{
    Array.Copy(_cost, _scratch, _cost.Length);
    _scratch[Index(cell)] = Blocked;

    BuildInto(_scratch, _scratchDist);              // same BFS, scratch buffers

    foreach (var spawn in _spawns)                  // fixed order, from the map def
        if (_scratchDist[Index(spawn)] == ushort.MaxValue) return false;

    return true;
}
```

One extra BFS **on build attempts only** — not per tick. At 4,096 cells that is well under the budget.

`SimStateView.PreviewRoute` calls the same function with the same scratch buffers. That is deliberate:
the drag preview the player sees and the refusal the sim issues are literally the same code, so they
cannot disagree. They can never run in the same tick — preview is a view-side query between ticks, the
check is phase 1 — so sharing the buffer is safe.

## Movement reads the field

```csharp
// MovementSystem, phase 4
if (CrossedCellBoundary(slot))
{
    var cell = state.CreepCell[slot];
    byte dir  = _path.FlowAt(cell);
    if (dir == Unreachable) { /* stand still — see below */ }
    else state.CreepHeading[slot] = dir;
}
```

A creep reads the field **only when it crosses into a new cell**. Between cells it keeps its heading no
matter what phase 2 did. That is what makes "no creep turns mid-cell" true without any extra state, and
it is a design rule the player learns, not a limitation being hidden.

### Unreachable cells

The block check makes a fully sealed lane impossible, so a creep should never stand on an unreachable
cell. "Should never" is not "cannot" — a map authored with an isolated pocket, or a future mechanic that
blocks cells without going through phase 1, could do it.

The defined behavior is: **stand still and emit `CreepStranded` once.** Not "walk toward the goal
anyway", not "die", and above all not "throw" — a crash in the tick loop takes the whole run with it.
Standing still is visible, debuggable, and cannot cascade.

## Complexity and budget

| Operation | Cost | Frequency |
|---|---|---|
| Field rebuild | O(cells) — 4,096 visits | Only on a dirty tick |
| Block check | One extra rebuild on scratch | Only on build attempts |
| Per-creep query | One array read | Only on a cell boundary crossing |

Cost is **independent of creep count**. 300 creeps and 1 creep cost the same, which is the property
that made the flow field win over A*: wave density is the axis the game scales along.

Worst case measured in the worked example: 2.1 ms with four lanes dirty in one tick, against an 8 ms
budget.

## What a second field would cost

Per-unit path variation — flyers, or creeps that avoid a specific tower — needs **another whole field**,
not a per-creep tweak. That is 24 KB and one more O(cells) pass per dirty tick, per variant.

This is a real constraint on future design, and it is the main thing ADR-0003 forecloses. Two or three
fields is fine. A field per creep archetype is not, and the design should hear that before it plans a
game around it.
