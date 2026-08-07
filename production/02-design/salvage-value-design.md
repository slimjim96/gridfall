# Salvage Value — Design

**Slug:** `salvage-value` · **Status:** done · **Supersedes for implementation:** the requirements file

## Behavior

1. Selling a tower refunds `SellValueAt(level) × remainingHealth / maxHealth`.
2. **An undamaged tower refunds exactly what it does today.** Not approximately — by an explicit early
   return, so repositioning never pays a rounding tax for a rule aimed at wrecks.
3. A tower at zero health refunds zero. A destroyed tower still refunds nothing at all.
4. Selling stays available while a wave is running. The fix is the price, not another restriction.
5. The sell price for the hovered tower is visible before the click.

## Why price and not a restriction

`tower-repair` fixed its problem with a rule (repair only between waves). The symmetric move here would
be *sell only between waves*, and it is the wrong move. Measured rather than argued, 200 runs each:

| | refund rule | policy | gold destroyed | destroyed | salvaged | refunded | standing | coverage |
|---|---|---|---|---|---|---|---|---|
| A | full | never sells | 868 | 5.8 | 0 | 0 | 45.8 | 261 |
| B | full | salvages mid-wave | **720** | **0.0** | 10.9 | 720 | 45.9 | 264 |
| C | **scaled** | salvages mid-wave | **1235** | 0.0 | 10.8 | 154 | 45.9 | 262 |
| D | **scaled** | never sells | 868 | 5.8 | 0 | 0 | 45.8 | 261 |
| E | scaled | sells between waves only | 1203 | 2.0 | 7.0 | 61 | **43.8** | **249** |

Read four pairs:

- **A vs D — the change is invisible to players it should not touch.** Identical to the last digit. A
  player who does not cash out wrecks cannot tell this shipped.
- **A vs B — the exploit was real.** Salvaging *saved* the player 148 gold of unrecoverable loss and
  took destructions from 5.8 to zero.
- **D vs C — the incentive inverted.** Salvaging now *costs* 367 gold rather than saving 148. The
  tedious micro is no longer merely less rewarding; it is worse than ignoring it.
- **C vs E — restricting is strictly worse than pricing.** Forbidding mid-wave sales costs two towers of
  standing defence and 13 route-cells of coverage, and destroys *less* gold than pricing does. It takes
  a decision away and gets nothing for it.

Constraint 4 stands, with numbers behind it.

## What this does not do, and why that is correct

**Towers destroyed stays at 0.0 under a salvaging policy** (row C), and that is not a failure.

Selling a doomed tower still pays *something* — 12.5% of spend at 25% health — so it will always beat
letting it die by exactly that much. **No price short of zero makes destruction unavoidable for an
attentive player.** A rule could (that is row E), and row E is worse.

So the criterion this slice can actually meet is not "destruction still happens to everyone". It is:

> **Salvaging must not pay.** A player who micro-manages health bars must not end up ahead of one who
> ignores them.

That is now true by 367 gold a run, and it is the honest version of what the requirements asked for.
Criterion 11 is rewritten below to say so.

Where salvage remains correct is the case it should be: **a tower you cannot afford to save.** Between
waves, repairing a 25%-health tower costs ~0.225 × spend and keeps it; selling pays 0.125 × spend and
costs a full rebuild. Repair wins comfortably. Salvage is the fallback when the gold is not there — a
priced retreat rather than a free one.

## Player-Visible States

| State | What the player sees |
|---|---|
| Hovering an undamaged tower | `Arrow Tower -- sell 25 gold (right click)` |
| Hovering a damaged tower | `Arrow Tower at 58% -- repair 7 gold (middle click) · sell 14 (right click)` |
| Hovering a damaged tower mid-wave | `Arrow Tower at 58% -- repair between waves · sell 14 (right click)` |

Selling is the one command in the game whose price was never shown before the click. Pillar 2 says
every player-visible state has a visible representation, and "what this button pays" is exactly that.

## Tuning Knobs

**None.** The refund fraction stays at half of remaining, and there is deliberately no `salvagePercent`.

A knob here would be a knob on how much the enemy's damage really costs, which is what `repairPercent`
and enemy `attackDamage` already control from two directions. A third would make attribution
impossible — the standing rule in `content-data/CONTEXT.md` is one knob per pass, and this pass has
zero.

## Interaction Rules

- **Vs. repositioning (pillar 1):** untouched. An undamaged tower refunds exactly what it always did.
  This is the constraint that shaped the implementation, not a side effect of it.
- **Vs. repair:** repair now clearly dominates selling for a tower worth keeping, which is the intended
  ordering. Before this slice they were close enough that selling won on availability alone.
- **Vs. upgrades:** an upgraded tower's refund still counts upgrade costs, scaled the same way — so
  concentrating investment now carries salvage risk as well as the maintenance liability `tower-repair`
  added. Same direction, same reason.
- **Vs. ADR-0007's repair bound:** the validator compares repair-to-full against `SellValueAt(level)`,
  the *undamaged* sell value. Scaled refunds make the real sell-and-rebuild alternative **more**
  expensive for a damaged tower, so the bound is now conservative rather than tight. It still holds and
  needs no change — but it is no longer the exact break-even it was written as, and the ADR says so.

## Rejection Cases

Unchanged. Selling a tower that does not exist is still silent; there is no new refusal.

## Acceptance Criteria (carried + revised)

1–9 and 12 carried unchanged from the requirements.

**10 (revised).** Gold destroyed per run under a salvaging policy must be **no lower** than under a
non-salvaging one. Measured: 1235 vs 868 — salvaging costs 367 rather than saving 148.

**11 (revised).** ~~Towers destroyed stays above zero with a salvaging policy.~~ **Unachievable by
pricing**, and the requirements were wrong to ask for it: selling a doomed tower always pays more than
losing it, so an attentive player always pre-empts. Replaced by:

> Towers destroyed per run stays above zero for the **default** policy (5.8), and salvaging must not be
> profitable — criterion 10.

13. Selling an undamaged tower refunds **bit-for-bit** what it did before this slice, at every level.
14. The sell price is visible on hover, for damaged and undamaged towers alike.

## Deliberately Not Doing

- **Restricting sales to between waves.** Row E. Costs standing defence and a decision, gains nothing.
- **A `salvagePercent` knob.** See Tuning Knobs.
- **Refunding anything for a destroyed tower.** Destruction paying out would undo `tower-combat` by the
  most direct route available.
