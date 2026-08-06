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
| HP growth, wave to wave | 1.10–1.18× |
| Speed spread across archetypes | 0.6× – 1.8× of base |
| Waves where a single archetype is > 70% of the creeps | ≤ 3 per run |

HP growth above 1.18× produces the classic wall: the player is fine, then suddenly is not, with nothing
to react to. That fails pillar 4.

## Map targets

| Property | Target |
|---|---|
| Shortest path, unmazed | 18–30 cells |
| Longest path at maximum mazing | ≤ 3× the unmazed path |
| Buildable cells | 35–55% of the grid |
| Lanes | 1–3 |

These four are also `MapTargets` constants in code — read by the balance sim's map report and by the
board editor's live validation panel. **Changing a number here means changing the constant too.** Two
copies of a target is one too many, and this doc is the one people read before they trust the panel.

The board editor warns on all four as you paint, except the maze multiplier, which it estimates on
demand with a greedy search. That estimate is a **lower bound**: over 3× proves the map fails, under 3×
proves nothing.

Maximum mazing above 3× breaks wave timing: waves overlap in ways the tables were never balanced for.

## Hard invariants

Not targets. These are checked on every map, every pass, and a failure blocks the change:

1. **Every spawn reaches the goal.** Always, from any legal board state.
2. **No build can fully block a lane.** The sim refuses the build; the map must never depend on the
   player choosing not to try.
3. **Path length at max mazing fits the wave timing budget** — no wave may still be walking when the
   next two have spawned.

## Sim invocation

```bash
dotnet run --project Gridfall.Verify -- --balance --map <map> --runs 200 --seed 1
```

Same seed, same run count, before and after. A different seed measures noise and reports it as insight.
