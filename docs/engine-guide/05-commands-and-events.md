# 05 · Commands and Events

Two channels cross the Core/View boundary, and there are no others.

```
   view ──▶ Enqueue(ICommand) ──▶ [ phase 1 ] ──▶ core
   view ◀── Events (ordered)  ◀── [ phase 9 ] ◀── core
   view ──── SimStateView (read-only) ──────────  core
```

Input goes **in** as a command. Consequences come **out** as events. Current values are read from a
read-only view. Nothing else is permitted, and `SimStateView` has no setters, so "nothing else" is
enforced by the compiler rather than by review.

## Commands

```csharp
public interface ICommand { }

public readonly struct BuildCommand   : ICommand { public Vector2I Cell; public ushort TowerDef; }
public readonly struct SellCommand    : ICommand { public int TowerId; }
public readonly struct UpgradeCommand : ICommand { public int TowerId; public byte Path; }
public readonly struct StartWaveCommand : ICommand { }
```

Rules:

- **A command is intent, not an outcome.** `Enqueue` always succeeds. Whether the build happens is
  decided in phase 1, next tick, and reported as an event.
- **Applied in insertion order.** The queue is a `List<ICommand>` drained front to back — deterministic
  by construction.
- **Never applied inline.** A click during frame rendering does not touch state. It waits for phase 1.
- **Commands are recorded.** The harness's trace format is `(tick, command)` pairs plus the seed. That
  is the entire input to a run, which is what makes replay exact ([Chapter 08](08-determinism-playbook.md)).

The one-tick latency between click and effect is at most 33 ms and nobody has ever felt it. It buys
exact replay, which is worth considerably more.

### Rejection

Phase 1 validates before mutating. A rejected command changes nothing and emits an event:

```csharp
if (!_path.WouldRemainConnected(cell))
{
    _events.Add(new BuildRejected(cell, RejectReason.WouldSealLane));
    return;                              // grid untouched, gold unspent
}
```

Every rejection reason has a player-facing message defined in the design spec. A refusal the player
cannot see reads as an unresponsive game — that is a design rule, but this is where it is enforced.

## Events

```csharp
public readonly struct SimEvent
{
    public readonly int      Tick;
    public readonly EventKind Kind;
    public readonly int      A, B;        // meaning depends on Kind
    public readonly Vector2I Cell;
}
```

A flat struct rather than a class hierarchy: no allocation, no virtual dispatch, and the log is a
contiguous array the renderer walks once.

| Kind | Emitted in phase | Meaning |
|---|---|---|
| `WaveStarted` | 3 | A wave began |
| `CreepSpawned` | 3 | Entity id A, def B |
| `PathRecomputed` | 2 | New field version A |
| `BuildPlaced` / `BuildRejected` | 1 | Cell, tower def / reject reason |
| `TowerFired` | 5 | Tower A targeted creep B |
| `CreepDamaged` / `CreepDied` | 7 | Creep A, amount B |
| `CreepLeaked` | 7 | Creep A reached the goal |
| `GoldChanged` / `LivesChanged` | 8 | New value A, delta B |
| `CapacityExceeded` | 3, 6 | A cap was hit |

### Rules for events

- **Ordered and tick-stamped.** Within a tick they appear in phase order, and within a phase in the
  order they were emitted.
- **Cleared every tick**, in phase 9, after the renderer has had its frame. An event not consumed is
  gone — the renderer must not rely on catching up later.
- **Events are output, not state.** The log is not hashed. Two runs that produce the same state must
  produce the same events, and there is a test for that, but the hash covers state alone.
- **Emit facts, not instructions.** `CreepDied(id)` — not `PlayDeathAnimation(id)`. Core does not know
  animations exist, and the day you want a different reaction you change one file in the view layer.

## Why the view drives off events, not diffs

The tempting alternative is polling: compare this frame's state to last frame's and animate the
difference. It fails in two specific ways.

1. **Two things in one tick.** A creep takes damage twice and dies. The diff shows a dead creep; the
   event stream shows both hits and the death, in order.
2. **Catch-up ticks.** After a stall the accumulator runs four ticks in one frame. The diff shows the
   net result of four ticks. The events show all four ticks' worth, so audio and VFX stay correct.

So: **audio and VFX subscribe to events; sprites and positions read state.** Continuous things
(where a creep is) come from state and get interpolated; discrete things (that it just died) come from
events.

## SimStateView

```csharp
public readonly struct SimStateView
{
    public int Gold { get; }
    public int Lives { get; }
    public int WaveIndex { get; }
    public bool WaveActive { get; }

    public int CreepCount { get; }
    public int CreepSlotByOrder(int k);      // iterate with this -- ascending id
    public int SlotOfCreep(int id);          // -1 if gone
    public int CreepId(int slot);
    public int CreepCellIndex(int slot);
    public Fix32 CreepProgress(int slot);
    public byte CreepHeading(int slot);
    public int CreepHp(int slot);
    // towers and projectiles follow the same shape
}
```

Read-only by construction: no setter, and **accessors are methods returning copies, not arrays**. A
caller cannot take a reference and write through it, which is the difference between a guarantee and a
naming convention. `SimStateViewTests` asserts both properties by reflection so they cannot quietly
erode.

Pathing is read from `Sim.Path` (`FlowAt`, `DistanceAt`, `IsBlocked`), which is already read-only in
practice. `PreviewRoute` for the drag preview is **not implemented yet** — the block check exists as
`PathSystem.WouldRemainConnected`, and exposing it to the view is follow-up work.

### The escape hatch, and who gets it

`Sim.MutableState` is `internal`, visible via `InternalsVisibleTo` to `Gridfall.Tests` (which mutates
single fields to prove hash coverage) and `Gridfall.Verify` (which grants itself gold to fill a board).

The Godot project is **not** on that list. That is the entire design: the boundary that matters is
Core↔View, and the renderer having no write path is enforced by the compiler, not by review.

**Widen this reluctantly.** Every getter added here is a thing the view can couple to, and a thing the
next refactor has to preserve. When the view wants to know something new, ask first whether an event
would say it better: events describe what happened, and that is usually what a renderer actually needs.
