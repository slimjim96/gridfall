# Play Policy — Fussiness — Verification

**Slug:** `policy-fussiness` · **Status:** review · **Date:** 2026-08-09
**Verdict:** PASS — and the acceptance criterion the slice was proposed under is **disproved by its own
measurement**, which is the result worth keeping

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **234 passed**, 0 failed (was 228; +6) |
| Determinism trace | PASS | 30/30 checkpoints, **no re-record** |
| `Verify maps` | PASS | exits 0, twelve maps, no new warnings |
| Balance targets | PASS (unchanged) | All twelve maps, 150 runs, seed 1 — **byte-identical** to the pre-change binary |
| Visual capture | N/A | Nothing the player sees was touched |

**The determinism gate deserves a sentence.** `Gridfall.Verify` is outside the simulation, so no policy
change can move a trace. But this slice also edited `Gridfall.Core` — extracting
`VisitorDef.ServingTaken` and pointing `DamageSystem` at it. `replay` passing unchanged is the proof
that extraction was behaviour-identical, and it is the only thing that could have proved it.

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | The policy ranks stations by serving *actually landed*, not base serving | PASS | `VisitorCensus.ValuePerGold`; `AnUnfussyWave_BuysTheCheapFastStation`, `AFussyWave_BuysBurstInstead` |
| 2 | The crossover sits where the arithmetic says | PASS | `TheCrossoverIsAtHalfTheWave_ByAppetite` — 45% share buys arrow, 56% buys cannon |
| 3 | The policy uses no information a player lacks | PASS | `ThePolicyKnowsOnlyWavesThatHaveStarted` — census counts waves with index `< WaveIndex` only |
| 4 | It reaches an actual `BuildCommand`, not just a ranking | PASS | `ThePolicyActuallyBuysTheCannon_WhenTheCensusWarrantsIt` — real `Sim`, cannons are the majority buy after wave 1 |
| 5 | The fussiness rule has one authority | PASS | `VisitorDef.ServingTaken`; `DamageSystem` calls it; `replay` unchanged |
| 6 | The balance report says which stations were bought | PASS | new `station mix` line, every run |
| 7 | `Verify curve` stops assuming one station | PASS | per-wave `srv/gold` column, 0.01333 → 0.01163 across `crossroads` |
| 8 | **"It would move every figure in `example-levels.md` and the balance report"** | **DISPROVED** | All twelve maps byte-identical. See below |
| 9 | Existing balance targets still hold | PASS | Unchanged, therefore unchanged |

## Criterion 8: the slice's own premise was wrong, and that is the finding

[`next-steps.md`](../docs/next-steps.md) §1 predicted a large blast radius: *"every balance number in
the repo describes a game that never uses half of it… it would move every figure."*

The first half is true. The second is false. After the change the policy **can** buy a cannon and, on
all twelve shipped boards, still never does — `station mix` reads `arrow-station 100%, cannon 0%`
everywhere, and every line of every balance report matches the pre-change binary exactly.

The reason is arithmetic, not a bug. The crossover is at average `fussiness` **4**, weighted by
appetite. The most armoured wave in any shipped table averages **1.53**. The arrow station stays 22.5%
better value even there. Full pricing of the three ways to close that gap — and the discovery that
raising `fussiness` is not one of them — is in the
[balance report](../../content-data/docs/reports/2026-08-09-policy-fussiness-balance.md).

**Two blocks, not one.** Ranking was the obvious one. The second was structural and had nothing to do
with fussiness: the policy bought the best station it could afford *this tick* with no reserve, so on
any roster the cheapest station is bought the instant its price is reached and gold never approaches
the price of anything else. Census-awareness alone left the policy building 2 arrows and 0 cannons on a
board of pure husks. Both fixes were needed and neither is sufficient.

## What this slice did NOT verify

- **That the beginner model is right.** The policy buys against the *average* of what it has met. A
  real player buys against what is *leaking*, which needs leak attribution the harness does not have.
- **That the cannon is correctly priced.** It is unused, which is not the same as being wrong. The
  balance report prices what it would take to make it the right buy and hands the call back.
- **Fussiness in the upgrade decision.** `TryUpgrade` still ranks by coverage.

## Loop-back

None. No criterion failed; criterion 8 was a prediction attached to the request, and disproving it
required no code change — only measuring both sides.
