# Core Foundation — Requirements

**Slug:** `core-foundation` · **Status:** done · **Owner:** design-lead

## In One Sentence

The game can be simulated: creeps spawn, walk a map, get shot, die or leak, and the whole run
reproduces exactly from the same inputs.

## Pillar Check

| Pillar | Supports / Neutral / Fights | Note |
|---|---|---|
| 1 · The maze is the game | **Supports** | Pathing recomputes on grid change from day one. |
| 2 · Legible at a glance | Neutral | No renderer in this slice. |
| 3 · Deterministic, therefore fair | **Supports** | This slice *is* the determinism machinery. |
| 4 · Every loss is explainable | **Supports** | Every consequence is an ordered, tick-stamped event. |
| 5 · Small numbers, big decisions | Neutral | Content is placeholder here. |

## TD Checklist

| Question | Answer |
|---|---|
| **Player fantasy** | None directly — this is the substrate every later feature stands on. |
| **Pathing** | Flow field, rebuilt on a dirty grid; builds that would seal a lane are refused. |
| **Economy** | Gold in from bounties, out from builds. Lives lost on leak. |
| **Wave pressure** | Waves spawn from a table on a tick schedule. |
| **Failure state** | Lives reach zero. Reported as an event; the sim does not stop itself. |

## Constraints

1. `Gridfall.Core` references no Godot type and contains no `float` or `double`.
2. Same map + same command trace + same tick count ⇒ byte-identical state hash, on any machine.
3. No allocation in the tick loop after construction.
4. A build that would leave any spawn unable to reach the goal is refused before the grid changes.

## Acceptance Criteria

1. A sim constructed from a map, content, and a seed advances by `Tick()` and never touches a clock.
2. Two runs with identical inputs produce identical per-tick hashes for the whole run.
3. `Restore(Snapshot())` then N ticks matches N ticks without the round trip, hash for hash.
4. A build on a cell that would seal the only route is refused, the grid is unchanged, gold is unspent,
   and a `BuildRejected` event is emitted.
5. Where two routes are equal cost, every creep takes the same one, and the same one across runs.
6. A creep entering a cell finishes crossing it before turning, even if the field changed mid-crossing.
7. Two towers killing the same creep on the same tick yield one death and one bounty.
8. Mutating any hashed field changes the hash — proven per field, not asserted in general.
9. `Gridfall.Core` contains no `float`, `double`, `System.Random`, or `DateTime` — checked, not claimed.
10. A wave runs start to finish: creeps spawn on schedule, are shot, die or leak, and gold and lives move.
