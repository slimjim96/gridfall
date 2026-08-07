# 07 · Content Loading

JSON in `content-data/` is the source of truth for every number in the game. This chapter is how it
becomes runtime data, and what the map format is — which is also the board editor's output format
([`tooling/docs/board-editor-spec.md`](../../tooling/docs/board-editor-spec.md)).

## The path a number takes

```
content-data/towers/frost-spire.json        authored by hand or by the editor
        │
        ├─ ContentLoader.Load()             validate → parse → Fix32
        ▼
   ContentSet  (immutable, id-indexed)      handed to the Sim constructor
        │
        ├─ Sim reads defs by index          never by name, never by dictionary lookup in the tick loop
        ▼
   godot/resources/generated/*.tres         generated for the view layer only, never hand-edited
```

Two rules that keep this honest:

- **Core never touches the filesystem.** `ContentLoader` runs before the `Sim` is constructed; the sim
  receives a fully-built `ContentSet`. This is what lets the harness build content in memory and the
  game load it from disk without the sim knowing which happened.
- **`.tres` files are generated.** JSON is authored. Editing a `.tres` puts a value in the game that no
  balance report ever saw.

## Defs

```csharp
public sealed class TowerDef
{
    public readonly ushort Index;         // dense, assigned at load in sorted-name order
    public readonly string Id;            // "frost-spire" — for authoring and logs only
    public readonly int    Cost;
    public readonly Fix32  Range;
    public readonly Fix32  RangeSquared;  // precomputed at load — never recompute in the tick loop
    public readonly int    Damage;
    public readonly int    CooldownTicks; // ticks, not seconds. Content authors write seconds.
    public readonly int    Hp;            // structure health. Towers are destructible.
    public readonly TargetRule Targeting;
}
```

Three things to notice:

1. **`Index`, not `Id`, is used at runtime.** Ids are strings; strings mean hashing and comparison in
   the hot path. Indices are assigned by sorting ids alphabetically at load, so the mapping is stable
   across runs and machines.
2. **Derived values are computed once at load.** `RangeSquared` is the canonical example.
3. **Time is in ticks inside Core.** The JSON says `"cooldown": 0.8` (seconds, human-friendly); the
   loader converts to `24` ticks. Rounding happens once, at load, deterministically.

## Armour

`"armour": 8` on an enemy def. Flat damage reduction applied **per hit** in phase 7, floored at 1:

```csharp
int amount = Math.Max(1, record.Amount - enemy.Armour);
```

Per hit rather than per tick total, and flat rather than percentage — both deliberate. A percentage
scales every tower equally and changes no decisions; flat punishes many-small-hits and rewards
few-big-hits. Applying it to a tick's total would leave rapid-fire towers almost unaffected, which is
the opposite of the intent.

The floor of 1 means no tower is ever useless against an enemy, only inefficient. An enemy immune to a
tower is a soft-lock waiting to happen.

Armour does **not** scale with `hpGrowth`. A growing armour value becomes immunity.

## Enemy attacks and tower health

Enemies can destroy towers. Four fields, all optional:

| Field | On | Default | Meaning |
|---|---|---|---|
| `hp` | tower | `100` | Structure health. Reaching 0 destroys the tower and frees its cell. |
| `attackDamage` | enemy | `0` | Damage per hit. **`0` means the enemy never attacks** — this is what keeps every pre-existing enemy unchanged. |
| `attackCooldown` | enemy | `1.0` s | Seconds between hits, converted to ticks at load. |
| `attackRange` | enemy | `1.5` | Cells. Stored as `AttackRangeSquared`. |

`AttacksTowers => AttackDamage > 0` is the only switch. Resolution happens in phase 5b and damage
applies in phase 7, exactly like creep damage — see [chapter 02](02-tick-loop.md) and
[ADR-0006](../../engine-systems/decisions/ADR-0006-enemy-attacks-in-phase-five.md).

Tower `hp` is large relative to `attackDamage` (shipped: 800 against 22). That ratio is deliberate —
the balance sweep found tower loss is driven by **attack throughput** across many attackers, not by
damage per hit, so a tower must survive a lot of individual chips. Do not "tidy" these numbers toward
each other without re-running the sweep.

## Upgrades

```json
"upgrades": [
  { "cost": 110, "damageMultiplier": 2.0, "rangeMultiplier": 1.0 },
  { "cost": 240, "damageMultiplier": 4.0, "rangeMultiplier": 1.15 }
]
```

Levels above the base; an absent array means the tower cannot be upgraded. Damage and squared range for
every level are resolved **once at load** into `UpgradeLevel`, for the same reason `RangeSquared` is —
the tick loop must never multiply to find a tower's stats.

The design rule these numbers must satisfy: **rising cost, falling damage-per-gold.** If upgrading were
more efficient than building, nobody would spread out and mazing would stop mattering.
`DamagePerGold_FallsWithEachLevel` fails the build if a content author breaks it.

