# Tower Upgrades — Verification

**Slug:** `tower-upgrades` · **Status:** review · **Verdict:** PASS — all twelve criteria

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` (5 projects) | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **113 passed**, 0 failed (was 102; +11) |
| Determinism trace | PASS | 30/30 after a deliberate re-record — see below |
| Visual capture | PASS | Re-captured 2026-08-06 once the RDP session returned |

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | A tower can be upgraded, costing gold and raising its level | PASS | `Upgrading_CostsGoldAndRaisesTheLevel` |
| 2 | Upgrading raises damage, and range where the data says so | PASS | `Upgrading_RaisesDamageAndRange` — level 2 is damage only, level 3 widens |
| 3 | Max level refuses further upgrades, visibly | PASS | `AtMaxLevel_FurtherUpgradesAreRefused` — `AlreadyMaxLevel`, no gold spent |
| 4 | Insufficient gold is refused, nothing spent | PASS | `WithoutEnoughGold_TheUpgradeIsRefusedAndNothingIsSpent` |
| 5 | Selling accounts for upgrades, never exceeds spend | PASS | `UpgradingThenSelling_IsNeverProfitable` — asserts both bounds |
| 6 | **Tower level visible on the board** | **PASS** | `board-baseline.png`: the level-2 tower is visibly taller and brighter than the level-1 one beside it. Height carries it, so the cue survives greyscale |
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

## Visual verification lost, then regained

Frames can be captured only because `DISPLAY=:10` is an **xrdp** session. Mid-slice that session ended,
taking its X server with it, and for a while criterion 6 was unverifiable and the committed baseline
was stale — a stale baseline being worse than none, since it produces a false failure for whoever
diffs it next.

Worth keeping as a standing fact: **visual verification on this machine depends on someone being
connected over RDP.** It is not always available.

**Both closed on 2026-08-06** when the RDP session returned. Baselines re-captured and verified
byte-reproducible; criterion 6 confirmed.

Getting a *useful* capture took four attempts, each a real defect in the screenshot seed rather than in
the feature:

1. Three towers cost 190 of 200 gold, so the upgrade was correctly refused for insufficient funds and
   the capture showed no cue at all — it would have "verified" nothing.
2. Waiting a guessed 55 ticks for the comparison tower missed the 50-gold threshold by two.
3. The wait loop consumed its whole tick budget, leaving no tick to *apply* the queued build.
4. Upgrade and build were both queued before either applied, so the gold check read the pre-upgrade
   150 and skipped the wait entirely.

All four are the same underlying thing: **a command queued is not a command applied.** Phase 1 is next
tick, and a seed that reads state between an enqueue and its tick sees the past.

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
| Any player interaction with upgrades | There is **no input binding** for upgrade in the gameplay scene — the command exists and the policy uses it, but a human cannot upgrade anything yet. Deliberate scope: the slice's purpose was the economy. Follow-up `upgrade-input`. |
| Whether upgrading *feels* like a real choice | Needs a human playing, and the input to play with. |

## Branch Resolution

None — PASS on all twelve criteria.
