# Balance Targets

The numbers a balance pass is measured against. Load this before touching any value.

These are targets, not laws — but a change that misses one is reverted or argued for explicitly in the
report. "Missed the target, kept it anyway, no reason given" is not an outcome this project accepts.

## Run-level targets

Measured over 200 headless runs per map, fixed seed, competent-play policy.

| Metric | Target | Fail if |
|---|---|---|
| Runs lost (**first half** of the table) | 0–5% | > 10% |
| Runs lost (**second half**) | 15–30% | < 5% (trivial) or > 50% (unfair) |
| **Waves that can kill you** | ≥ 3 | 1 (a wall, not a curve) |
| Leak rate, overall | ≤ 4% | > 8% |
| Leak rate, any single wave | ≤ 15% | > 25% |
| Time-to-clear, per wave | 20–45 s | > 60 s (grindy) |
| Idle gold at the halfway wave | 0–2 station costs | > 4 (nothing worth buying) |
| Idle gold at the last wave | 0–3 station costs | > 6 |

**The split is proportional, not a wave number.** `Verify balance` computes it as `waveCount / 2`, so a
12-wave table splits 1–6 and 7–12 and prints the actual range in its own output. The bands have always
meant *"the first half should rarely kill you and the second half should"*; a hardcoded wave 10 turned
that into a two-wave window on a twelve-wave table, where no level could sit in band except by
accident. Growing the tables to 20 waves was the alternative and was **not** chosen (2026-08-08).

> **Restated for the twelve waves that exist (2026-08-08).** The target used to read "waves 11–20"
> while every table in `content-data/waves/` had 12 waves, so the late window was waves 11 and 12.
> 15–30% over ten waves is a gentle per-wave rate; over two it needs one wave to be near a coin flip,
> which is not somewhere a level can sit. `comb` proved it: every global knob is a cliff around wave
> 12, and `waveClearGold` is not even monotone — 25→42%, 35→0%, 45→52%, 60→0%.
>
> The band **values are unchanged** and were never the problem. "How often does a competent beginner
> lose a run" is a property of the game, not of how long the window is; only the window was wrong. The
> split is now `waveCount / 2` and follows the table.

> **"Waves that can kill you" is new, and is the target `comb` actually fails.** Runs lost says how
> often you lose; this says whether losing is a difficulty curve or a single wall. Measured today:
> `crossroads` 5 of 12, everything else **1 or 0**. `comb` loses 42% of runs and every one of them
> dies at wave 12 — a level with one lethal wave is a gate, and no global knob can widen it because
> every knob moves that one wave through the threshold at once. Spreading the lethality is the fix,
> and it is a wave-composition job.
>
> This also unblocks `route-variance-metric`, which had been regressing map geometry against sd of
> lives left — a statistic that cannot tell a curve from a coin flip. See
> [example-levels balance](reports/2026-08-08-example-levels-balance.md).

> **Both runs-lost targets were carried here for months and only one was ever measured (2026-08-07).**
> The balance sim printed a single figure, labelled it `15-30% late`, and checked it against a 0-60%
> band that appears nowhere in this document. Split, the shipped 26.0% was **25.5% early and 0.5%
> late** — the exact inverse of the intent, with lost runs dying at wave 4.3 of 12. Six passes read that
> number as "ok". `balance` now reports the split and the mean wave a lost run died on.
> See [early economy 2](reports/2026-08-07-early-economy-2-balance.md).

**Idle gold** is the interesting one. High idle gold means the player has money and no decision — the
economy has stopped generating choices, which fails pillar 5 before it fails any math.

## Station targets

| Property | Target |
|---|---|
| Cost spread across the roster | Cheapest : most expensive ≤ 1 : 6 |
| DPS-per-gold spread at equal tier | Within ±15% |
| Share of a roster used in a winning run | ≥ 4 of 8 distinct stations |
| Any single station's presence in winning runs | ≤ 70% (a must-pick is a design failure) |

A station outside the DPS-per-gold band must earn it with utility — slow, splash, reveal, chain. If it
cannot name the utility, the number is wrong.

