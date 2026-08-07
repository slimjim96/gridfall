# Tower Combat — Verification

**Slug:** `tower-combat` · **Status:** review · **Verdict:** PASS

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **129 passed**, 0 failed (was 119; +10) |
| Determinism trace | PASS | 30/30 after a deliberate re-record |
| Balance targets | PASS | 200 runs: leak 1.3% (≤4%), runs lost 28.5% (15–30%) |
| Visual capture | PASS | `sapper-baseline.png`, byte-identical across two runs |

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | An enemy can damage a tower while walking | PASS | `ASapperDamagesATowerItWalksPast`, `ASapperKeepsWalkingWhileItAttacks` |
| 2 | A tower at zero HP is destroyed and announced | PASS | `ATowerReducedToZero_IsDestroyedAndAnnounced` |
| 3 | A destroyed tower frees its cell | PASS | `ADestroyedTower_UnblocksItsCellForPathing` — asserts the cost grid *and* rebuilds on the cell |
| 4 | Existing enemies are unaffected | PASS | `AnEnemyWithNoAttackDamage_NeverTouchesATower`; `attackDamage` defaults to 0 |
| 5 | Attacks are rate-limited | PASS | `AttacksAreRateLimitedByCooldown` — hit count × damage reconciles against HP lost |
| 6 | New state is hashed | PASS | `Hash_Covers_TowerHp`, `Hash_Covers_CreepAttackCooldown` |
| 7 | New state survives snapshot/restore | PASS | `SnapshotRestore_RoundTripsMidAttack` — asserts the fixture is genuinely mid-attack first |
| 8 | Identical inputs give identical hashes | PASS | `TowerDamage_IsDeterministicAcrossRuns` + the trace gate |
| 9 | **The sapper is identifiable at a glance** | PASS | `sapper-baseline.png`: red point-down wedges among khaki cubes and teal spheres. The roster's only red |
| 10 | **A damaged tower is visibly damaged** | PASS | Same capture: the 28%-health tower at cell (2,6) is dark reddish-brown beside bright orange neighbours |
| 11 | Every enemy has a distinct silhouette | PASS | `TheRosterHasFiveArchetypes_AndNoSharedSilhouette` |
| 12 | **Income stops compounding into permanent defence** | **PASS — the point of the slice** | 55.7 towers built, 45.8 standing. Those two numbers were equal for the project's entire history |

## The result that matters

Seven passes established one invariant: total defence tracks cumulative income, because towers are
permanent. **Built ≠ standing is the first time that has not held.**

| | Before | After |
|---|---|---|
| Towers built | 52.4 | 55.7 |
| Towers standing | 52.4 | **45.8** |
| `hpGrowth` needed | 1.09 | **1.08** |

Difficulty from destruction *replaces* some difficulty from enemy hitpoint inflation, rather than
stacking on top of it. See the [balance report](../../content-data/docs/reports/2026-08-07-tower-combat-balance.md).

## Tuning: hitting the target was not the same as succeeding

`arrow hp 1300` hit both balance targets — leak 1.3%, runs lost 20% — and was **rejected**. At that
value only ~5 towers die per run and the numbers are indistinguishable from the previous pass with no
sappers at all. The tuning had quietly turned the feature off while satisfying its metrics.

Worth keeping as a standing rule: **when a slice adds a mechanic, a balance target is necessary and not
sufficient.** Measure that the mechanic is still doing something.

## Three real defects found by verification

**1. Stale events replayed every frame in shot mode.** `UnitRenderer.Render` gated on `_driver.Ticked`,
which only `Advance()` clears — and shot mode steps the driver by hand and never calls it. The final
seeded tick's events therefore re-fired on all 40 rendered frames, permanently re-arming the hit flash
and rendering the damaged tower solid white. It hid the exact cue the capture existed to verify. Now
gated on the tick *number*.

This also affected the committed `board-baseline.png` (a stuck muzzle flash), which is why that
baseline is refreshed in this slice. The simulation hash is unchanged, confirming the difference is
purely rendering.

**2. The launcher runs a stale assembly, silently.** Godot does not rebuild C# on run; it loads
whatever is in `.godot/mono`. My first three captures rendered code that had never been compiled — one
of them a version that did not compile at all. `scripts/godot-env.sh` now builds first and refuses to
launch on failure.

**3. The sapper had no placeholder.** `TheRosterHasFourArchetypes` failed on the roster count, and the
same test's silhouette check showed the sapper falling through to the grey "unknown" sphere — the one
enemy that destroys your towers, rendered as an anonymous blob. Caught because the test asserts the
factory has a case per id, not just a count.

## Three seed bugs, one lesson each

The screenshot seed had to actually play to wave 5+, and got there wrong three times:

1. Offered build cells it had already built on → 6 towers, then every build refused.
2. Filtered placements by adjacency to `PathOnly` terrain → 9 cells. `crossroads` is a **mazing** map:
   the route runs over buildable cells and is a property of the flow field, not the terrain.
3. Re-offered a cell the seal check kept refusing → livelock, 2 towers built while holding 248 gold.

All three produced a *plausible* board that was quietly wrong. The fourth version plays to wave 7 with
28 towers and full lives.

A fourth, older trap recurred too: reading `TowerSlotByOrder(0)` before ticking, when the build command
was still queued. **A command queued is not a command applied** — that is now four slices running.

## Not Verified

| What | Why |
|---|---|
| Whether the player *notices* a tower dying | Needs a human. The cue is verified as visible in a still frame; whether it reads during play is a different claim. Follow-up `destruction-feedback` |
| Destructible towers on `gauntlet` | Sappers are only in the `crossroads` wave table. `gauntlet`'s cliff is untouched. Follow-up `gauntlet-sappers` |
| Any response to destruction other than rebuilding | There is no repair mechanic. Follow-up `tower-repair` |

## Branch Resolution

None — PASS on all twelve criteria.
