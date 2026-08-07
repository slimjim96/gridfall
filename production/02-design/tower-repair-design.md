# Tower Repair — Design

**Slug:** `tower-repair` · **Status:** done · **Supersedes for implementation:** the requirements file

> **Revision 2.** The first version of this spec put repair on the same footing as every other
> command — available whenever the player has gold. Verification measured that and found it drove
> towers lost per run to **exactly zero at every legal price**, silently reverting `tower-combat`.
> The slice looped back here from 04. The between-waves rule below is the fix, and it is the mechanic.
> The original reasoning is kept in *What revision 1 got wrong*, because the failure is the finding.

## Behavior

1. Every tower already has structure health (`tower-combat`). Repair restores it toward the maximum for
   its def.
2. **Repair is only possible between waves.** While a wave is running it is refused, visibly.
3. Repairing an existing tower spends gold and restores it to full. Instant, applied in phase 1, like
   the other four commands.
4. Repairing an undamaged tower is refused with "not damaged". It is not a free no-op — a silent
   success that charges nothing is indistinguishable from a bug.
5. Repair restores **health only**. Level, cooldown, and position are untouched.

## Why between waves

This is the whole design, and it was settled with numbers rather than argument.

`tower-combat` established that **tower destruction is driven by throughput** — 62 sappers over twelve
waves, not damage per hit. Its own release note says so, and warns against tidying the HP and damage
numbers toward each other.

The consequence nobody drew at the time: **a throughput-driven threat cannot be countered by an
action available at unlimited rate.** If the player may repair whenever they hold gold, they win the
throughput race at any price they can afford, and the tower becomes immortal. Measured, at three prices
spanning the entire legal range of the knob:

| repairPercent | towers built | standing | **towers lost** | leak | runs lost |
|---|---|---|---|---|---|
| — (no repair, `tower-combat`) | 55.7 | 45.8 | **9.9** | 1.3% | 28.5% |
| 60, any time | 45.6 | 45.6 | **0.0** | 1.2% | 25.5% |
| 96, any time *(the ceiling)* | 44.3 | 44.3 | **0.0** | 1.3% | 28.0% |
| 60, **between waves only** | 51.6 | 45.8 | **5.8** | 1.2% | 26.0% |

Note the third row. 96 is the highest value the loader will accept, and it still ends every run with
every tower standing. **No legal price fixes this**, because the wall that makes repair attractive is
the same thing that keeps it cheap: repair must beat sell-and-rebuild, which costs half a tower, and a
tower costs 50–90 gold in an economy that moves thousands. Price was never the lever.

Between waves, repair *reduces* losses rather than erasing them — 9.9 down to 5.8 — which is what a
counter-mechanic is supposed to do.

It also makes the decision better. During a wave, repair is a reflex: click the tower that is about to
die. Between waves it is a budget: *three damaged towers, 300 gold, wave 9 next — repair all three, or
repair one and build two?* That is the decision the requirements asked for, and it lands in the phase
where the player is already planning.

## The cost curve

Let `S` = everything spent on the tower so far (base cost plus every upgrade) — the same `S` that
`SellValueAt` halves.

**The upper wall.** A player who does not repair can sell the damaged tower for `S/2` and rebuild it to
the same level for `S`. Net cost `S/2`, which is exactly `SellValueAt(level)`. So repairing from zero to
full must cost strictly less than that, or nobody ever repairs.

`repairPercent` is therefore expressed **as a percentage of that alternative**, so the knob carries its
own bound: 100 *is* the wall.

```
repairCost = ceil( S × repairPercent × missingHp / (200 × maxHp) )
```

The 200 is 100 (percent) × 2 (the sell refund).

- **Proportional to damage taken**, so there is no "wait until 1 HP" optimum.
- **Ceiling division, not truncating.** Integer truncation would make ten small repairs cheaper than one
  large one. Rounding up makes granular repair strictly worse, so the exploit closes arithmetically
  instead of being policed.
- **The bound is enforced at load, not trusted** — [ADR-0007](../../engine-systems/decisions/ADR-0007-repair-bounds-validated-at-load.md).

### Repair cost rises with investment

Anchored to `S` and not to base cost, so a level-3 tower costs proportionally more to maintain than a
level-1 one:

