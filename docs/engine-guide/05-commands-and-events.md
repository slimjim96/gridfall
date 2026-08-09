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

public readonly struct BuildCommand   : ICommand { public Vector2I Cell; public ushort StationDef; }
public readonly struct SellCommand    : ICommand { public int StationId; }
public readonly struct UpgradeCommand : ICommand { public int StationId; public byte Path; }
public readonly struct RepairCommand  : ICommand { public int StationId; }
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

**A rejection can also be a mechanic.** `RepairCommand` is refused whenever a wave is running
(`RejectReason.WaveInProgress`), and that refusal is the entire point of the feature rather than an
error path: repair available at unlimited rate drove station destruction to zero at every legal price
([`station-repair`](../../production/06-release/station-repair-v1.md)). When a refusal carries design weight
like this, say so where the player meets it — the HUD names the rule on hover, not only after a click
that was going to fail.

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
| `VisitorSpawned` | 3 | Entity id A, def B |
| `PathRecomputed` | 2 | New field version A |
| `BuildPlaced` / `BuildRejected` | 1 | Cell, station def / reject reason |
| `StationSold` | 1 | Station A sold for refund B. B scales with remaining health (`salvage-value`) |
| `StationUpgraded` / `UpgradeRejected` | 1 | Station A, new level B / reject reason A, station B |
| `StationRepaired` / `RepairRejected` | 1 | Station A, health B restored / reject reason A, station B |
| `VisitorStranded` | 4 | Visitor A has no route. Should be impossible — the block check exists to prevent it |
| `StationFired` | 5 | Station A targeted visitor B |
| `VisitorDamaged` / `VisitorDied` | 7 | Visitor A, amount B / visitor A, def B |
| `StationDamaged` | 7 | Station A took B damage from an visitor attack |
| `StationDestroyed` | 7 | Station A destroyed at level B. Its cell is free, but pathing updates **next** tick — phase 2 has already run (ADR-0006) |
| `VisitorLeaked` | 7 | Visitor A reached the goal |
| `GoldChanged` / `LivesChanged` | 8 | New value A, delta B |
| `GameOver` | 8 | Lives reached zero |
| `CapacityExceeded` | 3, 6 | A cap was hit |
| `WaveCleared` | 9 | Wave A has no visitors left alive |

### Rules for events

- **Ordered and tick-stamped.** Within a tick they appear in phase order, and within a phase in the
  order they were emitted.
- **Cleared every tick**, in phase 9, after the renderer has had its frame. An event not consumed is
  gone — the renderer must not rely on catching up later.
- **Events are output, not state.** The log is not hashed. Two runs that produce the same state must
  produce the same events, and there is a test for that, but the hash covers state alone.
- **Emit facts, not instructions.** `VisitorDied(id)` — not `PlayDeathAnimation(id)`. Core does not know
  animations exist, and the day you want a different reaction you change one file in the view layer.

## Why the view drives off events, not diffs

The tempting alternative is polling: compare this frame's state to last frame's and animate the
difference. It fails in two specific ways.

1. **Two things in one tick.** A visitor takes damage twice and dies. The diff shows a dead visitor; the
   event stream shows both hits and the death, in order.
2. **Catch-up ticks.** After a stall the accumulator runs four ticks in one frame. The diff shows the
   net result of four ticks. The events show all four ticks' worth, so audio and VFX stay correct.

So: **audio and VFX subscribe to events; sprites and positions read state.** Continuous things
(where a visitor is) come from state and get interpolated; discrete things (that it just died) come from
events.

## SimStateView

```csharp
public readonly struct SimStateView
{
    public int Gold { get; }
    public int Lives { get; }
    public int WaveIndex { get; }
    public bool WaveActive { get; }

    public int VisitorCount { get; }
    public int VisitorSlotByOrder(int k);      // iterate with this -- ascending id
    public int SlotOfVisitor(int id);          // -1 if gone
    public int VisitorId(int slot);
    public int VisitorCellIndex(int slot);
    public Fix32 VisitorProgress(int slot);
    public byte VisitorHeading(int slot);
    public int VisitorAppetite(int slot);
    // stations and projectiles follow the same shape
}
```

Read-only by construction: no setter, and **accessors are methods returning copies, not arrays**. A
caller cannot take a reference and write through it, which is the difference between a guarantee and a
naming convention. `SimStateViewTests` asserts both properties by reflection so they cannot quietly
erode.

Pathing is read from `Sim.Path`. Its mutators — `SetBlocked`, `MarkDirty`, `ForceRebuild`,
`RecomputeIfDirty`, `RestoreFrom` — are `internal`, so the same boundary applies: the view reads the
field and cannot change it.

The read surface the renderer uses:

```csharp
public byte   FlowAt(int cellIndex);
public ushort DistanceAt(int cellIndex);
public bool   IsBlocked(int cellIndex);
public int    RouteLength(GridCell from);

// Walk the field to the goal, into the caller's span. Allocation-free.
public int TraceRoute(int startCellIndex, Span<int> into, bool preview = false);

// The drag preview: run the block check, then read the hypothetical field.
public bool   WouldRemainConnected(int cellIndex);
public byte   PreviewFlowAt(int cellIndex);
public ushort PreviewDistanceAt(int cellIndex);
```

`WouldRemainConnected` is the same call the simulation makes in phase 1, on the same scratch buffers, so
**the route the player sees while hovering and the refusal the sim issues on release cannot disagree**.
They can never contend for the buffers: the check runs in phase 1, a hover query runs between frames.

> **It answers connectivity, not legality.** `WouldRemainConnected` says the board stays connected — it
> does not say you may build there. Buildability is `map.Cells[i] == CellKind.Buildable` plus gold plus
> occupancy, checked separately by `CommandSystem` and by the view. Conflating the two produced a test
> that passed for the wrong reason; `WouldRemainConnected_AnswersConnectivity_NotBuildability` now pins
> it down.

### The escape hatch, and who gets it

`Sim.MutableState` is `internal`, visible via `InternalsVisibleTo` to `Gridfall.Tests` (which mutates
single fields to prove hash coverage) and `Gridfall.Verify` (which grants itself gold to fill a board).

The Godot project is **not** on that list. That is the entire design: the boundary that matters is
Core↔View, and the renderer having no write path is enforced by the compiler, not by review.

**Widen this reluctantly.** Every getter added here is a thing the view can couple to, and a thing the
next refactor has to preserve. When the view wants to know something new, ask first whether an event
would say it better: events describe what happened, and that is usually what a renderer actually needs.
