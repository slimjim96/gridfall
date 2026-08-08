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

> **Both runs-lost targets were carried here for months and only one was ever measured (2026-08-07).**
> The balance sim printed a single figure, labelled it `15-30% late`, and checked it against a 0-60%
> band that appears nowhere in this document. Split, the shipped 26.0% was **25.5% early and 0.5%
> late** — the exact inverse of the intent, with lost runs dying at wave 4.3 of 12. Six passes read that
> number as "ok". `balance` now reports the split and the mean wave a lost run died on.
> See [early economy 2](reports/2026-08-07-early-economy-2-balance.md).

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

| Property | Target | Scales with board size? |
|---|---|---|
| Shortest path, unmazed | 18–30 cells | **No — and deliberately not.** See below |
| Spawn-to-goal distance | ≤ 30 cells (`MaxSpawnGoalDistance`) | It *is* the size limit |
| Longest path at maximum mazing | ≤ 3× the unmazed path | Yes, it is a ratio |
| Buildable cells | 35–55% of the grid | Yes, it is a percentage |
| Lanes | 1–3 | No — this is cognitive load, not area |
| Buildable cells per route cell | **proposed: 1.5–2.0** — see below | Yes, it is a ratio |

### The supported board size, and why the path band does not scale (2026-08-07)

The validator permits boards from 8×8 to 64×64. **The balance targets support a much smaller range
than that, and the gap is now stated rather than discovered.**

The 18–30 band is about **time under fire** — how many cells a creep is exposed for against tower
DPS — not about geometry. Scaling it with board size would keep the warning quiet on a 64×64 map
while silently claiming a combat model that nothing has tested.

So the band stays absolute, and its consequence is named: the **geometric floor** of a map is the
Manhattan distance from spawn to goal (exact, because movement is four-way), and it is the shortest
route any map with those endpoints can have. If the floor already exceeds 30, no layout satisfies the
band and the map reports:

```
board too large for the tuned combat model: spawn and goal are 63 cells apart,
over the 30 cap, so no layout can reach the 18-30 path band
```

rather than the old `unmazed path 63 is outside 18-30`, which was true, implied, and read as
"repaint your map" when no painting could help.

**Both shipped maps are unaffected** — crossroads' floor is 19 and gauntlet's is 15.

`Verify maps` also now reports **path ÷ floor**, which is the genuinely size-relative quality: how
much the design lengthens the route beyond the minimum possible. crossroads is `1.0x` — a completely
straight lane, which is worth reading next to its 4.0 density — and gauntlet is `1.9x`.

> **A correction to the record.** The `camera-pan-zoom` release note claimed a 64×64 board reporting
> 89% buildable showed the buildable band was size-absolute. It does not: a percentage is already
> size-relative, and that test board was genuinely almost entirely open. **Only the path band was
> size-absolute.** Fixing what was actually wrong turned out to be smaller than advertised.