> Concentrating gold into one upgraded tower carries a **maintenance liability** proportional to the
> concentration.

`tower-upgrades` made spreading-vs-concentrating a decision about damage per gold. Repair adds a second
axis to the same decision. No new piece, a deeper interaction between two that exist. Pillar 5.

## Player-Visible States

| State | What the player sees |
|---|---|
| Tower damaged | Darker and redder as health falls — already shipped in `tower-combat` |
| Repair available | On hover: `Arrow Tower at 58% -- repair 7 gold (middle click)` |
| Repair unavailable (wave running) | On hover: `Arrow Tower at 58% -- repair between waves` |
| Repair refused (not damaged) | "Not damaged." |
| Repair refused (wave running) | "Not while a wave is running." |
| Repair refused (gold) | "Not enough gold." |

The rule is named on hover, not only in a refusal after the player has already clicked.

## Tuning Knobs

| Knob | Raising it… | Expected direction |
|---|---|---|
| `repairPercent` (tower def) | Makes repair more expensive | Weak. It moves gold spent, not towers lost — the between-waves rule does that work. Leave at 60 unless the repair *bill* is the problem |
| The between-waves rule itself | Not a knob. It is the mechanic | — |

One knob, and this slice proved it is the *less* important half. Worth saying plainly: the tuning knob
the design specified turned out not to control the thing the design cared about.

## Interaction Rules

- **Vs. mazing:** repair occupies the cell the tower already occupies. The walkable grid is never
  touched, phase 2 is never dirtied, no block check — identical to upgrade.
- **Vs. upgrade:** orthogonal. Repairing does not reset level; upgrading does not heal. Explicitly
  tested, because "upgrade also repairs" is the kind of convenience that quietly deletes the mechanic.
- **Vs. selling:** unchanged. Sell still refunds `S/2` regardless of damage, so salvaging a nearly-dead
  tower stays available as a retreat — and unlike repair, **selling still works mid-wave**. That is a
  real asymmetry and a real decision: mid-wave you may cut a tower loose, but you may not save it.
- **Vs. destruction:** a destroyed tower cannot be repaired. There is no corpse and no grace period.

## What revision 1 got wrong

Kept deliberately, because the mistake is more useful than the fix.

Revision 1 argued: *"the interesting case is repairing during a wave, with gold you wanted for something
else"*, and listed between-waves repair under **Deliberately Not Doing** on the grounds that it "deletes
the decision".

That reasoning had the sign backwards. Mid-wave repair does not create a decision, it creates a reflex —
and because the threat is throughput-based and repair is cheap, the reflex always wins. Restricting
repair to between waves is what *creates* the budget decision, by forcing the player to commit gold
before they know where the damage will land.

The error was arguing about the mechanic's feel without checking whether it left the previous slice's
result standing. `tower-combat`'s verify report had already written the rule that would have caught it:

> **when a slice adds a mechanic, a balance target is necessary and not sufficient.** Measure that the
> mechanic is still doing something.

This slice needed the converse, and it is worth adding to the standing rules:

> **Measure that the previous slice's mechanic is still doing something too.** Both balance targets
> passed while `tower-combat` was being switched off.

## Deliberately Not Doing

- **Auto-repair between waves.** Repairing is now a between-waves action, so making it automatic would
  delete the decision entirely rather than merely relocating it. Still a no.
- **Repair over time.** A new system and a new phase for a decision that does not need one.
- **Sell value scaling with damage.** A change to a shipped mechanic; its own slice and its own balance
  run. Follow-up: `salvage-value`.
- **Per-archetype `repairPercent`.** Both towers ship at 60. Differentiating maintenance cost by
  archetype needs a sweep this slice did not run. Follow-up: `repair-tuning`.

## Acceptance Criteria (carried + added)

1–12 carried from the requirements.

13. Repairing does not change the tower's level, and upgrading does not change its health.
14. Ten small repairs cost **at least** as much as one repair of the same total health.
15. `repairPercent` is authored data on every shipped tower def, absent from code as a literal.
16. **Repair is refused while a wave is running**, and allowed once it clears.
17. **Towers lost per run stays strictly above zero** in the balance sim. This is criterion 11 with a
    number attached, and it is the one that failed in revision 1.
