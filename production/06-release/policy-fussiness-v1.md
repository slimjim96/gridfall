# Play Policy — Fussiness — v1

**Slug:** `policy-fussiness` · **Status:** done · **Date:** 2026-08-09
**Verified at trace:** `crossroads-baseline`, unchanged · **Tests:** 228 → 234

## What Shipped

The balance harness can now tell the two shipped stations apart.

- **`VisitorCensus`** (`Gridfall.Verify`) — what the player has already fought, weighted by appetite,
  and what a station is therefore worth per gold against it.
- **`PlayPolicy` ranks by effective serving**, not base serving. `max(1, serving - fussiness)` per hit,
  averaged over the census.
- **`PlayPolicy` will not substitute down.** When the best buy is unaffordable it holds instead of
  buying a cheaper station. Without this the census changes nothing: the cheapest station is otherwise
  bought the instant its price is reached and gold never approaches the price of anything else.
- **`VisitorDef.ServingTaken(amount)`** (`Gridfall.Core`) — the fussiness rule, now with one authority.
  `DamageSystem` calls it. `replay` passed unchanged, which is the proof the extraction was inert.
- **`Verify balance` prints a `station mix` line.** Which stations a run bought, not just how many.
- **`Verify curve` computes `srv/gold` per wave** against that wave's composition. It printed one
  constant from base stats, which was the same blind spot in a second place.
- Six tests, including the one that will tell you when the content finally makes burst correct.

No simulation behaviour changed. No content values changed.

## The result that matters

**All twelve maps came back byte-identical.** The policy can now buy a cannon and still never does:

```
  station mix     arrow-station 100%, cannon 0%
```

The premise this slice was picked up under — *"it would move every figure in `example-levels.md` and
the balance report"* — is **disproved**. The crossover is at average `fussiness` 4 by appetite; the most
armoured wave in any shipped table averages **1.53**; the arrow station stays 22.5% better value even
there. The husk is present in the content and inert in the decision.

| Lever to close the gap | Cost |
|---|---|
| More husks | 19 → **90** in wave 12 (48% of its appetite) |
| Fussier husks | **impossible** — past fussiness 11 the arrow is floored at 1 per hit and further armour only subtracts from the cannon |
| A better cannon | cost 90 → **73**, or serving 40 → **49** |

## Two roster targets, failing, measured for the first time

`balance-targets.md` has asked for *"any single station's presence in winning runs ≤ 70% — a must-pick
is a design failure"* since it was written. Measured today: **100%**. Nothing had ever printed the mix,
so nothing had ever checked it. It is checked on every balance run from here.

## What it cost

The "no reserve" rule now bends by up to `price - 1` gold across at most one wave — the policy holds for
the station it has chosen. The wait is self-limiting: holding makes the build fail, which is what pulls
the next wave, which is what pays for the station. The idle-gold pathology that rule exists to detect is
thousands of gold across a run, not eighty-nine for one wave.

## Records

- [Verification](../05-verify/policy-fussiness-report.md)
- [Balance report](../../content-data/docs/reports/2026-08-09-policy-fussiness-balance.md) — the pricing above
- [Tool note](../../tooling/specs/policy-fussiness-tool-note.md) — what it reuses, and the honesty boundary
- `balance-targets.md` §Station targets, §Visitor targets — the fussiness ceiling of 11 and the 48% share floor
- `docs/engine-guide/07-content-loading.md` §Fussiness — the one authority, and the ceiling
