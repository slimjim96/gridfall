# ADR-0006 — Resolve Enemy Attacks in Phase 5, Not a New Phase

**Status:** accepted
**Date:** 2026-08-07 · **Raised by:** `tower-combat`

## Context

Enemies can now attack towers. That is a second combat participant, and the tick order has nine fixed
phases that every system was written against ([engine guide 02](../../docs/engine-guide/02-tick-loop.md)).

The mechanic exists to break an invariant seven balance passes ran into: *total defence tracks
cumulative income*, which holds only because towers are permanent. Destructible towers stop income
compounding into permanent power. So this needs to be correct, not merely working.

## Options

### A. A new phase between firing and projectile resolution

An explicit "enemies attack" step. Clearest to read in the tick listing, and it makes the two combat
directions visibly separate.

Cost: the phase count is part of the contract every existing system was written against, and the guide
says adding one requires an ADR and a rewrite of chapter 02. Ten phases also invites an eleventh.

### B. Inside phase 5, after tower targeting

Phase 5 is already "acquire a target and fire". Enemies acquiring a tower and firing at it is the same
operation with the roles swapped, so it belongs to the same step. Tower damage is buffered exactly like
creep damage and applied in phase 7, which is already "resolve damage".

Cost: phase 5 now does two things, and the within-phase order becomes load-bearing.

### C. Fold it into movement (phase 4)

Attack-while-walking is conceptually part of moving. Tempting because no new system is needed.

Cost: movement would deal damage, which breaks the rule that a phase does one thing, and it puts a
damage producer before the phase that decides what is in range. It would also make the tick order lie.

## Decision

Chose **B**.

Deciding factor: **enemy attacks are not a new kind of work.** Phase 5 acquires and fires; phase 7
applies the results. Adding a participant to an existing symmetric step preserves the nine-phase
contract, while option A would change a structure every system depends on for a mechanic that fits the
existing one.

Within phase 5, **towers fire first, then enemies attack.** Fixed and documented. A tower destroyed
this tick still gets its shot off — which is both fairer and easier to reason about than the reverse.

## Consequences

### Good
- Nine phases stay nine. No existing system needs to know this happened.
- Tower damage flows through the same buffer-then-apply discipline as creep damage, so simultaneous
  destruction is deterministic for the same reason simultaneous kills are.
- Symmetry is visible in the code: two acquisition passes, one resolution.

### Bad
- Phase 5 does two things, and their order is now load-bearing. It has a comment saying so.
- Reading the tick listing no longer tells you enemies attack; you have to read phase 5's body.
- A tower destroyed in phase 7 unblocks its cell, but phase 2 has already run — so pathing updates on
  the **next** tick. One tick of stale routing after a destruction. Acceptable and documented; the
  alternative is a second recompute per tick for a rare event.

### Forecloses
- Nothing structural. Splitting it into its own phase later is a mechanical change plus a guide edit,
  and this ADR would be superseded rather than worked around.
