# Salvage Value — Build Notes

**Slug:** `salvage-value` · **Status:** review

Source lives at the repository root, not here — see `docs/conventions.md` §Where the source lives.

## What Was Built

| File | Change |
|---|---|
| `Gridfall.Core/Content/Defs.cs` | `SalvageValueAt(level, hp)` |
| `Gridfall.Core/Systems/CommandSystem.cs` | `Sell` uses it — a one-line change |
| `Gridfall.Verify/PlayPolicy.cs` | `TrySalvage()`, behind `--salvage` |
| `Gridfall.Verify/Program.cs` | **`gold destroyed`** metric, salvage counters, `--salvage` |
| `godot/GameplayScene.cs` | hover prompt gains the sell price |
| `Gridfall.Tests/SalvageTests.cs` | 14 tests |

**The simulation delta is two lines.** Everything else is measurement and presentation, which is the
right ratio for a slice whose whole problem was that nothing could see the bug.

## Decisions Made While Building

### The metric came before the fix, and it had to

`towers lost` counts `TowerDestroyed` events. A tower **sold** at 1 HP is not destroyed, so that number
reads 0.0 while exactly the same investment is gone. The metric `tower-repair` added — the one written
specifically so a future slice could not delete destruction silently — was blind to the second way of
deleting it.

`gold destroyed` is the fix: the unrecoverable investment, in the one unit both removal routes share.

```
TowerDestroyed  → goldDestroyed += TotalSpentAt(level)
TowerSold       → goldDestroyed += TotalSpentAt(level) - refund
```

Reconstructed in the balance runner from `BuildPlaced` / `TowerUpgraded`, so it needs no new sim state
and no new view accessor. Building it *first* is what turned "towers lost is 0.0, panic" into "the
economic loss is 720 vs 868, so this is a 17% leak and a 100% experience problem" — two very different
slices.

### `hp >= Hp` is an early return, not an optimisation

```csharp
if (hp >= Hp) return SellValueAt(level);
```

Falling through to `SellValueAt × Hp / Hp` gives the same answer today. But repositioning an undamaged
tower is pillar 1, and it should not depend on an argument about integer rounding holding for every
input. The early return makes criterion 13 a *guarantee* rather than a consequence.

### Truncating division here, ceiling division in `RepairCostFor`

They sit near each other and look inconsistent. They are not: **both round against the player.** Repair
rounds up because the player pays it; salvage rounds down because the player receives it. Rounding
toward the player at either end opens a granularity exploit. There is a test named for this so the next
person to "tidy" them finds the reason first.

### The requirements asked for something impossible, and the design says so

Criterion 11 was *"towers destroyed stays above zero with a salvaging policy"*. It cannot be met by
pricing: selling a doomed tower pays 12.5% of spend at 25% health, losing it pays 0, so an attentive
player always pre-empts. Only a **rule** could force destructions — and that rule (row E) measured worse
on every axis that matters.

Rewritten to what pricing can actually guarantee: **salvaging must not pay.** Measured at 1235 vs 868 —
micro-managing health bars now costs 367 gold a run rather than saving 148.

That is the better criterion anyway. The goal was never to force players to watch towers die; it was to
stop rewarding the tedium of preventing it.

### One flag stays, one goes

`tower-repair` established that an experiment flag should not outlive its question. Two flags here, and
they get opposite treatment:

- **`--salvageBetweenWavesOnly` — deleted.** It answered the row-E question (restricting is worse than
  pricing) and nothing depends on it afterwards.
- **`--salvage` — kept, and documented in `Usage()`.** It backs a *standing* criterion. "Salvaging must
  not pay" is a property a future slice can break, so it has to stay re-runnable. The usage text says so
  explicitly rather than just naming the flag.

### `TowerDef.SellValue` is dead

Loaded from JSON, exposed on the def, and read by nothing — `SellValueAt` has always been the real
calculation. Left alone deliberately: removing it is a content-schema change with its own migration, and
mixing it into a slice about refund arithmetic would muddy exactly the diff someone will read later.
Flagged below.

## Trace

**No re-record.** No recorded trace sells a damaged tower, so `crossroads-baseline` had to be unchanged —
and was, 30/30.

## Baselines

`repair-baseline.png` updated: the prompt now reads
`Arrow Tower at 58% -- repair 7 gold (middle click) · sell 14 (right click)`.

That single line is the design argument made visible — repair costs 7, salvage pays 14 but costs a
50-gold rebuild. `board-baseline.png` and `sapper-baseline.png` are **byte-identical**, verified by
re-capture, because neither seed hovers a tower.

## What I Would Flag to the Next Slice

- **`gold destroyed` should be the first number checked** by anything that touches towers. Both of the
  last two slices had a failure that leak-rate and runs-lost could not see, and both would have been
  visible here immediately.
- **`TowerDef.SellValue` is dead weight** — `sellValue` in every tower JSON is read and ignored.
  Follow-up `dead-sell-value`.
- The salvaging policy sells at a blanket 25% even when repairing would be better. That makes row C a
  *floor* on how badly salvaging does, not a model of a good player. A smarter policy would salvage only
  when it cannot afford to repair — which would narrow the 1235 figure, not reverse it.
