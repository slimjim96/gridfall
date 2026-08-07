# Tower Upgrades — Design

**Slug:** `tower-upgrades` · **Status:** done · **Supersedes for implementation:** the requirements file

## Behavior

1. Every tower has a **level**, starting at 1. Its definition lists the upgrades available above that.
2. Clicking an existing tower with the upgrade input spends gold and raises it one level. The effect is
   immediate on the next tick, like every other command.
3. An upgraded tower is visibly taller and brighter. Level is legible from the normal camera.
4. At maximum level the upgrade is refused with "already at maximum".
5. Selling refunds half of **everything spent** — base cost plus every upgrade paid for — so upgrading
   then selling can never profit.

## Player-Visible States

| State | What the player sees |
|---|---|
| Level 1 | The tower as built |
| Level 2, 3 | Taller, and a brighter accent on the same palette slot |
| Upgrade affordable | (deferred — no build UI exists yet to show it in) |
| Upgrade refused | Refusal message in the HUD, same channel as a refused build |
| At max level | "Already at maximum" on attempt |

## The central tension

**Upgrading must not dominate building.** If a level-2 tower gives more damage per gold than a second
tower, the player never spreads out and pillar 1 quietly dies — mazing stops mattering because one
super-tower does the work.

So the shape is: **an upgrade costs more per point of damage than a new tower does.** Building is the
efficient play while space and coverage remain; upgrading is what you do when the good spots are gone.
That makes board saturation the trigger, which is exactly the hole in the economy.

Concretely, as authored data rather than code:

| Level | Cost | Damage | Effective damage/gold |
|---|---|---|---|
| 1 (base) | 50 | 12 | 0.24 |
| 2 | +110 | ×2.0 | 0.15 |
| 3 | +240 | ×4.0 | 0.12 |

Numbers are the content author's; the shape — **rising cost, falling efficiency** — is the design rule.

## Tuning Knobs

| Knob | Raising it… | Expected direction |
|---|---|---|
| `upgrades[].cost` | Makes upgrading a later-game decision | Up, if upgrades start dominating builds |
| `upgrades[].damageMultiplier` | Makes upgrading more attractive at the same price | Down, same reason |
| `upgrades[].rangeMultiplier` | Widens coverage, which competes with mazing | Cautious — range is stronger than damage on a maze map |
| Sell refund fraction | Makes repositioning cheaper | Fixed at half for now |

## Interaction Rules

- **Vs. mazing:** an upgraded tower blocks exactly like an unupgraded one. Upgrading never changes a
  route, so it cannot seal a lane and needs no block check.
- **Vs. selling:** refund is half of total spend including upgrades. Never a profit.
- **Vs. targeting:** level changes damage and range, never the targeting rule. A frost tower upgraded is
  still a frost tower.
- **Vs. a future upgrade tree:** deliberately **not now**. Branching paths are a real design with real
  cost, and the economy problem does not need them. One linear track first; branch later if the linear
  one proves too thin a decision.

## Rejection Cases

| Refused input | Message |
|---|---|
| Already at maximum level | "Already at maximum." |
| Not enough gold | "Not enough gold." |
| No tower on that cell | Silence — the click simply does nothing |

## Acceptance Criteria (carried + added)

1–10 carried unchanged from the requirements.

11. An upgrade never changes the walkable grid, and never triggers a pathing recompute.
12. Damage per gold **falls** with each level, verified from the shipped data, so building stays the
    efficient play while good spots remain.