> **Both roster targets fail, and neither had ever been measured (2026-08-09).** Nothing printed which
> stations a run actually bought. `Verify balance` now reports a **`station mix`** line, and on all
> twelve shipped boards it reads `arrow-station 100%, cannon 0%`. Presence target: ≤ 70%. Measured:
> **100%**.
>
> This is not the harness failing to try. `PlayPolicy` now ranks stations by *effective* serving per
> gold against the visitors it has met, and it will hold gold for a station it cannot yet afford —
> both fixes shipped, and on a husk-heavy board it does buy cannons. The shipped wave tables simply
> never make the cannon the better buy: the crossover is at average fussiness **4**, and the tables
> peak at **1.53** on wave 12. The husk is 16.5% of that wave by appetite and would need ~48%.
>
> **DPS-per-gold is therefore not a property of a station.** It is a property of a station against a
> mix, and the two shipped stations swap places at fussiness 4. The ±15% band above is written as if
> one number existed; it needs restating in terms of a reference mix before it can be checked.
> See [policy fussiness](reports/2026-08-09-policy-fussiness-balance.md).

## Visitor targets

| Property | Target |
|---|---|
| Archetypes per map | 4–7 |
| HP growth, wave to wave | 1.10–1.18× — **disputed, see below** |
| Speed spread across archetypes | 0.6× – 1.8× of base |
| Waves where a single archetype is > 70% of the visitors | ≤ 3 per run |
| **`fussiness`, per archetype** | ≤ **11** — see below |
| **A fussy archetype's share of a wave, by appetite** | ≥ **48%** if it is meant to change the buy |

> **Fussiness has a ceiling and a floor, and both are arithmetic (2026-08-09).**
>
> *Ceiling.* Fussiness subtracts from every station's hit, not just the fast one. Once it reaches
> `serving - 1` of the cheapest station — 11, against the arrow station's 12 — that station is floored
> at 1 per hit and cannot get worse, while the burst station keeps losing damage. **Past 11, raising
> `fussiness` makes burst a *worse* answer, not a better one.** `husk` is at 8, inside the ceiling.
>
> *Floor.* One armoured archetype in a mixed wave changes nothing unless it is most of the wave's
> **appetite**. Against the shipped two-station roster the crossover is average fussiness 4, which
> `husk` at 8 reaches at 48% share. It currently sits at 16.5% on wave 12, the most armoured wave in
> the repo, and no wave in any table crosses — asserted in
> `PolicyFussinessTests.NoShippedWaveTableEverReachesTheCrossover`.
>
> Appetite, not head count: `husk` is 120 and `runner` 60, so half the appetite is a third of the
> bodies. "One visitor in five is a husk" sounds like plenty and is not close.

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
> **Vindicated, largely (2026-08-07.)** That measurement was taken before station upgrades existed and
> before `startingGold` was fixed. With the economy working, the playable band moved and the shipped
> value is now **1.08** — just below the disputed 1.10 rather than a tenth of it. **Not editing the
> target to match the first measurement was the right call**, and this is the evidence. Resolve the
> remaining gap as `hp-growth-target`.
>
> It moved *down* again, 1.09 → 1.08, when destructible stations shipped: difficulty from losing stations
> substitutes for difficulty from visitor hitpoints. The band is a property of the whole system, not of
> the visitors alone. See
> [income vs difficulty](reports/2026-08-07-income-vs-difficulty.md).

## Wave pacing — `prepTicks`, `midWaveBuildPercent`, `earlyCallGoldPerSecond` (2026-08-07)

Three knobs on a wave table, **all defaulting to the original behaviour**. Built to make the gap
between waves a resource. Measured on `crossroads`, 150 runs, and **none of the three does what it
was meant to yet.**

| prep | mid-wave % | early gold/s | Runs lost | Lives |
|---|---|---|---|---|
| — | 100 | — | **27.3%** | 7.8 |
| 300 | 100 | — | **27.3%** | 7.8 |
| 300 | 110 | 3 | 16.0% | 9.8 |
| 300 | 125 | — | 63.3% | 4.5 |
| 300 | 125 | 3 | 36.7% | 6.7 |
| 300 | 150 | 3 | 81.3% | 3.1 |
| — | 150 | — | 86.7% | 2.5 |

**1. A prep window alone changes nothing — byte-identical to baseline.** Income is bounty-only, so
nothing is earned between waves, and a player who has already spent down has nothing to do with the
time. The pause is dead time. **Wave-clear income is the missing prerequisite**, not a nicety: until
gold arrives during the gap, no timer can make the gap matter.

