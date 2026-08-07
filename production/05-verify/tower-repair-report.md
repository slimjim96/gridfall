# Tower Repair — Verification

**Slug:** `tower-repair` · **Status:** review · **Verdict:** PASS, after one loop back to 02

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **151 passed**, 0 failed (was 129; +22) |
| Determinism trace | PASS | 30/30 checkpoints, **no re-record** |
| Balance targets | PASS | 200 runs: leak 1.2% (≤4%), runs lost 26.0% (15–30%) |
| Visual capture | PASS | `repair-baseline.png`, byte-identical across two runs |

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | A damaged tower can be repaired for gold | PASS | `ADamagedTower_IsRestoredToFullForGold` |
| 2 | Repair never exceeds maximum health | PASS | `RepairingNeverOvershootsMaximumHealth` — repairs from 1 HP, lands exactly on max |
| 3 | Repairing an undamaged tower is refused, no gold spent | PASS | `AnUndamagedTower_IsRefusedAndNothingIsSpent` |
| 4 | Insufficient gold is refused, no gold spent | PASS | `WithoutEnoughGold_TheRepairIsRefusedAndNothingIsSpent` — asserts HP unchanged too |
| 5 | Cost scales with health missing | PASS | `RepairCost_ScalesWithHealthMissing` |
| 6 | Repair-to-full always beats sell-and-rebuild | PASS | `RepairingToFull_AlwaysBeatsSellingAndRebuilding`, every tower × every level. Also enforced at load |
| 7 | A repaired tower keeps its upgrade level | PASS | `RepairingDoesNotChangeTheLevel_AndUpgradingDoesNotHeal` |
| 8 | Repair changes no route | PASS | `ARepair_ChangesNoRoute` — asserts path version *and* no `PathRecomputed` |
| 9 | Identical inputs give identical hashes | PASS | `Repair_IsDeterministicAcrossRuns` + the trace gate |
| 10 | `Restore(Snapshot())` round-trips mid-repair | PASS | `SnapshotRestore_RoundTripsAPartiallyRepairedBoard` — asserts the fixture is genuinely repaired first |
| 11 | **Towers are still destroyed after this slice** | **PASS — on the second attempt** | 51.6 built, 45.8 standing, **5.8 lost per run**. Failed at 0.0 in revision 1; see below |
| 12 | The repair affordance is discoverable on the board | PASS | `repair-baseline.png`: `Arrow Tower at 58% -- repair 7 gold (middle click)`, plus the help line |
| 13 | Repair ≠ upgrade in both directions | PASS | Same test as 7: upgrading a 40-HP tower leaves it at 40 |
| 14 | Granular repair is never cheaper | PASS | `ManySmallRepairs_AreNeverCheaperThanOneLargeOne` |
| 15 | `repairPercent` is authored data | PASS | `EveryShippedTower_AuthorsItsRepairPercent` — reads the shipped JSON, not the fixture |
| 16 | Repair is refused while a wave runs | PASS | `WhileAWaveIsRunning_RepairIsRefused`, `BetweenWaves_RepairIsAllowedAgain` |
| 17 | A def violating the wall fails to load | PASS | `ATowerWhoseRepairCostBeatsSellAndRebuild_FailsToLoad` — asserts the message names the tower, the level, and the percent |
| 18 | `RepairCostFor` does not overflow | PASS | `RepairCost_DoesNotOverflowAtExtremeValues` — exact expected value, not a range |
| 19 | Recorded traces are unchanged | PASS | `crossroads-baseline` 30/30. Repair is inert without a `RepairCommand`, so a shift would have meant something else moved |

## The failure, and the loop back

**Criterion 11 failed on the first build**, exactly as designed and exactly as specified.

