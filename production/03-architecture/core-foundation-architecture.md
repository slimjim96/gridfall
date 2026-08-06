# Core Foundation — Architecture

**Slug:** `core-foundation` · **Status:** done
**Supersedes for implementation:** the requirements file

## The architecture is the engine guide

This slice does not restate it. [`docs/engine-guide/`](../../docs/engine-guide/README.md) is the
architecture — nine tick phases, `Fix32`, structure-of-arrays state, the hash, the flow field, content
loading. Chapters 01–07 are the spec this build is checked against.

**Stage 02 was skipped deliberately.** Design (WF-02) turns player-facing intent into mechanics and
tuning knobs. This slice has no player-facing surface and introduces no knobs — it is the substrate.
Skipping a stage is normally a smell; the reason is recorded here so the gap in the trail is a decision
rather than an omission.

## Deltas from the guide

Everything below differs from what the guide says, and the guide has been corrected to match. Code and
manual do not get to disagree.

| # | Guide said | Reality | Why |
|---|---|---|---|
| 1 | Core is `net8.0` | unchanged — `net8.0` | Godot 4.6's `Godot.NET.Sdk` targets it |
| 2 | (unstated) | `Gridfall.Tests` and `Gridfall.Verify` target `net10.0` | Only the 10.0 runtime is installed on this box; a `net8.0` console app cannot run here. A `net10.0` app referencing a `net8.0` library is fine, and Core stays Godot-compatible |
| 3 | `FixMath.Sqrt` is Newton–Raphson with fixed iterations | Exact bit-by-bit integer sqrt | Exact beats approximate at the same cost, and there is no iteration count to get wrong |
| 4 | `FixMath.Sin`/`Cos` are table-based | **Not implemented** | The table would have to be seeded from somewhere. Seeding it with `double` at static init puts platform-dependent values in Core, which is the one thing Core exists to avoid. Nothing in this slice needs trig. It arrives, integer-generated, when something does |
| 5 | Source lives under `production/04-build/[slug]/` | Source lives at the repo root | A .NET solution needs stable project paths, and the Godot project must reference `Gridfall.Core` from a fixed location. `04-build/[slug]/` holds the build notes and points at the files. `docs/conventions.md` updated |

## Systems Touched

All new. Phase numbers are the tick order from
[engine guide 02](../../docs/engine-guide/02-tick-loop.md).

| System | Phase |
|---|---|
| `CommandSystem` | 1 |
| `PathSystem` / `FlowField` | 2 |
| `SpawnSystem` | 3 |
| `MovementSystem` | 4 |
| `TargetingSystem` | 5 |
| `ProjectileSystem` | 6 |
| `DamageSystem` | 7 |
| `EconomySystem` | 8 |
| `Sim.Finalize` | 9 |

## Determinism Checklist

| Check | Result |
|---|---|
| No floats in Core | Enforced — `Fix32` only; a test greps the source |
| No `Random` / `DateTime` / wall-clock | Enforced — `SimRandom` only; same test |
| No `Dictionary` / `HashSet` iteration | No dictionaries in Core at all; id→slot is a dense `int[]` |
| Ties broken by a fixed rule | N,E,S,W neighbour order then ascending entity id |
| No parallelism | Single-threaded throughout |
| State hash covers new state | Every field on `SimState`, with a per-field coverage test |

## Verify Plan

1. Determinism: two runs, identical hashes, across all fixture maps.
2. Snapshot round-trip: restore + N ticks == N ticks.
3. Equal-cost tie: asymmetric dogleg map, all creeps take one route, stable across 50 runs.
4. Block check: sealing build refused, grid byte-identical, event emitted.
5. Mid-cell: creep at sub-cell offset finishes its crossing before turning.
6. Simultaneous kill: one death, one bounty.
7. Hash coverage: one test per hashed field.
8. Source purity: no `float`/`double`/`Random`/`DateTime`/`Godot` in `Gridfall.Core`.
9. Full wave end to end.

## ADRs

Implements [ADR-0001](../../engine-systems/decisions/ADR-0001-core-view-boundary.md) (Godot-free core)
and [ADR-0002](../../engine-systems/decisions/ADR-0002-fixed-point-arithmetic.md) (`Fix32`).
Pathing follows ADR-0003, which this slice promoted from worked example to a real decision:
[`engine-systems/decisions/ADR-0003-flow-field-pathfinding.md`](../../engine-systems/decisions/ADR-0003-flow-field-pathfinding.md).
