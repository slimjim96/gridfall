# Tower Combat — v1

**Slug:** `tower-combat` · **Status:** done · **Verified at trace:** `73843cd13ed4dad6`

## What Shipped

Combat runs both ways. Enemies can attack towers while they walk, and a tower reduced to zero
structure health is destroyed.

- **`EnemyAttackSystem`**, phase 5b. Creeps with `attackDamage > 0` pick the **nearest** tower in range
  (ties by lowest entity id) and buffer damage. They never stop walking to do it.
- **Towers fire first** within phase 5, so a tower destroyed this tick still gets its shot off. Fixed,
  documented, and load-bearing ([ADR-0006](../../engine-systems/decisions/ADR-0006-enemy-attacks-in-phase-five.md)).
- **`TowerHp` and `CreepAttackCooldown`** on `SimState` — hashed, snapshotted, swap-remove-safe, each
  with its own test.
- Tower damage flows through **its own buffer**, applied in phase 7 alongside creep damage. Two sappers
  destroying one tower on the same tick is deterministic for the same reason simultaneous kills are.
- Destruction frees the cell. Phase 2 has already run, so **pathing updates on the next tick** — one
  tick of stale routing, documented rather than papered over.
- **`sapper`**, the fifth enemy archetype: 90 hp, armour 2, 22 damage per hit, 1.2 s cooldown, 1.6-cell
  reach. Waves 5–12 on `crossroads`.
- Nine phases are still nine.

## Player-Facing Change

A tower is no longer a permanent purchase. Gold spent can be lost, so position now carries risk as
well as value — and unlike every previous gold sink, this one is not the player's choice.

Reachable in the running game today: sappers appear from wave 5, hit flash and darken the towers they
chew on, and the tower collapses when it dies.

## The invariant this was for

| | Before | After |
|---|---|---|
| Towers built per run | 52.4 | 55.7 |
| Towers standing at end | 52.4 | **45.8** |
| `hpGrowth` required | 1.09 | **1.08** |
| Leak / runs lost | 1.1% / 20% | 1.3% / 28.5% |

Built and standing were the same number for the project's entire history. **Seven balance passes said
"total defence tracks cumulative income, because towers are permanent."** They are not permanent now.

`hpGrowth` came *down*: difficulty from destruction substitutes for difficulty from enemy hitpoint
inflation. That trade is the design win, not the balance numbers — a loss caused by something visibly
eating your towers is explainable in a way a rising HP bar is not.

## Visible State

Two new cues, both persistent state on `IUnitView` rather than clips, because a clip does not survive a
reload or a recreated view:

| State | Cue |
|---|---|
| Sapper identity | Point-down wedge, tall and narrow, and the roster's **only** red |
| Tower health | Darker and redder as it falls. Darkening is the channel that survives greyscale |

Verified in `presentation/docs/sapper-baseline.png`: a 28%-health tower reads as dark reddish-brown
beside bright orange neighbours.

## New Tuning Knobs

| Knob | Owner | Default set? |
|---|---|---|
| `hp` on a tower def | content-data | **Yes** — arrow 800, cannon 1440, swept over 200 runs |
| `attackDamage` on an enemy def | content-data | **Yes** — sapper 22. `0` (never attacks) is the default for every other enemy |
| `attackCooldown` | content-data | Placeholder — 1.2 s, not independently swept |
| `attackRange` | content-data | Placeholder — 1.6 cells, not independently swept |

`hp` defaults to 100 and `attackDamage` to 0, so **no existing content changed meaning.**

## Why 800 HP against 22 Damage

The ratio reads wrong beside every other number in the game, and it is correct. Tower loss is driven by
**throughput** — 62 sappers over twelve waves — not by damage per hit. Sweeping `attackDamage` alone
never worked: even 10 lost 90% of runs. Do not tidy these two numbers toward each other without
re-running the sweep; it is recorded in
[engine guide 07](../../docs/engine-guide/07-content-loading.md).

## Also Fixed

- **Shot mode replayed the last tick's events on every rendered frame**, because the gate was a flag
  only `Advance()` clears and shot mode never calls it. It pinned damaged towers in a permanent white
  hit flash. `board-baseline.png` is refreshed as a result — the simulation hash is unchanged, so the
  difference is purely the stuck flash going away.
- **The launchers now build the C# first.** Godot loads whatever assembly is already in `.godot/mono`,
  so an edited script silently runs as its previous version. Three captures were rendered by code that
  had never been compiled, one of which did not compile at all.

## Follow-Ups Not Done

| Item | Workspace | Slug |
|---|---|---|
| Sappers on `gauntlet` — it has no destructible-tower pressure and its cliff is untouched | content-data | `gauntlet-sappers` |
| Repair, so destruction has an answer other than rebuilding | game-design | `tower-repair` |
| Wave 3 leaks 14.1%, worst wave for four passes running | content-data | `early-economy-2` |
| Sweep `attackCooldown` and `attackRange` independently | content-data | `sapper-tuning` |

## Known Not Verified

- Whether a player *notices* towers dying during play, as opposed to in a still frame. Needs a human
  (`destruction-feedback`).
- Whether "protect your towers" reads as a real decision, or just as attrition.