| Config | built | standing | **lost** | leak | runs lost |
|---|---|---|---|---|---|
| No repair (`tower-combat`) | 55.7 | 45.8 | **9.9** | 1.3% | 28.5% |
| Repair any time, `repairPercent` 60 | 45.6 | 45.6 | **0.0** | 1.2% | ok 25.5% |
| Repair any time, `repairPercent` 96 *(the ceiling)* | 44.3 | 44.3 | **0.0** | 1.3% | ok 28.0% |
| **Repair between waves only** | 51.6 | 45.8 | **5.8** | 1.2% | ok 26.0% |

**Both balance targets read "ok" in every row, including the two that had switched the previous slice
off.** Leak rate and runs-lost cannot see this failure at all — the mechanic they measure was replaced
by a cheaper one that produces the same defence.

96 is the highest value `ValidateRepairCurve` accepts; at 97 the arrow tower's repair cost meets its
sell-and-rebuild cost and the loader throws. So the middle rows are not a bad guess at tuning — they
bracket **the entire legal range of the knob**, and the whole range fails.

### Branch resolution

Looped back to **02 (design)**, not 03 or 04. The architecture note and the build implemented the design
correctly; the design was wrong. Specifically it asserted that mid-wave repair was "the interesting
case" and listed between-waves repair under *Deliberately Not Doing*, without checking whether an
always-available counter could leave `tower-combat`'s result standing.

Revision 2 of the design spec keeps the original reasoning under *What revision 1 got wrong*, because
the error is more useful than the fix.

## Three things verification found that the tests would not have

**1. The design's own arithmetic was wrong, and the loader caught it.** `repairPercent` was specified as
a fraction of total spend bounded at 100. Selling refunds *half*, so the real bound as a fraction of
spend is 50 — the shipped default of 60 violated its own wall. `ValidateRepairCurve` threw on the first
run. ADR-0007 was written to catch a future content edit and instead caught the design it was written
from, on day one.

**2. Raising sapper throughput is not a substitute.** Quadrupling `attackCooldown` (1.2 → 0.3) recovered
1.6 of the 9.9 lost towers and pushed runs-lost to 42%, past the band. Threat throughput and counter
throughput are not symmetric levers, because the counter is funded by gold and gold scales with the
wave. Recorded so the next person does not spend the afternoon on it.

**3. A policy that cannot use a mechanic verifies nothing.** `PlayPolicy` initially never repaired, so
criterion 11 would have "passed" by measuring the previous slice unchanged. The policy had to learn to
repair before the criterion meant anything. **A criterion that a scripted player cannot exercise is not
a criterion.**

## Method note: the rule was tested as a policy flag first

The between-waves rule was tried as a two-line restriction on `PlayPolicy` before it was written into
`CommandSystem` — cheaper to run, and reversible. Once it worked, it moved into Core, and the
Core-enforced numbers came out **identical** to the policy-restricted ones (51.6 / 45.8 / 5.8 in both).

That identity is the confirmation that the *rule* did the work, not some artefact of how the policy was
written. The experiment flag was then removed; a knob that exists to answer one question should not
outlive the answer.

## Baselines

| File | Status |
|---|---|
| `repair-baseline.png` | **New.** Between waves, worst tower at 58%, prompt showing cost and binding |
| `board-baseline.png` | Refreshed — HUD help line only. Sim hash `b9c3bc7c95e6f726` unchanged |
| `sapper-baseline.png` | Refreshed — same reason. Sim hash `a15d4919788939c8` unchanged, worst tower still cell (2,6) at 28% |

## Not Verified

| What | Why |
|---|---|
| Whether the between-waves rule *feels* like a budget or like a restriction | Needs a human. The decision is verified to exist; whether it reads as tense or as annoying is a different claim. Follow-up `repair-feel` |
| A player who sells mid-wave instead of repairing | Selling works mid-wave, repairing does not. Cutting a tower loose and rebuilding later may beat repairing. The beginner policy never sells (`salvage-value`) |
| Repair on `gauntlet` | No sappers in its wave table, so nothing to repair (`gauntlet-sappers`) |
| Per-archetype `repairPercent` | Both towers ship at 60; no sweep separated them (`repair-tuning`) |
