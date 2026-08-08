# Wave Variance — Requirements

**Slug:** `wave-variance` · **Status:** backlog · **Owner:** design-lead

## In One Sentence

Waves stop being a fixed script — order, timing, composition and pace vary run to run, so wave 7 is
recognisably wave 7 without being the *same* wave 7.

## "Random" has to mean seeded, and that is not a compromise

Rule 1 of this codebase is that the simulation is deterministic: same map, same inputs, same tick
count, byte-identical state. Unseeded randomness would break every recorded trace, the balance sim,
and replay.

**The infrastructure is already there.** `SimRandom` exists, takes the run seed, and is included in
`Sim.Hash()` and in `Snapshot()` — so a seeded shuffle is reproducible, replayable and hashable by
construction. "Random" here means *drawn from the run seed*, which is unpredictable to the player and
exactly repeatable for the harness. Nothing is given up.

One consequence to plan for: **nothing appears to consume the RNG today**, so its state never
advances. The first draw changes `Sim.Hash()` and the `crossroads-baseline` trace must be
re-recorded — expected, deliberate, and worth saying out loud rather than discovering.

## The reframe that decides the design: vary the arrangement, not the budget

This is the whole slice. Get it wrong and the feature is a net loss.

If randomness can change **how hard** a wave is, three things break at once:

- **Pillar 4 — every loss is explainable.** "The RNG gave me brutes on wave 3" is not an explanation,
  it is an excuse. It is the specific kind of loss the pillar forbids.
- **The balance work stops meaning anything.** `hpGrowth 1.14` from `hpGrowthFrom 4` was reached over
  six passes, and the report says plainly that one scalar could not shape the curve. Randomising
  difficulty on top of it discards that.
- **"23.3% of runs lost" becomes unreadable.** It would mix player policy with wave luck, and no
  amount of runs separates them afterwards.

So: **every wave keeps a fixed threat budget, taken from the authored curve. Randomness decides only
how that budget is spent.** Same difficulty, different shape. That is precisely what was asked for —
hardest-first, mixed, unpredictable — because those are all *arrangements* of the same budget.

## What may vary

| Axis | Example | Notes |
|---|---|---|
| **Order** | Hardest first; escalating; interleaved | The "less predictable" ask, and the cheapest to build |
| **Timing** | One burst, or a steady trickle, at equal count | `SpacingTicks` / `DelayTicks` already exist per entry |
| **Composition** | 10 runners ↔ 4 brutes, at equal budget | Needs a threat cost per enemy — the real work |
| **Speed** | A per-wave `speedScale` | `EnemyDef.Speed` is `Fix32`, so this is exact. **But speed is difficulty** — it changes time under fire, so it must be priced into the budget, never free |

## What must not vary

- **The budget.** See above.
- **Which enemies are eligible at a given wave.** Sappers arriving at wave 1 is unfair regardless of
  budget — the player has not been taught them yet. First-appearance waves stay authored.
- **The spawn count / lane assignment**, unless the map says so. That is map design, not pacing.

## Authored and generated should coexist

A wave with explicit `entries` stays exactly as authored — that is how a designer pins a teaching
wave, or the wave that introduces an enemy. A wave that instead declares a budget and an arrangement
policy gets generated from the run seed.

That keeps every existing wave table valid and makes this opt-in per wave, not per game.

## Acceptance Criteria

- [ ] Same seed ⟹ byte-identical run. The determinism trace passes after re-recording
- [ ] Two different seeds produce visibly different wave 7s
- [ ] Measured difficulty across seeds is **flat**: the spread of runs-lost across N seeds is small
      compared to the spread across waves
- [ ] Every authored wave table still loads and plays unchanged
- [ ] An enemy never appears before its authored first-appearance wave
- [ ] `Verify balance` varies the wave seed as well as the policy, and reports both

## Open Questions

1. **What is the threat budget, concretely?** The obvious candidate is `hp × count`, scaled by the
   existing `HpScale`. But bounty, armour, speed and lives-cost all affect real difficulty, and
   `gauntlet` already proved a single scalar can encode the wrong thing. This is the slice's hard part
   and probably wants its own measurement pass.
2. **How much variance is wanted?** "Unpredictable" and "fair" pull against each other. A tunable
   variance factor (0 = today's fixed script, 1 = maximum shuffle) would let it be dialled with the
   sim rather than argued about.
3. **Should the player see the incoming composition?** A wave preview makes variance a decision
   ("brutes next — re-maze now") instead of a surprise. Without it, variance mostly adds noise.
   **This may be the more valuable half of the feature.**

## Suggested First Slice

**Order and timing only, at fixed composition.** No budget model needed — reordering and re-spacing
the *same* entries cannot change a wave's total difficulty by much, so it delivers "not as
predictable" while the threat-cost question is still open. It also forces the seeded-RNG plumbing and
the trace re-record, which everything after depends on.

Composition and speed follow once there is a budget worth trusting.

## Downstream

| Workspace | What it needs |
|---|---|
| `engine-systems` | Where generation runs — load time or tick time — and an ADR if the RNG grows a second consumer, since draw *order* becomes load-bearing |
| `content-data` | The budget model, the per-enemy threat cost, and a re-read of the curve reports |
| `presentation` | The wave preview, if question 3 goes that way |
