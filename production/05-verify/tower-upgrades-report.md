# Tower Upgrades — Verification

**Slug:** `tower-upgrades` · **Status:** review · **Verdict:** PASS, with one criterion unverifiable

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` (5 projects) | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **113 passed**, 0 failed (was 102; +11) |
| Determinism trace | PASS | 30/30 after a deliberate re-record — see below |
| Visual capture | **UNAVAILABLE** | The X display is gone; see "Visual verification lost" |

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | A tower can be upgraded, costing gold and raising its level | PASS | `Upgrading_CostsGoldAndRaisesTheLevel` |
| 2 | Upgrading raises damage, and range where the data says so | PASS | `Upgrading_RaisesDamageAndRange` — level 2 is damage only, level 3 widens |
| 3 | Max level refuses further upgrades, visibly | PASS | `AtMaxLevel_FurtherUpgradesAreRefused` — `AlreadyMaxLevel`, no gold spent |
| 4 | Insufficient gold is refused, nothing spent | PASS | `WithoutEnoughGold_TheUpgradeIsRefusedAndNothingIsSpent` |
| 5 | Selling accounts for upgrades, never exceeds spend | PASS | `UpgradingThenSelling_IsNeverProfitable` — asserts both bounds |
| 6 | **Tower level visible on the board** | **NOT VERIFIED** | Implemented (taller + brighter per level) and compiling. **Could not be seen** — see below |
| 7 | Identical inputs still produce identical hashes | PASS | `ScalingDoesNotBreakDeterminism` + the trace gate |
| 8 | Mutating tower level changes the hash | PASS | `Hash_Covers_TowerLevel` |
| 9 | Snapshot preserves tower levels | PASS | `Snapshot_PreservesTowerLevels` |
| 10 | **Late-game idle gold falls substantially** | **PASS — decisively** | Gold at wave 11: **1,090 → 34** |
| 11 | An upgrade never changes the route or dirties the grid | PASS | `UpgradingNeverChangesTheRouteOrDirtiesTheGrid` — path version and cost grid both unchanged |
| 12 | Damage per gold falls with each level | PASS | `DamagePerGold_FallsWithEachLevel`, asserted against the **shipped** data |

## The result that matters

The economy hole is closed.

| | Before | After |
|---|---|---|
| Gold at wave 10 | 378 | **54** |
| Gold at wave 11 | 1,090 | **34** |
| Upgrades bought | — | 16.9 per run |

Three passes identified "no gold sink that scales" as the cause of the runaway. Gold now has somewhere
to go that does not need free board space, and it goes there.

**Difficulty did not change at all**: leak rate 2.8%, runs lost 33.3%, identical to before. That is
expected and worth stating plainly — every death happens at wave 3, before upgrades are affordable, so
a late-game gold sink cannot touch it. Waves 6–11 still leak zero.

Upgrades fixed the economy. They did not fix the difficulty curve, and were never going to.

## Visual verification lost

Frames could be captured for the last four slices because `DISPLAY=:10` was an **xrdp** session. That
session has ended, taking its X server with it — `/tmp/.X11-unix` now has only the lightdm console
`X0`, which refuses authorization.

Consequences, stated rather than worked around:

- **Criterion 6 is unverified.** The level cue is written and compiles; nobody has seen it.
- **`presentation/docs/board-baseline.png` is now stale.** The screenshot seed was changed to include
  an upgraded tower, so the committed baseline no longer matches what shot mode produces. A stale
  baseline is worse than none — it will produce a false failure for whoever diffs it next. Flagged
  here and as a follow-up rather than deleted, because the image is still a useful reference.

Both close by reconnecting over RDP and re-running the capture. Follow-up `refresh-baselines`.

## Trace re-recorded

`TowerLevel` is new hashed state, so every hash shifts. It diverged at **tick 100**, not tick 0 —
correct, because a per-tower field contributes nothing to the hash until a tower exists, and the
script's first build is at tick 5. Diagnosed before touching the trace.

`23d8e456da0eba21` → `17ed9dcac986f821`.

## A fixture bug worth recording

Five upgrade tests failed on first run, all with the same cause: **the test fixture's towers had no
`upgrades`**. I had added the tracks to `content-data/` but not to `TestContent`, so `MaxLevel` was 1
and every upgrade was correctly refused with `AlreadyMaxLevel`.

The tests were right, the fixture was wrong — and a fixture that does not mirror the shipped content
shape makes upgrade tests pass vacuously in the other direction too. Comment added saying so.

## Not Verified

| What | Why |
|---|---|
| Criterion 6, the level cue | No display. |
| Any player interaction with upgrades | There is **no input binding** for upgrade in the gameplay scene — the command exists and the policy uses it, but a human cannot upgrade anything yet. Deliberate scope: the slice's purpose was the economy. Follow-up `upgrade-input`. |
| Whether upgrading *feels* like a real choice | Needs a human playing, and the input to play with. |

## Branch Resolution

None — PASS. Criterion 6 is unverifiable rather than failed, and recorded as such.
