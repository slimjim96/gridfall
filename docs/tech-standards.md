# Tech Standards

Stable reference. Load on demand — never by default. Owned by `engine-systems`.

## Solution layout

```
Gridfall.Core/      net8.0  class library.  The simulation. Zero Godot references.
Gridfall.Io/        net8.0  Reads content-data/ off disk. Core never touches the filesystem.
Gridfall.Verify/    net10.0 Console app. Determinism harness + balance sim + map/perf reports.
Gridfall.Tests/     net10.0 xUnit. Unit tests over Core.
godot/              net8.0  The Godot project (Godot.NET.Sdk 4.6.3). Presentation only.
```

## Pinned versions

| Thing | Version | Why |
|---|---|---|
| Godot editor | **4.6.3 mono** — run as `godot-mono` | [ADR-0005](../engine-systems/decisions/ADR-0005-pin-godot-4-6-3-mono.md) |
| `Godot.NET.Sdk` / GodotSharp | 4.6.3 | Must match the editor |
| `Gridfall.Core` / `Gridfall.Io` / `godot` | net8.0 | Godot 4.6's SDK targets net8.0 |
| `Gridfall.Verify` / `Gridfall.Tests` | net10.0 | Only the 10.0 runtime is installed on the dev box |

**Never run `godot` or `godot-4`.** Both resolve to 4.7 here, and a non-mono build ignores every C#
script — which looks like a broken game rather than the wrong binary.

`Gridfall.Core.csproj` must never reference `GodotSharp`. This is checkable and it is checked: the
verify stage fails the slice if it does.

## The Core / View boundary

| Core may… | Core may not… |
|---|---|
| Own all game state and advance it by ticks | Reference any `Godot.*` type |
| Emit an ordered `SimEvent` stream | Read input, time, files, or the network |
| Accept commands from an input queue | Know that a renderer exists |

The view layer reads state and events. It mutates nothing. A click becomes
`sim.Enqueue(new BuildCommand(...))`, applied at the top of the next tick — never applied inline.

## Determinism

The rule: **same map + same command trace + same tick count ⇒ byte-identical state hash.**
`Gridfall.Verify` enforces it by replaying recorded traces and diffing per-tick hashes.

What that forbids in Core:

- **No floats.** All sim math is fixed-point `Fix32` (Q16.16). Float rounding differs across platforms
  and JIT versions. Floats are fine in the view layer, where nothing depends on them.
- **No `System.Random`, no `DateTime`, no `Environment.TickCount`.** Use the seeded `SimRandom`,
  advanced only from within the tick loop.
- **No unordered iteration.** Do not iterate `Dictionary`/`HashSet` in a state-affecting path. Use
  arrays, `List<T>`, or a sorted key order. Entity iteration goes by stable entity id, ascending.
- **No LINQ in the tick loop** where ordering or allocation matters.
- **No parallelism** inside a tick unless the merge step is order-independent and proven so in an ADR.

## Tick order

Fixed timestep, 30 Hz (`TickMs = 33`). Every tick runs these in exactly this order:

1. Apply queued commands (build / sell / upgrade / start-wave)
2. Recompute pathing **if and only if** the grid is dirty
3. Spawn from the wave table
4. Move creeps
5. Tower acquisition and firing
6. Projectile and effect resolution
7. Damage, death, and leak resolution
8. Economy and score
9. Emit events, increment tick, compute state hash

Rendering interpolates between ticks. Interpolation is view-side and never feeds back into Core.

## The pathing rule

Players place towers, which changes the walkable grid. Two invariants hold at all times:

1. **Never fully blockable.** A build that would leave no path from any spawn to the goal is rejected
   at command-apply time, before the grid mutates. The rejection is a `SimEvent`, so the UI can show it.
2. **Deterministic tie-breaking.** When two paths cost the same, the winner is chosen by a fixed rule
   (lowest neighbor index in a fixed direction order), never by iteration accident.

## Performance budget

At 30 Hz with 300 creeps and 60 towers on a 64×64 grid: **≤ 8 ms per tick** on the dev box, with
zero steady-state allocation in the tick loop. Pathing recompute is amortized — it runs on the dirty
tick only, and it is the reason the flow field exists (see `engine-systems/decisions/ADR-0003`).

## Commands

```bash
dotnet build                                    # must be 0 warnings, 0 errors
dotnet test                                     # unit gate

dotnet run --project Gridfall.Verify -- replay  # determinism trace diff
dotnet run --project Gridfall.Verify -c Release -- balance --map crossroads --runs 30
dotnet run --project Gridfall.Verify -- maps    # map geometry vs MapTargets
dotnet run --project Gridfall.Verify -- perf    # tick cost vs the 8ms budget

./run-game.sh                                   # play it
./run-editor.sh crossroads                      # board editor
./run-game.sh --shot /tmp/x.png --shot-after 40 # byte-reproducible capture
./run-game.sh --headless --quit                 # scene/resource wiring check
```

The mode is a bare word, not a flag — `-- balance`, never `-- --balance`.

Use the launchers rather than calling Godot directly. They find the pinned 4.6.3 mono binary, put
engine flags before Godot's `--` and game flags after (an engine flag on the wrong side is silently
ignored), and report a missing display or binary in one line instead of a page of ALSA noise.

## C# conventions

- `net8.0`, nullable enabled, warnings-as-errors in Core.
- Sim types are `struct` where they are hot and small; entities are indices into arrays, not objects.
- Public API on Core is minimal: `Sim.Tick()`, `Sim.Enqueue(cmd)`, `Sim.State`, `Sim.Events`, `Sim.Hash()`.
- One system per file, named `<Thing>System.cs`, matching a step in the tick order above.
