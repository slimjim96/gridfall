# ADR-0002 — Use Q16.16 Fixed-Point for All Simulation Math

**Status:** accepted
**Date:** 2026-08-06 · **Raised by:** project setup

## Context

ADR-0001 puts the simulation in a Godot-free core so its output can be diffed tick by tick. That only
buys determinism if the arithmetic itself is reproducible.

IEEE 754 doubles are deterministic *in principle* for a fixed sequence of operations, but in practice
.NET does not guarantee it across platforms and JIT versions: x64 and ARM64 differ in FMA contraction,
`Math.Sqrt` and the transcendentals are not bit-specified, and the JIT may reassociate. Gridfall runs
creep movement, tower range checks, and damage accumulation every tick for hundreds of entities across
thousands of ticks. A one-ULP difference at tick 40 is a different creep dead at tick 900.

Constraints: 30 Hz, up to 300 creeps and 60 towers, ≤ 8 ms per tick, and traces that must match across
the developer's machine and any other.

## Options

### A. `double`, with a disciplined subset

Use doubles but forbid the risky operations: no transcendentals, no `Math.Sqrt` (compare squared
distances), no reassociation-sensitive expressions. Fast, familiar, no conversion code, and the JIT is
generally well-behaved for `+ - * /` on doubles.

The risk is that the discipline is unenforceable by anything but review. A single `Math.Sin` for a
projectile arc, added in a hurry, breaks determinism in a way that only shows on another machine.

### B. `Fix32` — Q16.16 fixed-point, integer-backed

All sim math is `int`-backed fixed point: 16 bits integer, 16 bits fraction, giving a range of
±32,768 with a resolution of 1/65,536. Exactly reproducible everywhere, because it is integer
arithmetic. Sqrt and trig come from our own table-based implementations, identical by construction.

The costs are real: precision must be reasoned about rather than assumed, overflow is possible,
division is awkward, and every developer has to think in a type that is not `float`.

### C. Deterministic floating point via software emulation

Bit-exact IEEE emulation in software. Fully portable, but roughly an order of magnitude slower and
solves a problem we do not have — we do not need IEEE semantics, only reproducible ones.

## Decision

Chose **B**.

Deciding factor: **enforceability.** Option A's correctness depends on every future change respecting an
unwritten rule; option B's correctness is a property of the type system. A grep for `float` and `double`
in `Gridfall.Core` is a complete audit, and it runs as a structural invariant in every verification pass.

Resolution at 1/65,536 of a cell is roughly 0.015 mm at Gridfall's scale — far finer than anything the
game distinguishes. The range of ±32,768 comfortably covers a 64×64 grid, damage totals, and gold.

## Consequences

### Good
- Determinism is structural: integer math is identical on every platform and runtime.
- The audit is a grep, and it is automated in stage 05.
- Overflow and precision become explicit engineering concerns instead of silent ones.
- Fixed-point compares and multiplies are cheap; the 8 ms budget is not at risk.

### Bad
- `Fix32.Sqrt` and any trig must be written and tested by us.
- Division is expensive relative to multiplication; algorithms should prefer reciprocal-multiply.
- Accumulating many small values loses precision differently than floats do — damage-over-time needs
  care, and the DoT system will need its own note.
- Every contributor pays a small ongoing tax in ergonomics.

### Forecloses
- Reusing float-based algorithms from references without porting them.
- Any third-party math or pathfinding library that assumes floats, below the boundary.
