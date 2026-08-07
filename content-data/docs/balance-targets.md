# Balance Targets

The numbers a balance pass is measured against. Load this before touching any value.

These are targets, not laws — but a change that misses one is reverted or argued for explicitly in the
report. "Missed the target, kept it anyway, no reason given" is not an outcome this project accepts.

## Run-level targets

Measured over 200 headless runs per map, fixed seed, competent-play policy.

| Metric | Target | Fail if |
|---|---|---|
| Runs lost (waves 1–10) | 0–5% | > 10% |
| Runs lost (waves 11–20) | 15–30% | < 5% (trivial) or > 50% (unfair) |
| Leak rate, overall | ≤ 4% | > 8% |
| Leak rate, any single wave | ≤ 15% | > 25% |
| Time-to-clear, per wave | 20–45 s | > 60 s (grindy) |
| Idle gold at wave 10 | 0–2 tower costs | > 4 (nothing worth buying) |
| Idle gold at wave 20 | 0–3 tower costs | > 6 |

**Idle gold** is the interesting one. High idle gold means the player has money and no decision — the
economy has stopped generating choices, which fails pillar 5 before it fails any math.

## Tower targets

| Property | Target |
|---|---|
| Cost spread across the roster | Cheapest : most expensive ≤ 1 : 6 |
| DPS-per-gold spread at equal tier | Within ±15% |
| Share of a roster used in a winning run | ≥ 4 of 8 distinct towers |
| Any single tower's presence in winning runs | ≤ 70% (a must-pick is a design failure) |

A tower outside the DPS-per-gold band must earn it with utility — slow, splash, reveal, chain. If it
cannot name the utility, the number is wrong.

## Enemy targets

| Property | Target |
|---|---|
| Archetypes per map | 4–7 |
| HP growth, wave to wave | 1.10–1.18× — **disputed, see below** |
| Speed spread across archetypes | 0.6× – 1.8× of base |
| Waves where a single archetype is > 70% of the creeps | ≤ 3 per run |

HP growth above 1.18× produces the classic wall: the player is fine, then suddenly is not, with nothing
to react to. That fails pillar 4.

> **The HP-growth band is contradicted by measurement (2026-08-06).** This target was written before
> anything could be measured. When per-wave scaling was implemented and swept, **1.10 lost 80% of runs
> and 1.18 lost 100%, dead by wave 5**; the playable range turned out to be 1.02–1.04. See
> [the HP scaling pass](reports/2026-08-06-crossroads-hp-scaling-balance.md).
>
> The number has deliberately **not** been edited to match. Either the band is wrong for this game, or
> the early economy is (wave 3 leaks 21.5% with 12 gold in hand) — and a target rewritten to match its
> first measurement stops being a target.
>
> **Vindicated, largely (2026-08-07.)** That measurement was taken before tower upgrades existed and
> before `startingGold` was fixed. With the economy working, the playable band moved and the shipped
> value is now **1.08** — just below the disputed 1.10 rather than a tenth of it. **Not editing the
> target to match the first measurement was the right call**, and this is the evidence. Resolve the
> remaining gap as `hp-growth-target`.
>
> It moved *down* again, 1.09 → 1.08, when destructible towers shipped: difficulty from losing towers
> substitutes for difficulty from enemy hitpoints. The band is a property of the whole system, not of
> the enemies alone. See
> [income vs difficulty](reports/2026-08-07-income-vs-difficulty.md).

## Map targets

| Property | Target |
|---|---|
| Shortest path, unmazed | 18–30 cells |
| Longest path at maximum mazing | ≤ 3× the unmazed path |
| Buildable cells | 35–55% of the grid |
| Lanes | 1–3 |
| Buildable cells per route cell | **proposed: 1.5–2.0** — see below |

> **Buildable share is the wrong metric, and the enemy-roster pass proved it (2026-08-07).**
> `crossroads` is 42% buildable — comfortably inside the band — and still permits a defence of 55
> towers against a 19-cell route. That is 4.0 buildable cells per route cell, and no enemy design
> survives it: raising a creep's armour until arrow towers dealt the floor of 1 damage produced zero
> leaks in the late game.
>
> A map can pass every current target and still be unwinnable for the attacker. The proposed
> **1.5–2.0 buildable cells per route cell** is the metric that would have caught it. It is reported by
> `Verify maps` but is not yet a `MapTargets` constant — making it one is `map-density-target`.
> `gauntlet` is at 1.7 and passes; `crossroads` is at 4.0 and does not.
>
> **But density is not sufficient either** (2026-08-07). `gauntlet` cut tower count by 60% and came out
> *easier*: a winding route raises coverage per tower, and gold that cannot buy breadth buys depth
> instead — 1.8 upgrades per tower against 0.77. **Total defence tracks cumulative income, and
> constraining one sink diverts gold to another.** Six passes have now confirmed that from six
> directions. See [the tighter-map pass](reports/2026-08-07-gauntlet-tighter-map-balance.md).
>
> **Broken on purpose (2026-08-07).** The invariant held only because towers were permanent. With
> destructible towers, `crossroads` builds 55.7 towers a run and finishes with **45.8** — the first
> time in this project those two numbers have differed. `hpGrowth` fell 1.09 → 1.08 to pay for it.
> See [destructible towers](reports/2026-08-07-tower-combat-balance.md).

The first four are also `MapTargets` constants in code — read by the balance sim's map report and by the
board editor's live validation panel. **Changing a number here means changing the constant too.** Two
copies of a target is one too many, and this doc is the one people read before they trust the panel.

The board editor warns on all four as you paint, except the maze multiplier, which it estimates on
demand with a greedy search. That estimate is a **lower bound**: over 3× proves the map fails, under 3×
proves nothing.

Maximum mazing above 3× breaks wave timing: waves overlap in ways the tables were never balanced for.

## Not yet measured: difficulty slope

Two maps can hit identical balance numbers and fail completely differently. `gauntlet` flips from 0% of
runs lost to 90% on a **0.005** change in `hpGrowth`; `crossroads` degrades smoothly across the same
range. A cliff fails pillar 4 — a loss you cannot see coming is not explainable — and **nothing here
currently measures it.**

Proposed: sweep `hpGrowth` and report how sharply runs-lost changes. Tracked as `difficulty-slope`.

## A target is necessary, not sufficient

`tower-combat` found a configuration that hit **both** run-level targets — leak 1.3%, runs lost 20% —
and it was the wrong answer: at that tuning only ~5 towers died per run and the numbers were
indistinguishable from the build with no destructible towers at all. The tuning had turned the new
mechanic off while satisfying its metrics.

**When a pass adds a mechanic, measure that the mechanic is still doing something.** The targets here
describe a game that is fun to lose; they cannot tell you whether the thing you just built matters.

## Hard invariants

Not targets. These are checked on every map, every pass, and a failure blocks the change:

1. **Every spawn reaches the goal.** Always, from any legal board state.
2. **No build can fully block a lane.** The sim refuses the build; the map must never depend on the
   player choosing not to try.
3. **Path length at max mazing fits the wave timing budget** — no wave may still be walking when the
   next two have spawned.

## Sim invocation

```bash
dotnet run --project Gridfall.Verify -c Release -- balance --map <map> --runs 200 --seed 1
```

The mode is a bare word — `-- balance`, not `-- --balance`.

Same seed, same run count, before and after. A different seed measures noise and reports it as insight.
