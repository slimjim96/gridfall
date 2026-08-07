# Core Foundation — v1

**Slug:** `core-foundation` · **Status:** done · **Verified at trace:** `394b8c4237d52a19`
*(`crossroads-baseline`, 3,000 ticks, seed 12345)*

## What Shipped

The simulation. `Gridfall.Core` is a `net8.0` library with no Godot reference, no floating point, and
no clock: nine ordered tick phases, Q16.16 fixed-point arithmetic, structure-of-arrays state behind a
per-tick FNV-1a hash, a flow field rebuilt only on a dirty grid, and a content loader that turns
authored JSON into runtime data without ever parsing a decimal to a `double`.

Around it: 70 tests, and `Gridfall.Verify` — a harness that records and replays traces, reports map
geometry against `MapTargets`, measures tick cost, and runs headless waves.

A wave now runs end to end. Creeps spawn on schedule, walk the flow field, get shot, die or leak, and
gold and lives move. Placing a tower re-routes them; a build that would seal the only lane is refused
before the grid changes.

## Player-Facing Change

None. There is nothing to look at — no renderer exists. What exists is a game that can be *run*,
*replayed exactly*, and *tested*, which is what everything visible will be built on.

## New Tuning Knobs

Every number in `content-data/` shipped **untuned**. They exist so the sim has something to run, and
each file says so in an `_untuned` field.

| Knob | Owner | Default set? |
|---|---|---|
| Tower cost / range / cooldown / damage (arrow-tower, cannon) | content-data | No — placeholder |
| Enemy hp / speed / bounty (runner, brute) | content-data | No — placeholder |
| Wave composition and spacing (crossroads, 3 waves) | content-data | No — placeholder |
| `crossroads` map geometry | content-data | Partly — inside `MapTargets` (42% buildable, 19-cell path), but no balance pass has run |

## Follow-Ups Not Done

| Item | Workspace | Suggested slug |
|---|---|---|
| Competent-play policy for the balance sim — without it the balance report is meaningless | tooling | `balance-play-policy` |
| A 64×64 map so the perf claim can be measured against its documented worst case | content-data | `perf-scale-map` |
| Verify determinism on a second machine or runtime — the whole `Fix32` argument is untested across platforms | tooling | `cross-platform-trace` |
| First balance pass once a play policy exists | content-data | `crossroads-first-balance` |
| The Godot view layer: `IUnitView`, placeholders, the iso camera | presentation | `view-layer-foundation` |
| The board editor | tooling | `board-editor` |
| Status effects (slow, burn) — the `09-recipe-new-system` example is not implemented | engine-systems | `status-effects` |
| `FixMath.Sin`/`Cos` via integer CORDIC, when something needs an angle | engine-systems | `fix-trig` |

## ADRs Accepted

- **ADR-0001** — Godot-free core. Implemented and enforced by a test rather than by convention.
- **ADR-0002** — `Fix32` Q16.16 everywhere. The "a grep is a complete audit" argument is now literally
  true: `SourcePurityTests` runs that grep on every `dotnet test`.
- **ADR-0003** — flow field over per-unit A*. Implemented. **Should be promoted** from
  `_examples/path-recompute/` into `engine-systems/decisions/` — it is a real decision now, not an
  illustration.

## Known Not Verified

Carried forward verbatim from the verify report:

- **Perf** was measured on a 20×9 map, not the 64×64 / 300-creep case the 8 ms budget is written for.
  215× inside budget on what was measured; the documented case remains unmeasured.
- ~~**Balance** numbers describe an undefended board.~~ **Closed 2026-08-06** by `balance-play-policy`:
  the sim is now driven by a scripted player and the first real baseline is at
  `content-data/docs/reports/2026-08-06-crossroads-baseline-balance.md`.
- **Randomness** is seeded and hashed but unused — every seed currently produces an identical run.
- **Cross-platform determinism** was verified on one machine and one runtime. The claim that `Fix32`
  generalises is well-founded but not yet observed.

## Docs Corrected to Match the Code

The engine guide was written before the code. Five places where reality won:

- Sqrt is exact bit-by-bit, not Newton–Raphson (ch. 03)
- `Sin`/`Cos` are deliberately absent, with the reason (ch. 03)
- `Verify` and `Tests` target `net10.0`; Core stays `net8.0` (ch. 01)
- Phase 9 is `FinalizeTick`, and `Hash()` is a method rather than a phase (ch. 02)
- Source lives at the repo root, not in the stage folder (`docs/conventions.md`)