**2. The mid-wave premium is a cliff, not a texture.** 100 → 27%, 125 → 63%, 150 → 87%. A 25% price
bump more than doubles the loss rate, because the policy builds continuously and pays it on most
stations. Any shipped value lives between 100 and 125, and wants finer steps than were tested.

**3. The early-call bonus is unconditional income, not a trade.** At 3 gold/second of a 10s window it
is 30 gold a wave, worth **~27 points** of loss rate (63.3% → 36.7% at the same premium). A
spent-down player always calls early, so there is no cost to weigh against the payment. It only
becomes a decision once there is something to buy during the prep window — i.e. after fix 1.

### With `waveClearGold` added, measured together (150 runs)

`waveClearGold` pays on clearing a wave, before the prep timer arms — income that arrives *in the
gap*, which the table above showed was the missing prerequisite.

| clear | prep | mid % | Runs lost | Lives |
|---|---|---|---|---|
| — | — | 100 | 27.3% | 7.8 |
| 25 | 0 / 30 / 300 | 100 | **4.0% (identical at all three)** | 12.1 |
| 80 | 0 / 300 | 100 | **0.7% (identical at both)** | 18.4 |
| 25 | 300 | 115 | **24.0%** | 8.4 |
| 25 | 300 | 125 | 40.0% | 6.9 |
| 40 | 300 | 125 | 20.0% | 8.8 |

**`prepTicks` is unmeasurable by the sim, at any value including 0.4 seconds.** The policy spends
down and *then* calls the wave, so it already has unlimited prep and a timer never binds. Prep time is
a constraint on how fast a *human* decides, and this harness decides instantly. **It has to be tuned
by playing.** Every prep row above is byte-identical to its prep-less twin.

**`waveClearGold` is a strong lever.** 25/wave takes 27.3% → 4.0% on its own. Paired against a
premium it lands back in band: **clear 25 + mid 115 = 24.0%**, inside the 15–30% late target and
within noise of the 27.3% baseline — but now income arrives in the gap and reacting late costs.

### Shipped on crossroads (2026-08-08)

Enabling `midWaveBuildPercent 115` **broke two verification seeds**: `sappers` and `repair` finished
at 5 stations and 0 lives instead of 28 stations and 20 lives. Isolated to the premium — with it at 100
both recover exactly.

The cause is that both seeds build almost entirely *during* waves, so they pay the premium on nearly
every station. That is the mechanic working as designed, and it is also a harness that models the one
playstyle the mechanic exists to discourage.

**The seeds were rewritten to build between waves**, which is what a premium-aware player does
anyway, and both recover: 28 stations and 20 lives, with `repair` still holding its `worstHp 59%` case.

`crossroads` now ships `waveClearGold 25`, `midWaveBuildPercent 115`, `prepTicks 300`. Re-measured
after the change: **24.0% runs lost, 8.4 lives** — unchanged from the tuning pass. Determinism trace
and all three gameplay baselines re-recorded.

`prepTicks 300` (10s) is a **placeholder**, and the only one of the four the sim cannot judge. It has
to be set by playing. `earlyCallGoldPerSecond` stays 0 until it is, because a bonus for skipping a
window nobody is constrained by is just income.

## Wave variance — `waveVariance` (2026-08-07)

`"waveVariance": 0-100` on a wave table jitters **when each group of a wave starts**, drawn from the
run seed. Nothing else: composition, counts and spacing between spawns are untouched, so the authored
difficulty curve survives and a varied wave stays explicable.

Jitter is a delay of up to `1.2s × variance`, never an advance. Groups authored to start together get
reordered; the pressure changes shape without the budget changing.

**Default is 0, and at 0 the sim draws no random numbers at all.** That is load-bearing rather than an
optimisation — `SimRandom`'s state is hashed, so a draw taken while the feature is off would change
every recorded trace for no behaviour.

### Measured, `crossroads`, 150 runs

| | Runs lost | Lives left |
|---|---|---|
| `waveVariance` 0 | 27.3% | 7.8, sd 7.1 |
| `waveVariance` 100 | 30.0% | 7.1, sd 7.2 |

**+2.7pp against a standard error of ~3.7pp — not distinguishable from noise at this sample size.**
The direction was consistent across every measurement taken (0 → 50 → 100 rose monotonically at 30
runs too), and the mechanism is plausible: jitter can overlap two groups that were authored apart, and
overlap hurts more than a gap helps. Treat it as *approximately* neutral with a possible small
hardening, not as proven neutral.

