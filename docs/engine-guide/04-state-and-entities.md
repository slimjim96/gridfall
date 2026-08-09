# 04 · State and Entities

## Entities are indices, not objects

There is no `Visitor` class with a `Update()` method. An entity is an **integer id**, and its data lives
in parallel arrays owned by `SimState`.

```csharp
public sealed class SimState
{
    // visitors — structure of arrays, index == slot, not id
    public int      VisitorCount;
    public int[]    VisitorId;          // stable, ascending, never reused within a run
    public Vector2I[] VisitorCell;
    public FixVec2[]  VisitorOffset;    // sub-cell position, Fix32
    public int[]      VisitorAppetite;
    public Fix32[]    VisitorAppetiteFraction;// the DoT accumulator (Chapter 03)
    public byte[]     VisitorHeading;   // 0-3, the direction it is crossing toward
    public ushort[]   VisitorDefIndex;
    public int[]      VisitorAttackCooldown; // ticks until it can hit a station again

    // stations — same shape
    public int StationCount;
    public int[] StationId;
    public int[] StationStock;             // structure health; 0 destroys the station
    …
}
```

Three reasons this shape rather than objects:

1. **Iteration order is explicit.** No object graph, no reference chasing, no hidden ordering.
2. **No allocation in the tick loop.** Arrays are sized at init to the documented caps and reused.
3. **The hash is trivial** — hash the arrays in slot order and you have hashed the state.

## Ids versus slots

An **id** is stable for an entity's life. A **slot** is where it currently sits in the arrays and can
change when something dies.

Death uses **swap-remove**: the last live entity moves into the dead one's slot, `Count--`. That is O(1)
and it means slot order is *not* id order.

```csharp
// Anywhere that must be deterministic, iterate by id — not by slot.
foreach (int slot in state.VisitorSlotsByIdAscending())
{
    …
}
```

`VisitorSlotsByIdAscending()` is maintained incrementally as an index array; it is not a sort per tick.
When you write a new system, use it. Slot order is an implementation detail that changes whenever
something dies, and code that depends on it is a determinism bug waiting for a busy wave.

## Lookups

```csharp
int slot = state.SlotOfVisitor(id);     // -1 if dead
```

Backed by a dense `int[]` indexed by id, not a `Dictionary` — no hashing, no iteration-order hazard,
and ids are compact because they are assigned sequentially.

## What is state and what is not

| Is state (hashed) | Is not state |
|---|---|
| Entity arrays and counts | The flow field itself (derived from the grid) |
| The next entity id | Anything in the view layer |
| The grid's cost array | Interpolation alpha |
| `PathSystem._version` | Profiling counters |
| The PRNG's internal position | The event log (it is *output*) |
| Gold, lives, wave index, tick count | Cached squared ranges (derived from defs) |
| Every accumulator (DoT, income drip) | Debug overlays |

The test for "is this state?": **if two runs could differ in this value and produce different
gameplay later, it is state.** Derived values are exempt only when they are recomputed from hashed
inputs every time they are used — and `_version` is hashed precisely because the field is *not*.

## The hash

```csharp
public ulong Hash()
{
    var h = FnvHash.Init();
    h = FnvHash.Combine(h, TickCount);
    h = FnvHash.Combine(h, Gold, Lives, WaveIndex);
    h = FnvHash.Combine(h, _random.Position);
    h = FnvHash.Combine(h, _path.Version);
    h = FnvHash.CombineGrid(h, _grid.Cost);

    foreach (int slot in VisitorSlotsByIdAscending())
        h = FnvHash.Combine(h, VisitorId[slot], VisitorCell[slot], VisitorOffset[slot],
                               VisitorAppetite[slot], VisitorAppetiteFraction[slot], VisitorHeading[slot]);

    foreach (int slot in StationSlotsByIdAscending())
        h = …;

    return h;
}
```

FNV-1a, 64-bit. Not cryptographic — it needs to be fast and stable, not secure.

Two properties that matter more than the algorithm:

- **Iteration is by id, not slot.** Otherwise two identical games hash differently after a swap-remove.
- **It covers everything in the left column above.** A hash that misses a field is worse than no hash:
  the harness reports green while the game diverges, and you will not find out until much later, in a
  slice that did not cause it.

### When you add state

Same commit, three edits:

1. The array or field on `SimState`
2. The line in `Hash()`
3. A test that mutates the new field and asserts the hash changes

Step 3 is what catches the mistake. Without it, "I added it to the hash" is a claim, not a fact.

```csharp
[Fact]
public void Hash_Covers_VisitorShieldHp()
{
    var sim = TestSim.WithOneVisitor();
    ulong before = sim.Hash();
    sim.State.VisitorShieldHp[0] += 1;
    Assert.NotEqual(before, sim.Hash());
}
```

## Capacity

Arrays are allocated once at construction, sized from the documented caps: 512 visitors, 128 stations,
1,024 projectiles, 64×64 grid. Exceeding a cap throws in debug and drops the spawn in release, emitting
`CapacityExceeded`.

No growth, no `List<T>` in the hot path, no allocation after init. The tick loop should produce zero
gen-0 collections, and there is a test that asserts exactly that over 10,000 ticks.

## Snapshots

The harness and the board editor's playtest both need "run from here":

```csharp
public SimSnapshot Snapshot();              // deep copy of everything hashed
public static Sim Restore(SimSnapshot s);   // exact resume
```

`Restore` followed by N ticks must produce the same hashes as running those N ticks without the
round-trip. There is a test for that too, and it is the fastest way to discover you forgot to snapshot
a field you also forgot to hash.
