# Tower Repair — Build Notes

**Slug:** `tower-repair` · **Status:** review

Source lives at the repository root, not here — see `docs/conventions.md` §Where the source lives.

## What Was Built

| File | Change | Tick phase |
|---|---|---|
| `Gridfall.Core/Commands.cs` | `RepairCommand`, `CommandKind.Repair = 5` | 1 |
| `Gridfall.Core/Systems/CommandSystem.cs` | `Repair(...)` | 1 |
| `Gridfall.Core/Content/Defs.cs` | `RepairPercent`, `TotalSpentAt`, `RepairCostFor` | — |
| `Gridfall.Core/Content/ContentLoader.cs` | parse `repairPercent`; `ValidateRepairCurve` | — |
| `Gridfall.Core/Events/SimEvent.cs` | `TowerRepaired`, `RepairRejected`; `NotDamaged`, `WaveInProgress` | 9 |
| `Gridfall.Verify/PlayPolicy.cs` | the scripted player repairs, between waves | — |
| `Gridfall.Verify/Program.cs` | balance report gains repairs, repair gold, **towers lost** | — |
| `godot/GameplayScene.cs` | middle-click binding, hover prompt, `repair` shot seed | — |
| `godot/Hud/Hud.cs` | repair prompt label, two refusal cases, help line | — |
| `godot/Placeholders/Palette.cs` | `Hint` — an offer, not a warning | — |
| `Gridfall.Tests/RepairTests.cs` | 22 tests | — |
| `content-data/towers/*.json` | `repairPercent: 60` on both | — |

**`SimState` is untouched.** No new field, no new hash entry, no new snapshot entry.

## Decisions Made While Building

### The design's arithmetic was wrong, and the validator found it before the sim did

The design spec said `repairPercent` was bounded `0 < p < 100` as a fraction of *total spend*. That is
wrong: selling refunds half, so the sell-and-rebuild round trip costs `S/2`, and the bound as a fraction
of spend is `p < 50`. The shipped default of 60 would have violated its own wall.

Caught immediately, because `ValidateRepairCurve` threw at load on the first run. This is ADR-0007
earning its place on day one — the check was written to catch a *future* content edit and instead caught
the design it was written from.

Fixed by re-anchoring the knob: `repairPercent` is now a percentage **of the sell-and-rebuild cost**, so
100 *is* the wall and the knob carries its own bound. The denominator became `200 × maxHp` (100 percent
× 2 for the refund) rather than `100 × maxHp`.

### Ceiling division, and where the exploit actually was

`(numerator + denominator - 1) / denominator`. With truncation, repairing 5 HP ten times costs less than
repairing 50 HP once, because each division floors away the remainder. Rounding up inverts it, so
granular repair is strictly worse and there is nothing to police. Test:
`ManySmallRepairs_AreNeverCheaperThanOneLargeOne`.

### `long` intermediate

`TotalSpentAt × RepairPercent × missingHp` reaches ~10¹⁴ in the overflow test. Int overflow would be
*deterministic* — every machine would agree on the same wrong number, and the state hash would confirm
it. That is the worst kind of determinism bug, so the guard is a test with an exact expected value
rather than a range.

### The balance policy had to learn to repair, or criterion 11 would have passed vacuously

A policy that never repairs leaves towers-lost exactly where `tower-combat` left it, and the criterion
would have "passed" while measuring nothing. `PlayPolicy` now repairs the worst-hurt tower below 40%
health, between waves, before building.

Worst-hurt by health **fraction**, not points missing: an 800-hp tower at 200 is in more danger than a
1440-hp tower at 400, and the beginner being modelled is reading the colour, which tracks the fraction.
Compared by cross-multiplication so there is no division and no float.

### The mechanic was measured, not argued

Repair-at-any-time was built first, exactly as the design specified, and it drove towers lost per run to
**0.0** — at `repairPercent` 60 and again at 96, the highest value the loader accepts. Both balance
targets read "ok" throughout.

The between-waves rule was then tested as a *policy* restriction first (a static flag on `PlayPolicy`,
`--repairBetweenWavesOnly`), because that was a two-line experiment against a Core change. It produced
5.8 towers lost. Only then was the rule moved into `CommandSystem`, and the Core-enforced numbers came
out identical to the policy-restricted ones — which is the confirmation that the *rule* was doing the
work and not some artefact of how the policy was written.

The experiment flag was removed afterwards. A knob that exists only to answer one question should not
outlive the answer.

### Sapper throughput was the wrong lever, and it is worth recording why

Before trying the between-waves rule, the obvious fix was tried: make sappers hit harder so repair
cannot keep up. Quadrupling attack rate (`attackCooldown` 1.2 → 0.3) recovered only 1.6 towers lost per
run and pushed runs-lost to 42%, well past the 15–30% band.

That is the shape of the whole finding. Throughput on the threat side and throughput on the counter side
are not symmetric levers, because the player's counter is gated by gold and gold scales with the wave.
Making the threat bigger makes the game harder without making the counter fail. Restricting *when* the
counter is available is what makes it fail.

`attackCooldown` was returned to 1.2. No enemy data changed in this slice.

### The HUD names the rule where the player meets it

Hovering a damaged tower mid-wave shows `repair between waves` rather than a price. A rule the player
only discovers by clicking and being refused is a rule they experience as the game not working.

The prompt calls `TowerDef.RepairCostFor` rather than recomputing the curve. A second copy of the cost
formula in the view would be the copy the player actually reads.

## Trace

**No re-record.** Repair is inert unless a `RepairCommand` is enqueued, and no recorded trace enqueues
one, so `crossroads-baseline` had to be unchanged — and was, at all 30 checkpoints. A hash shift here
would have meant something moved that should not have. Asserting that *before* reaching for `record` is
the point of criterion 16.

## Baselines Refreshed

`board-baseline.png` and `sapper-baseline.png` both change, because the HUD help line gained
`middle click: repair`. The simulation hashes are unchanged (`b9c3bc7c95e6f726` and
`a15d4919788939c8`, with the worst-hurt tower still at cell (2,6) at 28%), confirming the difference is
purely the new HUD text.

New: `repair-baseline.png`, from the `repair` shot seed. Byte-identical across two runs.

## What I Would Flag to the Next Slice

- `repairPercent` moves gold spent, not towers lost. Tuning it is nearly pointless while the
  between-waves rule is doing the work. If the repair *bill* is the complaint, it is the right knob; if
  tower survival is the complaint, it is not.
- Selling still works mid-wave while repairing does not. That asymmetry is deliberate and untested
  against a player who exploits it — a competent player may find that cutting towers loose mid-wave and
  rebuilding between waves beats repairing. The beginner policy never sells, so the balance sim cannot
  see it.