> **A caution about run counts.** The same measurement at 30 runs put the baseline at 23.3% and the
> gap at +6.7pp. At 150 runs the baseline is 27.3% and the gap is +2.7pp. **A 4-point swing in the
> baseline came from sample size alone.** Thirty runs is enough to separate 0% from 25%; it is not
> enough to resolve a few points, and numbers in this document quoted from 30-run passes should be
> read that way.

## Run length — `runWaves`, and why it is not a length dial (2026-08-07)

A wave table may carry `"runWaves": N` to play only its first N waves. It is **truncation, not
re-tuning**: the HP curve is authored per wave index, so a shorter run is the same waves stopping
earlier.

Measured on `crossroads`, 30 runs, beginner policy:

| `runWaves` | Runs lost | Lives left |
|---|---|---|
| 8 | **0.0%** | 18.7, sd 2.8 |
| 10 | **0.0%** | 18.5, sd 2.9 |
| 12 (authored) | **23.3%** | 8.7, sd 7.0, range 0–20 |

**Every loss on crossroads happens in waves 11–12.** Truncating to 10 does not shorten the game by
a sixth — it removes the entire losing condition and produces the `gauntlet` failure: a map that
cannot be lost, with no spread of outcomes.

So: **shortening a run means re-authoring the curve, not truncating it.** `appetiteGrowth` and
`appetiteGrowthFrom` are the knobs — a steeper rate from an earlier wave reaches the same threat in fewer
waves. `runWaves` is for testing and for deliberately gentle boards, and any use of it in shipped
content needs a balance run beside it. Re-authoring a short curve is `short-run-curve`.

## Station coverage — the metric that links range to board size (2026-08-08)

`Verify maps` reports **cover**: the share of the route one cheapest station reaches from its best
buildable cell.

| map | size | path | cover | useful |
|---|---|---|---|---|
| `crossroads` | 20×9 | 19 | 25% | 73% |
| `gauntlet` | 10×10 | 29 | 20% | 100% |

> **Correction (2026-08-08).** The `cover` figures published earlier that day — crossroads 22% → 11%,
> gauntlet 37% → 18% — were computed against **every walkable cell rather than the actual route**, so
> they measured coverage of the whole board. `Verify maps` now walks the route down the distance field
> and the numbers above are the corrected ones. The *conclusion* the old figures supported still holds
> (halving range halves coverage) but the values themselves were wrong. The bug surfaced only because
> a second metric built on the same list read 100% everywhere, which was obviously impossible.

**`useful`** is the share of buildable cells within range of the route at all. It is a **viability
floor, not a difficulty predictor**: across twelve maps the middle of its range does not order by
outcome, but the bottom does. `spiral` sits at 43% — 89 buildable cells forming a courtyard the visitors
never approach — passes every other band, and lost **100% of 150 runs**. Nothing else in the repo
would have caught that.

**What halving cover cost.** `arrow-station` range 3.0 → 2.0 (area scales with r², so ~55% less
reach) took `crossroads` from **24.0% runs lost to 80.7%** at an unchanged wave table. Recovered with
`appetiteGrowth 1.10 from wave 6` → **20.0%**, in band. The rate stays inside the documented 1.10–1.18
band; delaying the ramp to wave 6 did the work, not a rate below band.

