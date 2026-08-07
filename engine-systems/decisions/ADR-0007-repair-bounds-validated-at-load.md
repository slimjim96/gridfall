# ADR-0007 — Validate the Repair Cost Bound at Content Load

**Status:** accepted
**Date:** 2026-08-07 · **Raised by:** `tower-repair`

## Context

The repair design has an arithmetic wall. Repairing a tower from zero to full must cost **strictly less
than `SellValueAt(level)`** — because a player who does not repair can sell the tower for `S/2` and
rebuild it for `S`, a round trip whose net cost is exactly `S/2`. Above that line, repair is dominated
and nobody ever uses it.

That wall is a property of two numbers that live in different places: `repairPercent` on the tower def,
and the `cost` fields (base plus every upgrade) that determine `S`. **Either can be edited without
looking at the other.** A content author raising an upgrade cost is not thinking about repair, and the
failure is silent — the mechanic does not crash, it just stops being worth using, and the next balance
report reads as "players don't repair much" rather than "repair is arithmetically dominated".

This is the same failure mode `tower-combat` hit from the other side: `arrow hp 1300` satisfied every
balance target while quietly turning the feature off.

## Options

### A. Trust the content author

Document the bound in the design spec and the tower JSON's `_note` fields, as the upgrade rule already
is (`_upgradeRule` in `arrow-tower.json`).

Cost: the existing `_upgradeRule` note is exactly the precedent *against* this. It is prose next to the
numbers it constrains, and nothing checks it. It has held so far because two towers exist.

### B. Assert it in a unit test

A test loads the shipped content and asserts the bound for every tower at every level.

Cost: catches it, but only for content in the repo at test time. The board editor loads hand-edited
JSON, and the balance sim sweeps values that never pass through a test run. A sweep that wanders past
the wall would report its results as if they meant something.

### C. Validate in `ContentLoader` and throw

The loader already computes `RangeSquared` and defaults `SellValue` — it is where a def becomes a
runtime object. Check the bound there, for every level, and refuse to load a def that violates it.

Cost: a bad hand edit is a hard failure at load rather than a warning. Someone mid-sweep gets a crash
instead of a number.

## Decision

Chose **C**.

Deciding factor: **the bound must hold everywhere a def is loaded, and there are three such places** —
the game, the board editor, and the balance sim. Only the loader sits underneath all three. Option B
protects the one path that already has the most scrutiny and leaves the two that have the least.

The crash is the feature, not the cost. A repair curve above the wall is not a tuning choice with bad
numbers; it is a def that cannot express a working mechanic, and the sweep that produced it should stop
rather than report.

`MapValidator` is the precedent: map thresholds are enforced against `MapTargets` at load rather than
trusted, and the board editor's validation panel and the balance sim's map report read the same
constants so they cannot disagree.

## Consequences

### Good
- The wall cannot drift. Raising an upgrade cost without revisiting `repairPercent` fails loudly, at
  the moment the two numbers first meet.
- The balance sim inherits it. A sweep cannot wander into a region where repair is dominated and report
  the results as a tuning finding.
- The bound is stated once, in executable form, next to `SellValueAt` — the function it is defined
  against.

### Bad
- A hand edit in the board editor can now fail to load. The message has to name both numbers and the
  level at which they conflict, or it is worse than no check.
- The check costs a loop over upgrade levels per tower at load. Irrelevant at this scale, but it is
  work in a path that previously only parsed.

### Amended by `salvage-value` (2026-08-07)

The bound compares repair-to-full against `SellValueAt(level)` — the refund for an **undamaged** tower.
Since `salvage-value`, refunds scale with remaining health, so the real sell-and-rebuild alternative for
a *damaged* tower costs more than `SellValueAt` suggests.

The check is therefore **conservative rather than exact**: it still guarantees repair beats
sell-and-rebuild, by a wider margin than when it was written. Left as-is deliberately — the tight
version would have to model the health at which the player is deciding, which is not a property of the
def and cannot be validated at load. A bound that is safe and computable beats one that is exact and
situational.

### Forecloses
- An intentionally unrepairable tower archetype cannot be expressed as "repair so expensive nobody does
  it". It needs an explicit representation instead. That is the better design anyway — *unrepairable*
  should be a fact about a tower, not an emergent property of two costs.
