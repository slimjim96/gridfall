# Salvage Value — Balance

**Date:** 2026-08-07 · **Slug:** `salvage-value` · **Map:** `crossroads` · **200 runs, seed 1**

## The result

Selling a tower used to refund half of what it **cost**, regardless of damage. Cashing out a wreck paid
the same as cashing out a pristine tower, which made pre-empting every destruction profitable. Refunds
now scale with remaining health, and the incentive has inverted: micro-salvaging costs 367 gold a run
instead of saving 148.

## The measurement that made this visible

`tower-repair` added `towers lost` precisely so a future slice could not delete tower destruction in
silence. **It was blind to this one.** A tower sold at 1 HP is not destroyed, so the counter read 0.0
while the same investment was just as gone.

The metric this pass adds is **`gold destroyed`** — unrecoverable investment, in the one unit both
removal routes share:

```
destroyed → the whole spend        sold → spend minus refund
```

Anything that touches towers should check this number first. Both of the last two slices had a failure
that leak-rate and runs-lost could not see, and both would have shown up here immediately.

## The sweep

| | refund rule | policy | gold destroyed | destroyed | salvaged | refunded | built | standing | coverage | leak | runs lost |
|---|---|---|---|---|---|---|---|---|---|---|---|
| A | full | never sells | 868 | 5.8 | 0 | 0 | 51.6 | 45.8 | 261 | 1.2% | 26.0% |
| B | full | salvages mid-wave | **720** | **0.0** | 10.9 | 720 | 56.8 | 45.9 | 264 | 1.2% | 27.0% |
| C | **scaled** | salvages mid-wave | **1235** | 0.0 | 10.8 | 154 | 56.7 | 45.9 | 262 | 1.3% | 26.0% |
| D | **scaled** | never sells | 868 | 5.8 | 0 | 0 | 51.6 | 45.8 | 261 | 1.2% | 26.0% |
| E | scaled | sells between waves only | 1203 | 2.0 | 7.0 | 61 | 52.8 | **43.8** | **249** | 1.2% | 26.5% |

Every row passes both balance targets. Again — that is now three consecutive slices where the targets
could not see the thing that mattered.

### A vs D — invisible to the players it should not touch

Identical to the last digit, on every column. A player who does not cash out wrecks cannot tell this
shipped. That is constraint 1 and pillar 1, confirmed at the run level as well as by unit test.

### A vs B — the exploit was real, and modest

Salvaging saved 148 gold of unrecoverable loss, an 17% recovery. Not a crisis economically.

**But destructions went 5.8 → 0.0.** The player never sees a tower die; they pre-empt every one. A
mechanic that never fires is not a balanced mechanic, it is an absent one — and this cost a *modest*
amount of gold to achieve, which is the worst possible shape: a small reward for constant tedium.

### D vs C — the incentive inverted

1235 against 868. Salvaging no longer merely pays less; it is **worse than ignoring it entirely**,
because selling at 25% and rebuilding costs more than repairing between waves would have.

The tedium is now unrewarded, which is the actual goal. Note the player still ends with the same
standing defence (45.9 vs 45.8) — they are not punished into a weaker board, just into wasting gold if
they play that way.

### C vs E — pricing beats restricting

Forbidding mid-wave sales, the symmetric partner to the repair rule, costs **two towers of standing
defence and 13 route-cells of coverage** and destroys *less* gold than pricing does. It removes a
decision and gets nothing for it.

`tower-repair` was fixed with a rule and this one with a price. The difference is what the player was
doing wrong: there, an unlimited-rate action beat a throughput threat and no price could stop it; here,
an action was simply mispriced.

## What this deliberately does not fix

**Towers destroyed stays at 0.0 under a salvaging policy (row C), and that is correct.**

Selling a doomed tower always pays more than losing it — 12.5% of spend at 25% health against nothing.
**No price short of zero makes destruction unavoidable for an attentive player.** A rule could, and row
E shows a rule is worse.

So the criterion this can meet is not "destruction still happens to everyone" but "salvaging must not
pay", which is now true by 367 gold a run. `--salvage` stays in the balance CLI so that stays checkable.

## Not measured

- **A smart salvaging policy.** The scripted one sells at a blanket 25% even when repairing is clearly
  better, so row C is a **floor** on how badly salvaging performs, not a model of good play. A policy
  that salvaged only when it could not afford repair would narrow the 1235 figure, not reverse it.
- **`gauntlet`.** No sappers in its wave table, so nothing gets damaged and nothing gets salvaged.
  Unchanged, still carrying its cliff (`gauntlet-sappers`).
- **Wave 3**, still leaking 14.1% — worst wave for six passes running now (`early-economy-2`).

## Standing rules, updated

Three slices, three metrics, one pattern:

| Slice | Failure the targets could not see | Number added |
|---|---|---|
| `tower-combat` | Tuning that satisfied targets while the mechanic did nothing | towers built vs standing |
| `tower-repair` | A new mechanic deleting the previous one | towers lost |
| `salvage-value` | The *same* deletion by a route the metric did not cover | **gold destroyed** |

The lesson is not "add a metric per slice". It is that **each of these metrics was too specific**, and
the fix each time was to measure one level more abstractly — from tower counts, to destructions, to the
gold those destructions represent. `gold destroyed` should hold for a while, because it is denominated
in the thing the invariant is actually about.
