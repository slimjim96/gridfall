# Salvage Value — Architecture

**Slug:** `salvage-value` · **Status:** done · **Owner:** systems-architect
**Implement against this note, not the design spec.**

## The shape of this slice

One arithmetic change to one existing command, plus the harness work needed to see it.

> **No new state, no new command, no new event, no new phase.**

`Sell` already exists in phase 1, already frees the cell, already emits `TowerSold`. This slice changes
what number goes into the refund. The whole simulation delta is two lines.

The work that is *not* trivial is the measurement: the previous behaviour was invisible to the balance
sim because the scripted player never sold, and the metric that would have caught it did not exist.

## Systems Changed

| System | Change |
|---|---|
| `Content/Defs.cs` | `SalvageValueAt(level, hp)` |
| `Systems/CommandSystem.cs` | `Sell` calls it instead of `SellValueAt(level)` |
| `Gridfall.Verify/PlayPolicy.cs` | `TrySalvage()` — cuts doomed towers loose, behind `--salvage` |
| `Gridfall.Verify/Program.cs` | **`gold destroyed`** metric; salvage counters; `--salvage` |
| `godot/GameplayScene.cs` | hover prompt gains the sell price |

**Not changed:** `SimState`, `Commands.cs`, `SimEvent.cs`, `PathSystem`, `SimStateView`, every phase.

## The function

```csharp
public int SalvageValueAt(int level, int hp)
{
    if (hp >= Hp) return SellValueAt(level);          // undamaged: unchanged, exactly
    if (hp <= 0) return 0;
    return (int)((long)SellValueAt(level) * hp / Hp);
}
```

Three properties, each load-bearing:

1. **The `hp >= Hp` early return is not an optimisation.** It is criterion 13. Falling through to the
   multiply would give `SellValueAt × Hp / Hp`, which is arithmetically identical *today* — but it makes
   repositioning depend on a rounding argument rather than on a stated guarantee. Pillar 1 should not
   rest on `a * b / b == a`.
2. **`long` intermediate.** `SellValueAt × hp` reaches ~10⁹ at the values `RepairCostFor`'s overflow
   test already exercises. Same reasoning as that function: int overflow here would be *deterministic*
   and therefore invisible to the hash.
3. **Truncating division, deliberately — the opposite of `RepairCostFor`.** Both round against the
   player: repair rounds *up* because the player pays it, salvage rounds *down* because the player
   receives it. Rounding toward the player in either place opens a granularity exploit. Worth stating
   because the two functions sit next to each other and look inconsistent otherwise.

## Tick Placement

Unchanged. `Sell` stays in phase 1, still sets the dirty flag via `path.SetBlocked(index, false)`, and
phase 2 still recomputes. Nothing about *when* selling happens changes — only the gold.

## Determinism Checklist

| Check | Result |
|---|---|
| Float accumulation | None. Integer throughout. |
| Dictionary iteration order | None in Core. The balance runner's `towerId → def` map is read by key, never iterated. |
| Wall clock / unseeded random | None. |
| Godot types below the boundary | None. |
| LINQ over unordered collections | None. |
| New state to hash | **None.** `TowerHp` and `TowerLevel` were already hashed. |
| Int overflow | `long` intermediate, as above. |

## The measurement, which is most of the slice

### `gold destroyed` — the metric that survives both routes

The existing `towers lost` counts `TowerDestroyed` events. **A tower sold at 1 HP is not destroyed**, so
that number reads zero while the same investment is just as gone. It could not see this bug, and it
could not have seen `tower-repair`'s either if the player had sold instead of repairing.

```
TowerDestroyed  → goldDestroyed += TotalSpentAt(level)
TowerSold       → goldDestroyed += TotalSpentAt(level) - refund
```

Reconstructed in the balance runner from `BuildPlaced` / `TowerUpgraded` events, so it needs **no new
sim state and no new view accessor** — the events already carry tower id, def index, and level.

This is the invariant `tower-combat` actually installed, stated in the one unit both removal routes
share. It should be the first number anyone checks when a slice touches towers.

### `--salvage`, and why this flag stays

`PlayPolicy` never sold, so the sim was structurally blind to the behaviour this slice prices. The flag
makes a salvaging player measurable.

**It is permanent, unlike `tower-repair`'s `--repairBetweenWavesOnly`.** That flag answered a one-time
design question and was deleted once answered. This one backs a *standing* criterion — "salvaging must
not pay" is a property future slices can break, so it has to stay re-runnable.

`--salvageBetweenWavesOnly` is the one to delete: it answered the row-E question and nothing depends on
it afterwards.

## Acceptance Criteria the Verify Stage Will Run

1–9, 12–14 from the design spec, plus:

15. **Traces unchanged.** No recorded trace sells a damaged tower, so a hash shift means something moved
    that should not have. Check before considering a re-record.
16. `SalvageValueAt` does not overflow at extreme values.
17. Selling an undamaged tower at every level refunds exactly `SellValueAt(level)` — the pillar-1 guard,
    asserted directly rather than inferred from the balance run.

## Risks

| Risk | Mitigation |
|---|---|
| Repositioning quietly gets more expensive | Criterion 13/17, plus rows A vs D being identical |
| The economy tightens too far and runs-lost blows the band | Criterion 12. Measured at 26.0% |
| `gold destroyed` bookkeeping drifts from reality | Reconstructed from events only; a tower with no `BuildPlaced` contributes 0 rather than guessing |
