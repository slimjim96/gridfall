# Salvage Value — v1

**Slug:** `salvage-value` · **Status:** done · **Verified at trace:** `crossroads-baseline`, unchanged

## What Shipped

Selling a tower refunds half of what is **left** of it, not half of what it cost.

- **`SalvageValueAt(level, hp)`** — `SellValueAt(level) × remaining / max`.
- **An undamaged tower refunds exactly what it always did**, by an explicit early return rather than by
  arithmetic that happens to land there. Repositioning is pillar 1 and must not pay a rounding tax for a
  rule aimed at wrecks.
- **Selling stays available mid-wave.** The fix is the price, not another restriction.
- **The sell price is visible on hover** — the one command in the game whose cost was never shown before
  the click, and now the one whose price moves.
- **`gold destroyed`**, a new balance metric: unrecoverable investment, counting both destruction and
  discounted sales.
- No new state, no new command, no new event, no new phase. The simulation delta is two lines.

## Player-Facing Change

A tower the enemy has nearly destroyed is nearly worthless. You can still cut it loose mid-wave — that
retreat is priced now, not free.

Hovering any tower shows what it pays. Hovering a damaged one shows both options side by side:

```
Arrow Tower at 58%  --  repair 7 gold (middle click)  ·  sell 14 (right click)
```

That line is the whole design argument: repair costs 7 and keeps the tower; salvage hands you 14 and
costs a 50-gold rebuild.

## The result that matters

Before this slice, a player who watched health bars and sold every doomed tower came out **ahead**:

| | refund rule | policy | gold destroyed | destroyed | salvaged | refunded | standing |
|---|---|---|---|---|---|---|---|
| A | full | never sells | 868 | 5.8 | 0 | 0 | 45.8 |
| B | full | salvages | **720** | **0.0** | 10.9 | 720 | 45.9 |
| C | **scaled** | salvages | **1235** | 0.0 | 10.8 | 154 | 45.9 |
| D | **scaled** | never sells | 868 | 5.8 | 0 | 0 | 45.8 |
| E | scaled | sells between waves only | 1203 | 2.0 | **43.8** standing | 61 | 43.8 |

- **A vs D — invisible where it should be.** Identical on every column. A player who does not cash out
  wrecks cannot tell this shipped.
- **A vs B — the exploit was real, and small.** 148 gold, a 17% recovery. But destructions went 5.8 to
  **zero**: a small reward for constant tedium, which is the worst shape a mechanic can have.
- **D vs C — the incentive inverted.** Salvaging now *costs* 367 gold rather than saving 148. Standing
  defence is unchanged, so the tedium is unrewarded rather than punished.
- **C vs E — pricing beats restricting.** Forbidding mid-wave sales costs two towers of standing defence
  and 13 route-cells of coverage, and destroys *less* gold. `tower-repair` was fixed with a rule; this
  one needed a price. The difference is that there an unlimited-rate action beat a throughput threat and
  no price could stop it, while here an action was simply mispriced.

## The metric that found it

`tower-repair` added **towers lost** so that a future slice could not delete tower destruction in
silence. **It was blind to this one.** It counts destructions, and a tower *sold* at 1 HP is not
destroyed — so it read 0.0 while the same gold was just as gone.

**`gold destroyed`** replaces it as the number to check: unrecoverable investment, in the unit both
removal routes share. Three slices, three guard metrics, and the pattern is not "add a metric per
slice":

| Slice | Failure the targets missed | Number added |
|---|---|---|
| `tower-combat` | Tuning that hit targets while the mechanic did nothing | built vs standing |
| `tower-repair` | A new mechanic deleting the previous one | towers lost |
| `salvage-value` | The **same** deletion, by a route that metric did not cover | **gold destroyed** |

Each metric was too specific, and the fix each time was to measure one level more abstractly. This one
should hold longer, because it is denominated in what the invariant is actually about.

## What this deliberately does not fix

**Towers destroyed stays at 0.0 for a salvaging player, and that is correct.** Selling a doomed tower
always pays more than losing it, so no price short of zero makes destruction unavoidable for someone
paying attention. A rule could — row E — and a rule is worse.

The criterion was rewritten to what pricing can guarantee: **salvaging must not pay.** True by 367 gold
a run. `--salvage` stays in the balance CLI so it stays checkable.

## New Tuning Knobs

**None, deliberately.** There is no `salvagePercent`.

A knob here would control how much enemy damage really costs — which `repairPercent` and enemy
`attackDamage` already control from two directions. A third would make attribution impossible, and
`content-data`'s standing rule is one knob per pass. This pass has zero.

## Also Worth Knowing

- **Salvage rounds down; repair rounds up.** They look inconsistent side by side and they are not: both
  round *against* the player, because the player pays one and receives the other. Rounding toward the
  player at either end opens a granularity exploit. There is a test named for this.
- **ADR-0007's repair bound is now conservative rather than tight.** It compares repair-to-full against
  the *undamaged* sell value; scaled refunds make the real sell-and-rebuild alternative more expensive
  for a damaged tower. The bound still holds and needs no change — the ADR records that it is no longer
  the exact break-even it was written as.
- `board-baseline.png` and `sapper-baseline.png` are byte-identical, verified by re-capture.

## Follow-Ups Not Done

| Item | Workspace | Slug |
|---|---|---|
| `TowerDef.SellValue` is loaded from every tower JSON and read by nothing | engine-systems | `dead-sell-value` |
| Wave 3 leaks 14.1%, worst wave for **six** passes running | content-data | `early-economy-2` |
| Sappers on `gauntlet` — still no destructible-tower pressure there | content-data | `gauntlet-sappers` |
| Per-archetype `repairPercent`; both towers ship at 60 | content-data | `repair-tuning` |
| Sweep `attackCooldown` and `attackRange` independently | content-data | `sapper-tuning` |

## Known Not Verified

- Whether losing a tower now reads as a real setback, and whether the between-waves repair rule reads as
  a budget or a restriction. Both need a human (`repair-feel`, `destruction-feel`).
- A *smart* salvaging player. The scripted one sells at a blanket 25% even when repair is better, so the
  1235 figure is a floor on how badly salvaging does, not a model of good play.
