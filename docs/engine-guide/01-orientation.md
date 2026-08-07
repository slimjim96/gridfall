# 01 · Orientation

## The mental model

Gridfall's simulation is a **pure function of its inputs**. Feed it a map, a seed, and a list of
commands, and it produces the same game every time, on any machine. The renderer is a separate program
that happens to be watching.

```
        commands ──▶┌──────────────────┐──▶ state   ──▶ renderer draws it
   map + seed ─────▶│  Gridfall.Core   │
                    │  Tick() × N      │──▶ events  ──▶ renderer reacts to them
                    └──────────────────┘──▶ hash    ──▶ harness diffs it
```

If you internalize one thing: **the sim cannot see the renderer, and does not know time exists.** It
advances only when someone calls `Tick()`. In the game that someone is a fixed-timestep accumulator; in
the harness it is a `for` loop running as fast as the CPU allows. Both produce identical games.

## The projects

```
Gridfall.Core/       net8.0  class library. The simulation. No Godot, no floats, no clock.
Gridfall.Verify/     net10.0 console app. Determinism harness + balance sim + map report.
Gridfall.Tests/      net10.0 xUnit over Core.
Gridfall.Io/         net8.0  filesystem loader for content-data/. Core never touches disk.
godot/               net8.0  Godot 4.6.3 mono project. Presentation. Board editor NOT YET BUILT.
```

**Godot is pinned to 4.6.3 mono** — run it as `godot-mono`, never `godot` or `godot-4` (both resolve to
4.7 on this box, and a non-mono build cannot run C# at all). See
[ADR-0005](../../engine-systems/decisions/ADR-0005-pin-godot-4-6-3-mono.md).

**Why the split targets:** Core is `net8.0` because Godot 4.6's `Godot.NET.Sdk` targets it. Verify and
Tests are `net10.0` because that is the only runtime installed on the dev box — a `net8.0` console app
cannot run here. A `net10.0` app referencing a `net8.0` library is fine, and Core stays
Godot-compatible, which is the constraint that actually matters.

`Gridfall.Core.csproj` must never reference `GodotSharp`. `SourcePurityTests` greps for it on every
`dotnet test`, and a reference fails the build. See
[ADR-0001](../../engine-systems/decisions/ADR-0001-core-view-boundary.md).

## Namespace layout inside Core

```
Gridfall.Core
├── Sim.cs                  the entry point: Tick, Enqueue, State, Events, Hash
├── SimState.cs             all mutable game state, and the hash over it
├── SimRandom.cs            the seeded PRNG — the only randomness allowed
├── Math/    Fix32.cs, FixVec2.cs, FixMath.cs
├── Path/    FlowField.cs, PathSystem.cs
├── Systems/ one file per tick phase: CommandSystem, SpawnSystem, MovementSystem, …
├── Content/ TowerDef, EnemyDef, WaveTable, MapDef + the loaders
└── Events/  SimEvent and its variants
```

The rule for `Systems/`: **one file per phase of the tick loop**, named for what it does, in the order
it runs. If you cannot say which phase your new file belongs to, read [Chapter 02](02-tick-loop.md)
before writing it.

## The public surface

Core exposes almost nothing. This is deliberate — a small surface is a small determinism problem.

```csharp
public sealed class Sim
{
    public Sim(MapDef map, ContentSet content, uint seed);

    public void Tick();                       // advance exactly one 33 ms step
    public void Enqueue(ICommand command);    // player intent; applied in phase 1 of the next tick
    public SimStateView State { get; }        // read-only; the renderer gets this
    public EventLog Events { get; }           // ordered, tick-stamped, cleared each tick
    public ulong Hash();                      // state hash — the determinism primitive
    public int TickCount { get; }
}
```

`SimStateView` is a read-only façade with no setter and no way to reach an underlying array, so "the
view never mutates state" is a compile-time fact rather than a code-review convention. A struct
wrapping one reference — nothing allocates per frame.

First-party tooling that genuinely needs to write — the test suite proving hash coverage, the perf
harness granting itself gold — uses `Sim.MutableState`, which is `internal` and exposed only to
`Gridfall.Tests` and `Gridfall.Verify` via `InternalsVisibleTo`. **The Godot project is deliberately
not on that list**, so the renderer has no write path at all. Verified: a write attempt from the view
fails with `CS0200`, and reaching for `MutableState` fails with `CS1061`.

## Running it

```bash
dotnet build                                       # 0 warnings, 0 errors — Core is warnings-as-errors
dotnet test                                        # unit + determinism tests

# replay every recorded trace and diff per-tick hashes
dotnet run --project Gridfall.Verify

# replay one trace, verbose, stop at the first divergence
dotnet run --project Gridfall.Verify -- --trace path-recompute-baseline --verbose

# 200 headless runs for balance
dotnet run --project Gridfall.Verify -- --balance --map crossroads --runs 200 --seed 1

godot-mono --headless --quit                            # scene/resource wiring check, no display needed
```

The harness needs no Godot and no display. That is the whole reason for the project split, and it is
why the harness will still be run in a year.

## Your first change, in five steps

1. Find the phase your change belongs to ([Chapter 02](02-tick-loop.md)).
2. Write it in `Fix32`, not `float` ([Chapter 03](03-fix32.md)).
3. If it adds state, add that state to the hash **in the same commit** ([Chapter 04](04-state-and-entities.md)).
4. If the view needs to know about it, add an event — do not widen `SimStateView` casually
   ([Chapter 05](05-commands-and-events.md)).
5. Run `dotnet test`, then the harness. A green build with a red trace means you broke determinism, and
   [Chapter 08](08-determinism-playbook.md) is how you find out where.

## What will bite you

- **Writing `float` because the value is "just cosmetic".** Nothing in Core is cosmetic. If it is
  cosmetic, it belongs in the view layer.
- **Adding state and forgetting the hash.** The harness goes green while determinism is already broken,
  and you find out three slices later. This is the most expensive mistake available here.
- **Iterating a `Dictionary`** in a state-affecting path. It works on your machine. That is the problem.
- **Calling `Tick()` from the renderer's frame callback directly.** Use the accumulator; a variable
  timestep is not a timestep.