`TowerLevel` is state on `SimState`: 1-based, hashed, snapshotted. Selling refunds half of everything
spent via `TowerDef.SellValueAt(level)`, so upgrade-then-sell cannot profit.

## Numbers in JSON → Fix32

The single conversion point in the whole system:

```csharp
static Fix32 ParseFix(JsonElement e)
{
    // decimal string → exact rational → Fix32. No float ever exists in this path.
    var (num, den) = DecimalToRational(e.GetRawText());   // "0.35" → (35, 100)
    return Fix32.FromFraction(num, den);
}
```

`e.GetDouble()` is never called. Parsing `"0.35"` to a `double` and then scaling it introduces the exact
platform-dependent rounding that `Fix32` exists to avoid. Values that cannot be represented exactly are
truncated toward zero, consistently, and the loader logs any value that lost precision beyond 1/65,536
so a content author can see it.

## Validation

The loader validates before the sim ever sees the data, and **fails loudly**:

| Check | Failure |
|---|---|
| Schema: required fields present, types correct | Throw with file and JSON path |
| Ranges: cost > 0, cooldown > 0, damage ≥ 0 | Throw |
| References: wave tables name enemies that exist | Throw with both ids |
| Map: exactly one goal, at least one spawn | Throw |
| Map: every spawn reaches the goal on the empty board | Throw |
| Precision: any value that lost more than 1/65,536 | Warn, keep going |

A content error is a startup crash, not a runtime surprise. The board editor calls the same validator
before it writes a map, which is how "the editor cannot save a broken map" is true without the editor
implementing any validation of its own.

## The map format

```json
{
  "id": "crossroads",
  "version": 1,
  "width": 24,
  "height": 24,
  "cells": [
    "########################",
    "#....b......b..........#",
    "#.bbb.bbbbb.bbbb.bbbb..#",
    "S......................G",
    "########################"
  ],
  "spawns": [{ "x": 0, "y": 3 }],
  "goal": { "x": 23, "y": 3 },
  "meta": { "author": "board-editor", "created": "2026-08-06" }
}
```

| Glyph | Cell |
|---|---|
| `.` | Path-only — creeps walk it, you cannot build on it |
| `b` | Buildable — creeps walk it until you build |
| `#` | Blocked — permanent scenery |
| `S` | Spawn (also listed in `spawns` for ordering) |
| `G` | Goal |

Rows are strings on purpose: a map diffs readably in git, and a human can see the shape of it in a pull
request. `cells` length must equal `height`, and every row's length must equal `width` — checked at
load.

**Spawn order is the `spawns` array, not the glyph scan order.** Anything that iterates spawns
(the block check, wave assignment) uses that array, so reordering it changes the game and is therefore
a content decision, not an accident of layout.

## Wave tables

```json
{
  "map": "crossroads",
  "hpGrowth": 1.08,
  "waves": [
    { "index": 1, "entries": [ { "enemy": "runner", "count": 8, "spacingTicks": 18, "spawn": 0 } ] },
    { "index": 2, "entries": [ { "enemy": "runner", "count": 12, "spacingTicks": 15, "spawn": 0 },
                               { "enemy": "brute",  "count": 2,  "spacingTicks": 60, "spawn": 0,
                                 "delayTicks": 120 } ] }
  ]
}
```

`hpGrowth` compounds wave to wave: wave N's enemies have `baseHp x growth^(N-1)`, computed once at
load with `Fix32` multiply and stored per wave. Wave 1 is never scaled. A single wave may override the
curve with an explicit `hpScale`.

Without it later waves cannot be harder -- enemy HP is fixed per definition, so sending more creeps of
the same toughness just hands the player more bounty, which becomes more towers. Measured before it
existed: waves 5-12 leaked nothing at all.

`SpawnSystem` applies the scalar in **long** arithmetic, not `Fix32` multiply: a tough enemy late in a
long table can exceed Fix32's +/-32767 range, and a silently wrapped creep health shows up as "wave 30
is trivial" months later.

Entries within a wave spawn independently on their own timers. `SpawnSystem` walks entries in array
order each tick and spawns whatever is due — so entry order determines entity id order on ties, which
means **reordering entries changes the run**. Deterministic, but not inert; treat it as a content
change and re-run the balance sim.

## Adding a new def field

1. Add it to the JSON schema and to every existing file (a missing required field is a load failure —
   that is the point).
2. Add the field to the def class; compute any derived form at load.
3. If the sim's *behavior* changes, that is a systems change, not a content change — it needs an
   architecture note ([Chapter 09](09-recipe-new-system.md)).
4. Regenerate `.tres`.
5. Run the balance sim. A new field that does nothing yet still changes nothing — prove it by seeing an
   unchanged report.
