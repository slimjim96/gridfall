# Balance Pass — enemy roster and armour

**Date:** 2026-08-07 · **Map:** crossroads · **Runs:** 30 · **Seed:** 1
**Before:** [early economy](2026-08-06-crossroads-early-economy-balance.md)

## Intent

Four passes had established that the late game is trivial and that the cause is not economic. The
remaining hypothesis: *a defence of 55 towers cannot be threatened by two enemy archetypes that both
die to the same tower.*

This slice tested that hypothesis by fixing the roster. **The hypothesis was wrong.**

## What shipped

- **Armour** — flat, per-hit damage reduction, floored at 1. Flat rather than percentage so it punishes
  many-small-hits and rewards few-big-hits, which is the axis stat-variants cannot express.
- **husk** (hp 120, armour 8) — asks *do you have burst?* An arrow tower's 12 lands as 4; a cannon's 40
  lands as 32.
- **mite** (hp 18, speed 0.10) — asks *do you have coverage?* Fast and numerous.
- Four archetypes, meeting the 4–7 target. No two share a silhouette.

## The result

| Metric | Before | After |
|---|---|---|
| Leak rate | 0.9% | 0.6% |
| Runs lost | 6.7% | 6.7% |
| Waves 6–11 leak | 0.0% | **0.0%** |

**The late game did not move.** Waves 6 through 11 still leak nothing, against roughly 12,000 creeps
across four archetypes.

## The decisive diagnostic

Rather than tune armour upward and hope, I raised the husk's armour to **12** — equal to an arrow
tower's full damage, so every arrow hit lands as the floor of 1, a **92% reduction**.

Waves 7–11 still leaked **zero**.

That settles it. If reducing the dominant tower to minimum damage does not produce a single leak, the
problem is not enemy statistics and no roster can fix it.

## What is actually wrong

The player ends with **55 towers** on a 20×9 board whose route is 19 cells long. `crossroads` has 76
buildable cells, and the policy fills 72% of them.

That is roughly **three towers per cell of route**. No enemy design survives that, because every
attacker must walk past all of them.

The balance targets constrain **buildable share** — 35–55% of the grid, and crossroads is 42%, well
inside the band. That metric does not capture the thing that matters. A map can sit comfortably inside
the buildable-percentage target and still permit a defence that nothing can beat.

The missing metric is something like **buildable cells per route cell**. crossroads is at 4.0 (76 ÷ 19);
a number nearer 1.5–2.0 would make placement a real choice instead of an exercise in filling space.

## Verdict

**Kept.** Armour and the two archetypes are a genuine improvement and the roster now meets its target —
but this pass did not achieve what it set out to, and saying otherwise would be false. The value
delivered is the diagnostic, not the balance change.

The next slice is **map scale**, not content:

| Follow-up | Workspace | Slug |
|---|---|---|
| **Add a buildable-cells-per-route-cell metric** to `MapTargets`, the map report, and the editor's validation panel. Without it the editor will keep approving unwinnable-for-the-attacker maps | content-data / tooling | `map-density-target` |
| **Rework `crossroads`, or author a tighter map.** 4.0 buildable cells per route cell is the real cause of the trivial late game | content-data | `tighter-map` |
| A cannon that actually competes — the policy buys arrow towers almost exclusively, so armour's decision is untested in practice | content-data | `tower-competition` |

## A note on the trace

Enemy indices are assigned by **sorted id**, so adding `husk` and `mite` renumbered `runner` from 1 to
3. `CreepDefIndex` is hashed, so every mid-run checkpoint shifted — while the **final** hash was
unchanged, because no creeps are alive at tick 3000 to carry a def index.

A single end-of-run hash would have reported this change as no change. The harness checks 30
checkpoints, and this is why.

## Reproduce

```bash
dotnet run --project Gridfall.Verify -c Release -- balance --map crossroads --runs 30 --seed 1
```
