# 02 · The Tick Loop

One tick is 33 ms of game time (30 Hz). Every tick runs nine phases in exactly this order, every time,
with no early exits and no conditional reordering.

**Knowing which phase your code runs in is most of knowing whether it is correct.** Two pieces of code
that read the same value in different phases read different values.

```csharp
public void Tick()
{
    ApplyCommands();        // 1
    RecomputePathing();     // 2  — only if _dirty
    Spawn();                // 3
    Move();                 // 4
    AcquireAndFire();       // 5
    ResolveProjectiles();   // 6
    ResolveDamage();        // 7
    UpdateEconomy();        // 8
    FinalizeTick();         // 9  — flush events, ++tick, hash on demand
}
```

## The phases

### 1 · Apply commands — `CommandSystem`

Drains the command queue in **insertion order**. Build, sell, upgrade, start-wave.

This is the only phase that may change the walkable grid, and the only one that may reject player
input. A build runs the block check *before* mutating anything ([Chapter 06](06-pathing.md)); on
rejection it emits `BuildRejected` and the grid is untouched.

Sets `_dirty` when the grid changed. Nothing else in the engine may set it.

**May not:** move entities, deal damage, spend time. A command is applied, not simulated.

### 2 · Recompute pathing — `PathSystem`

If `_dirty`, rebuild the flow field and clear the flag. If not, do nothing at all — this phase is free
on the overwhelming majority of ticks.

Increments `_version`, which is part of the state hash. That is how the harness proves a recompute
happened when one should have, and did not when it should not have.

**May not:** read entity positions. Pathing depends on the grid, not on who is standing where.

### 3 · Spawn — `SpawnSystem`

Reads the wave table, spawns visitors whose spawn tick has arrived. New entities get the next entity id,
ascending, never reused within a run — which is what makes id order a stable tie-break everywhere else.

Newly spawned visitors **do move this tick**. They spawn before phase 4 on purpose; spawning after
movement would give every visitor a one-tick stutter at birth.

### 4 · Move — `MovementSystem`

Each visitor advances by its speed along its current heading. On crossing a cell boundary it reads
`_flow[cell]` for its new heading — and only then. A visitor between cells keeps its heading regardless
of what happened in phase 2.

Iterate visitors by **ascending entity id**. Always. Movement is independent per visitor so order does not
affect the result today, but it will the first time something couples two visitors, and by then nobody
will remember this line.

### 5 · Acquire and fire — `TargetingSystem`, then `VisitorDrainSystem`

Combat runs in **both directions**, and this one phase holds both. Two systems, in a fixed order:

**5a — stations fire (`TargetingSystem`).** Each station picks a target and fires if off cooldown. Target
selection is a fixed priority rule (default: furthest along the path, ties broken by lowest entity id).
Never "closest by float distance" — compare squared `Fix32` distances, and break exact ties by id.

Firing creates projectiles; it does not deal damage. Damage happens in 7.

**5b — visitors attack stations (`VisitorDrainSystem`).** Visitors whose def has `attackDamage > 0` pick the
**nearest** station in range (ties by lowest entity id) and buffer damage against it. They do not stop
walking to do it — phase 4 has already moved them, and attacking never touches position.

**Stations fire first, and that order is load-bearing** ([ADR-0006](../../engine-systems/decisions/ADR-0006-visitor-attacks-in-phase-five.md)):
a station destroyed this tick still gets its shot off. Swapping the two changes outcomes.

**Iterate stations by ascending entity id**, and evaluate candidate targets in ascending id order too.
The same rule applies to visitors in 5b.

Visitor attacks live here rather than in a tenth phase because acquiring a target and firing is the same
operation with the roles swapped. Station damage goes into its own buffer and is applied in 7, exactly
like visitor damage.

### 6 · Resolve projectiles — `ProjectileSystem`

Advance projectiles, detect arrival, convert arrivals into pending damage records. Instant-hit weapons
skip straight to a pending damage record in the same tick they fired.

Pending damage accumulates into a buffer; it is not applied yet. This is what makes simultaneous kills
deterministic — see below.

### 7 · Resolve damage — `ServingSystem`

Apply the whole pending-damage buffer, **in entity id order**, then process deaths, then process leaks.

Deaths are resolved after all damage is applied. Two stations that both fire a killing blow at the same
visitor on the same tick produce one death and one bounty, regardless of which station is evaluated first.
If damage were applied inline in phase 5, the answer would depend on station iteration order.

### 8 · Economy — `EconomySystem`

Bounties from deaths this tick, income, and life loss from leaks. Single-threaded integer arithmetic
over an ordered list — nothing interesting happens here, which is the point.

### 9 · Finalize — `Sim.FinalizeTick`

Close out the wave if it is complete, then increment `TickCount`.

The event log is cleared at the *top* of the next tick rather than the bottom of this one, so a caller
can read `sim.Events` after `Tick()` returns — which is what the renderer and the harness both need.

`Hash()` is a method, not a phase: it reads the finished state whenever someone asks. It must see
everything the tick did, so anything not folded into it is invisible to the harness. See
[Chapter 04](04-state-and-entities.md).

*(The method is `FinalizeTick`, not `Finalize` — the latter collides with `Object.Finalize` and C#
rejects it.)*

## Why the order is what it is

| Boundary | Why it must be that way |
|---|---|
| Commands (1) before pathing (2) | A build must be reflected in the field visitors use this same tick. |
| Pathing (2) before movement (4) | Otherwise visitors spend one tick walking a field that no longer exists. |
| Spawn (3) before movement (4) | New visitors move on their birth tick; no stutter. |
| Firing (5) before damage (7) | Damage is buffered so simultaneous kills don't depend on station order. |
| Damage (7) before economy (8) | Bounties need this tick's deaths, not last tick's. |
| Hash (9) last | It must see everything the tick did. |

## The rules for adding to a phase

1. **Name the phase before writing the code.** If you cannot, you do not yet understand the change.
2. **Never read a value written later in the same tick.** If you need it, you are in the wrong phase.
3. **Never write to a phase's data from another phase.** `_dirty` is set only in 1, cleared only in 2.
4. **Iterate by ascending entity id, always** — even where order provably does not matter yet.
5. **Buffer, then apply**, wherever two producers can affect one target in the same tick.
6. If your work does not fit any phase, do not wedge it in. Adding a phase is allowed; it needs an ADR
   and an update to this chapter. See [Chapter 09](09-recipe-new-system.md).

## Timestep and the renderer

The game runs an accumulator, not a per-frame tick:

```csharp
_accumulator += (float)delta;
while (_accumulator >= TickSeconds) { _sim.Tick(); _accumulator -= TickSeconds; }
float alpha = _accumulator / TickSeconds;   // interpolation factor for the renderer
```

`alpha` is view-side only, and it is a `float` — that is fine, because it never re-enters Core. A frame
that renders between ticks shows an interpolated position that the simulation never actually held.
Nothing may ever read that interpolated value back into the sim.

If the game stalls, the `while` catches up by running several ticks in one frame. The simulation cannot
tell the difference, and neither can the harness. That is the property that makes the whole design work.
