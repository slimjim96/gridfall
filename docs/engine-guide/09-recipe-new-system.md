# 09 · Recipe — Add a Simulation System

For new simulation behavior that does not fit inside an existing phase. Worked with a running example:
**a burning status that deals damage over time**.

Before starting, be sure it actually needs a system. It does not if it is a new *number* on an existing
def (content), or a new *visual* for an existing event (presentation). It does if it introduces state
that advances on its own schedule — burning does.

## 1 · Place it in the tick order

Burning deals damage, so it produces damage records. Damage is applied in phase 7, buffered from phase
6. Burning therefore ticks in **phase 6**, alongside projectile resolution, writing into the same
pending-damage buffer.

Not phase 7 — that phase applies the buffer, and a producer running inside the consumer is how ordering
bugs start. Not phase 4 — movement must not care whether something is on fire.

If you cannot place your system, stop and read [Chapter 02](02-tick-loop.md) again. An unplaced system
is an undefined system.

## 2 · Decide whether you need a new phase

You almost never do. Adding one requires an ADR and an edit to Chapter 02, because it changes the
contract every other system was written against.

Burning does not. It slots into 6.

## 3 · Define the state

```csharp
// SimState.cs — parallel to the existing visitor arrays
public Fix32[] VisitorBurnDps;        // 0 == not burning
public int[]   VisitorBurnEndTick;
public Fix32[] VisitorBurnAccum;      // the sub-unit accumulator — see Chapter 03
```

Three arrays rather than a `BurnStatus` object per visitor: no allocation, and the hash stays a loop over
arrays.

The accumulator is not optional. Burning applies a fraction of a point per tick, and truncating that to
zero every tick means burning deals no damage at all — the classic fixed-point mistake
([Chapter 03](03-fix32.md)).

## 4 · Hash it, in the same commit

```csharp
// SimState.Hash(), inside the visitor loop
h = FnvHash.Combine(h, VisitorBurnDps[slot], VisitorBurnEndTick[slot], VisitorBurnAccum[slot]);
```

```csharp
[Fact]
public void Hash_Covers_Burn()
{
    var sim = TestSim.WithOneVisitor();
    ulong before = sim.Hash();
    sim.State.VisitorBurnDps[0] = Fix32.FromInt(1);
    Assert.NotEqual(before, sim.Hash());
}
```

Do not defer this to "when the system works". A system that works and is not hashed is the
worst-case outcome: green harness, broken determinism.

## 5 · Write the system

```csharp
// Gridfall.Core/Systems/BurnSystem.cs — phase 6
internal static class BurnSystem
{
    public static void Run(SimState s, ServingBuffer pending, int tick)
    {
        foreach (int slot in s.VisitorSlotsByIdAscending())      // id order, always
        {
            if (s.VisitorBurnDps[slot].Raw == 0) continue;

            if (tick >= s.VisitorBurnEndTick[slot])
            {
                s.VisitorBurnDps[slot] = default;
                s.VisitorBurnAccum[slot] = default;
                continue;
            }

            s.VisitorBurnAccum[slot] += s.VisitorBurnDps[slot] * Sim.TickSeconds;
            if (s.VisitorBurnAccum[slot] >= Fix32.One)
            {
                int whole = s.VisitorBurnAccum[slot].ToInt();
                s.VisitorBurnAccum[slot] -= Fix32.FromInt(whole);
                pending.Add(s.VisitorId[slot], whole, DamageSource.Burn);
            }
        }
    }
}
```

One file, named for the system, in `Systems/`. Static and stateless — all state lives on `SimState`,
which is what makes snapshot and restore work without the system participating.

## 6 · Wire it into the tick

```csharp
// Sim.Tick()
void ResolveProjectiles()
{
    ProjectileSystem.Run(_state, _pending, TickCount);
    BurnSystem.Run(_state, _pending, TickCount);        // ← ordered within the phase, deterministically
}
```

Order *within* a phase matters as much as the phase itself. Projectiles before burning, chosen because
a projectile landing this tick should be able to refresh a burn before the burn ticks. Write that
reason down — here and in the architecture note.

## 7 · Emit events, don't dictate visuals

```csharp
_events.Add(new SimEvent(tick, EventKind.VisitorBurnApplied, visitorId, durationTicks));
_events.Add(new SimEvent(tick, EventKind.VisitorBurnExpired, visitorId, 0));
```

Facts, not instructions ([Chapter 05](05-commands-and-events.md)). What the fire looks like is
presentation's decision and it should be able to change it without touching Core.

## 8 · Snapshot and restore

`Snapshot()` deep-copies everything hashed. If you added the arrays to `SimState` and to `Hash()`, and
`Snapshot` copies by reflection over hashed fields, you are done. If it copies explicitly, add them.

The round-trip test will tell you: restore then run 100 ticks, compare against 100 ticks without the
round-trip.

## 9 · Test it

| Test | Asserts |
|---|---|
| `Burn_DealsExpectedTotal_OverDuration` | Sum of damage matches DPS × duration, exactly |
| `Burn_Expires_OnEndTick` | Not one tick early, not one late |
| `Burn_Reapplied_RefreshesWithoutStacking` | The interaction rule from the design spec |
| `Burn_SubUnitDps_AccumulatesNotTruncates` | 0.4 DPS over 10 ticks deals 4, not 0 |
| `Hash_Covers_Burn` | From step 4 |
| `Determinism_BurnHeavyTrace` | A recorded trace with lots of burning replays identically |

The sub-unit test is the one that catches the real bug. Write it first.

## 10 · Document it

- Add the system to the phase-6 entry in [Chapter 02](02-tick-loop.md).
- If it introduced a rule other systems must respect — "burn refreshes, never stacks" — that belongs in
  the design spec, and the tick-order note here.
- Record the within-phase ordering decision in the build notes.

## Checklist

- [ ] Phase named, and within-phase order justified
- [ ] State on `SimState`, in arrays, no per-entity objects
- [ ] Accumulator for anything sub-unit per tick
- [ ] Hash line + hash-coverage test, same commit
- [ ] Iterates by ascending id
- [ ] Emits facts, not visual instructions
- [ ] Snapshot round-trip passes
- [ ] Trace re-recorded — **after** diagnosing which tick it diverged at and why
- [ ] Any new player-visible state has a view cue. Persistent state is a property on `IUnitView`, never
      a clip: a clip replays on reload and does not survive the view being recreated
- [ ] Chapter 02 updated