`gauntlet` was **unaffected: still 0.0% lost, sd 0.2.** Halving every station's reach did not make a
walled-in route interesting, which is the clearest evidence yet that its problem is route freedom and
not tuning — see [gauntlet's cliff](reports/2026-08-07-gauntlet-cliff-balance.md) and
`route-variance-metric`.

**Station range is fixed in cells; boards are not.** So the same station covers a shrinking share of the
route as a board grows, and a wave tuned on a 20×9 board is a different problem on a 40×40 one — the
visitors spend proportionally longer outside every station's reach.

Buildable-per-route-cell measures how much defence a map *permits*. Cover measures how much one station
*buys*. Wave design depends on the second, and until now nothing reported it.

**No target band yet, deliberately.** Two maps is not a sample, and the honest next step is to measure
cover against runs-lost across several boards before drawing a line. Follow-up `coverage-target`.

> This is also the concrete form of the large-board problem. `MaxSpawnGoalDistance` caps boards at a
> spawn-goal distance of 30 because the combat model was not tuned past it; cover is *why*. A board
> twice as long does not just take longer, it gives every station half the job.

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

The 18–30 band is about **time under fire** — how many cells a visitor is exposed for against station
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

> **Buildable share is the wrong metric, and the visitor-roster pass proved it (2026-08-07).**
> `crossroads` is 42% buildable — comfortably inside the band — and still permits a defence of 55
> stations against a 19-cell route. That is 4.0 buildable cells per route cell, and no visitor design
> survives it: raising a visitor's fussiness until arrow stations dealt the floor of 1 damage produced zero
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
> **But density is not sufficient either** (2026-08-07). `gauntlet` cut station count by 60% and came out
> *easier*: a winding route raises coverage per station, and gold that cannot buy breadth buys depth
> instead — 1.8 upgrades per station against 0.77. **Total defence tracks cumulative income, and
> constraining one sink diverts gold to another.** Six passes have now confirmed that from six
> directions. See [the tighter-map pass](reports/2026-08-07-gauntlet-tighter-map-balance.md).
>
> **Broken on purpose (2026-08-07).** The invariant held only because stations were permanent. With
> destructible stations, `crossroads` builds 55.7 stations a run and finishes with **45.8** — the first
> time in this project those two numbers have differed. `appetiteGrowth` fell 1.09 → 1.08 to pay for it.
> See [destructible stations](reports/2026-08-07-station-combat-balance.md).
>
> **And nearly restored by accident (2026-08-07).** `station-repair` gave the player a way to undo that
> damage, and at *every legal price* it drove stations lost per run to **0.0** — built and standing equal
> again at 45.6, with both run-level targets still reading ok. Price was never the lever: repair is
> bounded below half a station's cost by the sell-and-rebuild alternative, and a station costs 50–90 gold
> against 6,479 earned. Restricting repair to **between waves** gives 5.8 lost per run.
> See [station repair](reports/2026-08-07-station-repair-balance.md).
>
> **And nearly restored a second time, by a different route (2026-08-07).** Selling refunded half of a
> station's *cost* regardless of damage, so cashing out a wreck paid the same as cashing out a pristine
> station. A player who sold every doomed station took destructions from 5.8 to **0.0** — and `stations lost`
> could not see it, because a sold station is not a destroyed one. Refunds now scale with remaining
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
runs lost to 95% on a **0.005** change in `appetiteGrowth`; `crossroads` degrades across a range.

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

Still proposed: sweep `appetiteGrowth` and report how sharply runs-lost changes (`difficulty-slope`).

## A target is necessary, not sufficient

`station-combat` found a configuration that hit **both** run-level targets — leak 1.3%, runs lost 20% —
and it was the wrong answer: at that tuning only ~5 stations died per run and the numbers were
indistinguishable from the build with no destructible stations at all. The tuning had turned the new
mechanic off while satisfying its metrics.

**When a pass adds a mechanic, measure that the mechanic is still doing something.** The targets here
describe a game that is fun to lose; they cannot tell you whether the thing you just built matters.

`station-repair` then found the converse, and it is the sharper half. Repair satisfied every target while
driving station destruction — the *previous* slice's entire result — to exactly zero. Nothing in this
document could see it, because the defence on the board came out the same either way.

**Measure that the previous pass's mechanic is still doing something too.** A new mechanic can hit every
target while quietly deleting the one before it. `balance` prints the guard number on every run for
exactly this reason: the number that catches a deletion has to be on screen by default, because nobody
thinks to go looking for it.

Then `salvage-value` deleted the same mechanic again, past the guard. `stations lost` counts destructions,
and a station **sold** at 1 hp is not destroyed — so it read 0.0 while the same investment was just as
gone.

| Pass | Failure the targets missed | Number added |
|---|---|---|
| `station-combat` | Tuning that hit targets while the mechanic did nothing | stations built vs standing |
| `station-repair` | A new mechanic deleting the previous one | stations lost |
| `salvage-value` | The **same** deletion by a route that metric did not cover | **gold destroyed** |

The lesson is not "add a metric per pass". Each of these was **too specific**, and the fix each time was
to measure one level more abstractly — from station counts, to destructions, to the gold those
destructions represent.

**`gold destroyed` is now the first number to check** when a pass touches stations. It counts
unrecoverable investment whether the station was destroyed or sold at a discount, which is what the
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