**Raising the cap is a balance question, not a constant.** It needs the sim run at that scale, with
wave duration and DPS re-checked — `large-board-balance`.

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
> **Do not promote it alone (2026-08-07).** The map built to satisfy it cannot be balanced at any growth
> rate, and the cause is the thing that made it score well: **the way to lower density is to wall the
> route in, which removes mazing.** A fixed route means one solution, one outcome, and a threshold
> instead of a curve — `gauntlet` finishes 200 runs with sd 0.0 and a range of 20–20 lives. Every
> variant tried that restored route freedom moved density straight back out of band. Density measures
> how much defence a map permits and says nothing about whether the player has a decision; it needs a
> route-variability companion (`route-variance-metric`).
> See [gauntlet's cliff](reports/2026-08-07-gauntlet-cliff-balance.md).
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
>
> **And nearly restored by accident (2026-08-07).** `tower-repair` gave the player a way to undo that
> damage, and at *every legal price* it drove towers lost per run to **0.0** — built and standing equal
> again at 45.6, with both run-level targets still reading ok. Price was never the lever: repair is
> bounded below half a tower's cost by the sell-and-rebuild alternative, and a tower costs 50–90 gold
> against 6,479 earned. Restricting repair to **between waves** gives 5.8 lost per run.
> See [tower repair](reports/2026-08-07-tower-repair-balance.md).
>
> **And nearly restored a second time, by a different route (2026-08-07).** Selling refunded half of a
> tower's *cost* regardless of damage, so cashing out a wreck paid the same as cashing out a pristine
> tower. A player who sold every doomed tower took destructions from 5.8 to **0.0** — and `towers lost`
> could not see it, because a sold tower is not a destroyed one. Refunds now scale with remaining
> health. See [salvage value](reports/2026-08-07-salvage-value-balance.md).

The first four are also `MapTargets` constants in code — read by the balance sim's map report and by the
board editor's live validation panel. **Changing a number here means changing the constant too.** Two
copies of a target is one too many, and this doc is the one people read before they trust the panel.

The board editor warns on all four as you paint, except the maze multiplier, which it estimates on
demand with a greedy search. That estimate is a **lower bound**: over 3× proves the map fails, under 3×
proves nothing.

Maximum mazing above 3× breaks wave timing: waves overlap in ways the tables were never balanced for.

## Difficulty slope, and the spread that explains it

Two maps can hit identical balance numbers and fail completely differently. `gauntlet` flips from 0% of
runs lost to 95% on a **0.005** change in `hpGrowth`; `crossroads` degrades across a range.

**Measured since 2026-08-07:** `balance` reports the standard deviation and range of lives left, not
just the mean. That is the number that separates the two cases.

| | mean lives | sd | range |
|---|---|---|---|
| crossroads | 7.6 | **6.8** | **0–20** |
| gauntlet | 20.0 | **0.0** | **20–20** |

A mean can cross zero gradually; a distribution with no width crosses it all at once. **That is what a
cliff is.** Leak rate moves perfectly smoothly across gauntlet's cliff, so no summary of the mean could
have caught it.

**Read the spread before the mean** when judging whether a map has a difficulty curve at all. A low
spread means the map has one solution, and a map with one solution cannot be tuned — see
[gauntlet's cliff](reports/2026-08-07-gauntlet-cliff-balance.md).

Still proposed: sweep `hpGrowth` and report how sharply runs-lost changes (`difficulty-slope`).

## A target is necessary, not sufficient

`tower-combat` found a configuration that hit **both** run-level targets — leak 1.3%, runs lost 20% —
and it was the wrong answer: at that tuning only ~5 towers died per run and the numbers were
indistinguishable from the build with no destructible towers at all. The tuning had turned the new
mechanic off while satisfying its metrics.

**When a pass adds a mechanic, measure that the mechanic is still doing something.** The targets here
describe a game that is fun to lose; they cannot tell you whether the thing you just built matters.

`tower-repair` then found the converse, and it is the sharper half. Repair satisfied every target while
driving tower destruction — the *previous* slice's entire result — to exactly zero. Nothing in this
document could see it, because the defence on the board came out the same either way.

**Measure that the previous pass's mechanic is still doing something too.** A new mechanic can hit every
target while quietly deleting the one before it. `balance` prints the guard number on every run for
exactly this reason: the number that catches a deletion has to be on screen by default, because nobody
thinks to go looking for it.

Then `salvage-value` deleted the same mechanic again, past the guard. `towers lost` counts destructions,
and a tower **sold** at 1 hp is not destroyed — so it read 0.0 while the same investment was just as
gone.

| Pass | Failure the targets missed | Number added |
|---|---|---|
| `tower-combat` | Tuning that hit targets while the mechanic did nothing | towers built vs standing |
| `tower-repair` | A new mechanic deleting the previous one | towers lost |
| `salvage-value` | The **same** deletion by a route that metric did not cover | **gold destroyed** |

The lesson is not "add a metric per pass". Each of these was **too specific**, and the fix each time was
to measure one level more abstractly — from tower counts, to destructions, to the gold those
destructions represent.

**`gold destroyed` is now the first number to check** when a pass touches towers. It counts
unrecoverable investment whether the tower was destroyed or sold at a discount, which is what the
invariant is actually about.

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
