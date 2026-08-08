# Tier 2 — The Soft-Lock Question

**Status:** `proposed` · **Decision owner:** the human · **Written:** 2026-08-08

[`fulfilment-direction.md`](fulfilment-direction.md) lists three ways to stop *"my stations do
nothing"* and says the choice is the whole design. This note prices the three against the engine as
it actually stands, so the decision is a preference rather than an investigation. **It decides
nothing.**

The rule the three options are all trying to preserve is one line in
[`DamageSystem.cs:101`](../../Gridfall.Core/Systems/DamageSystem.cs):

```csharp
int amount = System.Math.Max(1, r.Amount - armour);
```

That clamp *is* the no-soft-lock guarantee. Everything below is judged on whether it keeps it.

---

## What each option costs in the engine

### A · A wrong station still gives partial progress

**The same line of code.** A mismatch becomes a penalty subtracted like armour, floored at 1. No new
per-creep state, nothing new in the state hash, no new tick phase, no new determinism surface. The
invariant is not re-derived — it is literally the one already shipping.

- **Keeps the guarantee:** yes, by construction.
- **Cost:** an answer field on the station def, a need field on the visitor def, one comparison.
- **Risk:** matching becomes an optimisation rather than a requirement. A child who never does the
  arithmetic still finishes the level, just slower.

### B · Waves only ask questions the current stations can answer

`SpawnSystem` would have to read tower state to choose wave content. That makes
`content-data/waves/*.json` **a generator input rather than a wave table**, and it changes what a
recorded trace means: reproducing a wave would require the whole board, not the seed and the table.
`Verify balance` and every committed trace are downstream of that.

- **Keeps the guarantee:** yes — the failure cannot arise.
- **Cost:** the largest by a wide margin, and it lands on the two things the project protects most
  (determinism and static content).
- **Risk:** removes being caught out, which is most of the planning pillar. The board can never pose
  a problem the player has not already solved.

### C · An unanswered visitor slows instead of passing

**There is no slow mechanic in `Gridfall.Core`.** Nothing in the tree matches `Slow`; speed is applied
in `MovementSystem` with no modifier concept. This needs new per-creep state, which needs to enter the
state hash (`HashCoverageTests` enforces that), which is a new determinism surface.

- **Keeps the guarantee:** **no — it replaces one soft-lock with a worse one.** A board with no
  correct station stalls: visitors slow, never resolve, the wave never ends, the run neither fails nor
  progresses. "My stations do nothing" at least ends. This is the exact failure the floor-at-1 comment
  exists to prevent, wearing a friendlier coat.

---

## Recommendation

**Option A.** It is the only one of the three that preserves the no-soft-lock invariant by keeping it
rather than by rebuilding it, and it is close to free in a codebase whose two hard rules are
determinism and a Core that owns its own state.

The objection to A is real — brute force works — but the pressure it needs already exists.
`hpGrowth 1.10 from wave 6` compounds; appetites outrun a player who is not matching, and they feel it
as falling behind rather than as a wall. **Matching is how you keep up, not a gate you must pass.**
That is a better shape for a children's product than a lockout, and it is the same shape the game
already uses everywhere else.

The decisive argument is that A makes the question **measurable instead of arguable.** Set the
mismatch penalty, run `Verify balance --map <id> --runs 150`, and read runs lost. The gap between
"matched play" and "brute-force play" becomes a number in `balance-targets.md` that can be tuned to
whatever the product wants. B and C both have to be built before anyone can find out whether they
feel right.

## If A is chosen, the open sub-question

**How big is the mismatch penalty?** That is a balance number, not a design one, and it should be
picked by measurement — but note it cannot be measured honestly until the wave-table length question
is settled, because the late band is currently two waves wide. See
[`2026-08-08-example-levels-balance.md`](../../content-data/docs/reports/2026-08-08-example-levels-balance.md).

## What is still the human's call

Everything above is a cost analysis. **The product question — whether a child should be able to finish
a level without doing the arithmetic — is not a technical one**, and A answers it "yes, slowly" while
B answers it "the question never arises" and C answers it "no". That is the actual decision, and the
engine costs should inform it rather than make it.
