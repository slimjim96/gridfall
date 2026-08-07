# Tower Repair — v1

**Slug:** `tower-repair` · **Status:** done · **Verified at trace:** `crossroads-baseline`, unchanged

## What Shipped

Destruction has an answer that is not "build another one" — **between waves**.

- **`RepairCommand`**, the fifth command, phase 1. Restores a damaged tower to full for gold.
- **Only between waves.** While a wave runs it is refused with "Not while a wave is running." This is
  the mechanic, not a restriction on it ([the design's revision 2](../02-design/tower-repair-design.md)).
- **Cost scales with damage taken** and is anchored to everything spent on the tower, so a level-3
  tower is proportionally more expensive to keep alive than a level-1 one.
- **`repairPercent`**, one knob, expressed as a percentage of the sell-and-rebuild cost so that 100 *is*
  the wall. Both towers ship at 60.
- **The wall is enforced at load, not trusted** —
  [ADR-0007](../../engine-systems/decisions/ADR-0007-repair-bounds-validated-at-load.md).
- **No new simulation state.** `TowerHp` and `Gold` already existed; the rate limit rides on
  `WaveActive`, which was already hashed. Nine phases are still nine.
- Middle click to repair. Hovering a damaged tower shows the price and the binding.

## Player-Facing Change

Between waves, damaged towers are a bill. You can pay it, partly pay it, or spend the gold on a new
tower and accept that something on the front line may not survive wave 9.

During a wave you watch. You can still **sell** a doomed tower mid-wave — cutting it loose is allowed,
saving it is not.

Reachable in the running game today: sappers from wave 5, towers darken as they are chewed on, and
between waves each one carries a price to put right.

## The result that matters

The first build shipped repair the way the design specified — available whenever you hold gold — and it
**switched tower destruction off entirely**:

| | built | standing | **lost** | leak | runs lost |
|---|---|---|---|---|---|
| `tower-combat` (no repair) | 55.7 | 45.8 | **9.9** | 1.3% | 28.5% |
| Repair any time, cost 60 | 45.6 | 45.6 | **0.0** | 1.2% | ok |
| Repair any time, cost 96 *(the ceiling)* | 44.3 | 44.3 | **0.0** | 1.3% | ok |
| **Repair between waves** | 51.6 | 45.8 | **5.8** | 1.2% | ok |

96 is the highest value the loader accepts, so the two middle rows bracket **the entire legal range of
the price knob** — and the whole range ends every run with every tower standing.

**Both balance targets read "ok" the whole time.** The previous slice's central result was being deleted
and no target could see it.

## Why price was never the lever

`tower-combat` found that tower destruction is driven by **throughput**, not damage per hit. The
consequence it did not draw:

> A throughput-driven threat cannot be countered by an action available at unlimited rate.

Given gold, the player wins the throughput race at any affordable price — and repair is *bounded* cheap,
because it must beat sell-and-rebuild, which costs half a tower, in an economy that moves 6,479 gold.
Cost decides how much the immortality costs, not whether it is available.

Raising the threat instead does not work either: quadrupling sapper attack rate recovered 1.6 towers and
pushed runs-lost to 42%, past the band. **No enemy data changed in this slice.**

## New Tuning Knobs

| Knob | Owner | Default set? |
|---|---|---|
| `repairPercent` on a tower def | content-data | **Yes** — 60 on both towers |

One knob, and this slice proved it is the *less* important half. It moves the repair bill (169 gold per
run, 2.6% of lifetime income) and does not move tower survival at all. If the bill is the complaint it
is the right knob; if survival is the complaint it is not.

## Visible State

| State | Cue |
|---|---|
| Tower damaged | Darker and redder as health falls — unchanged from `tower-combat` |
| Repair available | On hover: `Arrow Tower at 58% -- repair 7 gold (middle click)` |
| Repair unavailable | On hover: `Arrow Tower at 58% -- repair between waves` |

The rule is named where the player meets it. A rule discovered only by clicking and being refused is a
rule they experience as the game not working.

Verified in `presentation/docs/repair-baseline.png`.

## Also Worth Knowing

- **The loader caught the design's own arithmetic on day one.** `repairPercent` was specified as a
  fraction of total spend bounded at 100; selling refunds half, so the real bound was 50 and the shipped
  default of 60 violated its own wall. ADR-0007 was written to catch a future content edit and instead
  caught the design it came from.
- **Ceiling division, deliberately.** Truncating would make ten small repairs cheaper than one large
  one. Rounding up makes granular repair strictly worse, so the exploit closes arithmetically rather
  than being policed.
- **The balance policy had to learn to repair** before criterion 11 meant anything. A policy that never
  uses a mechanic verifies nothing — it would have "passed" by measuring the previous slice unchanged.
- `board-baseline.png` and `sapper-baseline.png` are refreshed: the HUD help line gained
  `middle click: repair`. Both simulation hashes are unchanged, so the difference is purely the text.

## Standing rule this slice adds

`tower-combat` left one: *when a slice adds a mechanic, a balance target is necessary and not
sufficient.* This slice needed the converse:

> **Measure that the previous slice's mechanic is still doing something.** A new mechanic can satisfy
> every target while quietly deleting the one before it.

`balance` now prints **towers lost per run** on every run, so the next slice cannot delete this one
without saying so.

## Follow-Ups Not Done

| Item | Workspace | Slug |
|---|---|---|
| Selling works mid-wave and repairing does not — cutting towers loose may beat repairing | game-design | `salvage-value` |
| Wave 3 leaks 14.1%, worst wave for **five** passes running | content-data | `early-economy-2` |
| Sappers on `gauntlet` — still no destructible-tower pressure there | content-data | `gauntlet-sappers` |
| Per-archetype `repairPercent`; both towers ship at 60 | content-data | `repair-tuning` |
| Sweep `attackCooldown` and `attackRange` independently | content-data | `sapper-tuning` |

## Known Not Verified

- Whether the between-waves rule reads as a **budget** or as a **restriction**. The decision is verified
  to exist; whether it is tense or annoying needs a human (`repair-feel`).
- Whether a competent player who sells mid-wave beats one who repairs between them. The beginner policy
  never sells, so the balance sim cannot see it.
