# Salvage Value — Verification

**Slug:** `salvage-value` · **Status:** review · **Verdict:** PASS, with criterion 11 rewritten as unachievable-as-stated

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **165 passed**, 0 failed (was 151; +14) |
| Determinism trace | PASS | 30/30 checkpoints, **no re-record** |
| Balance targets | PASS | Default policy: leak 1.2%, runs lost 26.0%. Salvaging policy: 1.3%, 26.0% |
| Visual capture | PASS | `repair-baseline.png`, byte-identical across two runs |

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | Undamaged tower refunds exactly `SellValueAt(level)` | PASS | `SellingAnUndamagedTower_RefundsExactlyWhatItAlwaysDid` |
| 2 | Damaged tower refunds strictly less, in proportion | PASS | `SellingADamagedTower_RefundsStrictlyLess`, `RefundScalesWithHealthRemaining` |
| 3 | Minimum health refunds ~nothing, never negative | PASS | `AWreckRefundsAlmostNothing_AndNeverANegativeAmount`, `ATowerAtZeroHealth_RefundsNothing` |
| 4 | Refund never exceeds total spend | PASS | `RefundNeverExceedsTotalSpend_AtAnyHealthOrLevel` — every level × 20 health points |
| 5 | Upgraded tower's refund accounts for upgrades, scaled | PASS | `AnUpgradedDamagedTower_ScalesTheUpgradeCostsToo` |
| 6 | Sell price visible before the click | PASS | `repair-baseline.png`: `… repair 7 gold (middle click) · sell 14 (right click)` |
| 7 | Selling still works mid-wave | PASS | `SellingStillWorksWhileAWaveIsRunning` |
| 8 | Identical inputs give identical hashes | PASS | `SellingADamagedTower_IsDeterministicAcrossRuns` + the trace gate |
| 9 | Snapshot/restore unaffected | PASS | No new state added; existing round-trip tests cover `TowerHp`/`TowerLevel` |
| 10 | **Gold destroyed under salvaging ≥ under non-salvaging** | **PASS** | 1235 vs 868. Was 720 vs 868 before the change |
| 11 | Towers destroyed > 0 under a salvaging policy | **REWRITTEN — unachievable as stated** | See below |
| 12 | Balance targets still hold | PASS | Both policies, both targets |
| 13 | Undamaged refund is bit-for-bit unchanged | PASS | `AtEveryLevel_AnUndamagedTowerRefundsTheUnscaledValue`, every tower × every level |
| 14 | Sell price visible on hover, damaged and undamaged alike | PASS | Capture above; undamaged branch returns `Name -- sell N (right click)` |
| 15 | Recorded traces unchanged | PASS | 30/30. No trace sells a damaged tower |
| 16 | `SalvageValueAt` does not overflow | PASS | `SalvageValue_DoesNotOverflowAtExtremeValues` — exact expected value |
| 17 | Undamaged sell at every level equals `SellValueAt` | PASS | Same as 13, asserted directly rather than inferred from the balance run |

## Criterion 11: the requirements asked for something pricing cannot deliver

**As written:** *towers destroyed per run stays above zero with a salvaging policy.*

It cannot be met by any refund rule. Selling a doomed tower pays 12.5% of spend at 25% health; losing it
pays nothing. **Any refund above zero makes pre-empting strictly better**, so an attentive player always
sells first and destructions stay at 0.0 by construction.

Only a *rule* could force destructions — forbidding mid-wave sales, the symmetric partner to
`tower-repair`'s restriction. That was measured (row E) and is worse on every axis that matters:

| | gold destroyed | destroyed | standing | coverage |
|---|---|---|---|---|
| C · scaled refund, sells any time | **1235** | 0.0 | **45.9** | **262** |
| E · scaled refund, sells between waves only | 1203 | 2.0 | 43.8 | 249 |

Row E buys 2.0 destructions with two towers of standing defence, 13 route-cells of coverage, *and* less
gold destroyed. It takes a decision away and gets nothing for it.

**Rewritten to what pricing guarantees:** salvaging must not pay — criterion 10. Now true by 367 gold a
run, where before this slice it saved 148.

This is the better criterion. The goal was never to make players watch towers die; it was to stop
rewarding the tedium of preventing it.

## Three things verification found

**1. The previous slice's guard metric was blind to this.** `tower-repair` added `towers lost`
specifically so a future slice could not delete tower destruction silently. It counts `TowerDestroyed`
events, and a tower **sold** at 1 HP is not destroyed — so it read 0.0 while the same investment was
gone. Building `gold destroyed` *before* attempting the fix is what separated "destructions are zero,
panic" from "the economic loss is 17% and the experience loss is 100%", which are two different slices.

**2. Rounding direction is not cosmetic, and the two functions must differ.** `RepairCostFor` rounds
**up**, `SalvageValueAt` rounds **down**. Both round against the player — the player pays one and
receives the other. Rounding toward the player at either end opens a granularity exploit. They look
inconsistent side by side, so there is a test named for it.

**3. Pillar 1 needed a guarantee, not an inference.** `SellValueAt × Hp / Hp` equals `SellValueAt` for
every value that exists today, so the undamaged case would have worked without special handling. It is
an explicit early return anyway: repositioning is the maze mechanic, and it should not depend on an
argument about integer rounding surviving the next content edit.

## Scope note: one flag kept, one deleted

`tower-repair` established that an experiment flag should not outlive its question. Applied
asymmetrically here, on purpose:

- **`--salvageBetweenWavesOnly` deleted** — it answered the row-E question and nothing depends on it.
- **`--salvage` kept and documented in `Usage()`** — it backs a standing criterion. "Salvaging must not
  pay" is a property a future slice can break, so it stays re-runnable.

## Baselines

| File | Status |
|---|---|
| `repair-baseline.png` | Updated — prompt now carries the sell price beside the repair price |
| `board-baseline.png` | **Byte-identical**, verified by re-capture |
| `sapper-baseline.png` | **Byte-identical**, verified by re-capture |

## Not Verified

| What | Why |
|---|---|
| A *smart* salvaging player | The scripted one sells at a blanket 25% even when repair is better, so row C is a floor on how badly salvaging does, not a model of good play. A smarter policy would narrow 1235, not reverse it |
| Whether losing a tower now reads as a real setback | Needs a human. Follow-up `destruction-feel`, folded in with `repair-feel` |
| `gauntlet` | No sappers in its wave table, so nothing is damaged and nothing is salvaged (`gauntlet-sappers`) |

## Branch Resolution

None. Criterion 11 was rewritten at stage 02 rather than looped back through build — the implementation
was correct and the *criterion* was wrong, which is a spec edit, not a rebuild.
