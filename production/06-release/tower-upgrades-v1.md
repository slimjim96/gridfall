# Tower Upgrades — v1

**Slug:** `tower-upgrades` · **Status:** done · **Verified at trace:** `17ed9dcac986f821`

## What Shipped

Towers have levels. Spending gold on a tower you already own is now an alternative to placing another
one — the gold sink three balance passes said was missing.

- `UpgradeCommand`, handled in phase 1. No block check: an upgrade occupies the same cell and changes
  no route, so it cannot seal a lane and never dirties the grid.
- `TowerLevel` on `SimState` — hashed, snapshotted, and covered by a test, like all state.
- Upgrade tracks are **data** in the tower JSON: cost, damage multiplier, range multiplier. Damage and
  squared range for every level are resolved once at load, so the tick loop never multiplies.
- Selling refunds half of **everything spent**, upgrades included, so upgrade-then-sell cannot profit.
- Level is drawn as height plus brightness — height first, because it survives greyscale.

## Player-Facing Change

In principle: commit to a position instead of spreading thin. **In practice, not yet reachable** —
there is no input binding for upgrade in the gameplay scene. The command works and the balance sim
uses it; a human cannot press anything. Follow-up `upgrade-input`.

## The number this was for

| | Before | After |
|---|---|---|
| Idle gold at wave 11 | 1,090 | **34** |
| Upgrades bought per run | — | 16.9 |

**Difficulty is unchanged** — leak 2.8%, runs lost 33.3%, identical. Every death is at wave 3, before
upgrades are affordable, so a late-game sink cannot touch it. That is the correct outcome, not a
disappointment: the slice fixed the economy, which is what it set out to do.

## The design rule, enforced by a test

Upgrading costs **more per point of damage** than a new tower. If it were cheaper the player would
never spread out, one super-tower would do the work, and mazing — pillar 1 — would quietly stop
mattering.

`DamagePerGold_FallsWithEachLevel` asserts this against the shipped data, not the intent. A content
author who makes upgrades too cheap breaks the build.

## New Tuning Knobs

| Knob | Owner | Default set? |
|---|---|---|
| `upgrades[].cost` (arrow, cannon) | content-data | No — placeholder, ×2.2 and ×4.8 of base |
| `upgrades[].damageMultiplier` | content-data | No — placeholder, ×2 and ×4 |
| `upgrades[].rangeMultiplier` | content-data | No — placeholder, ×1.0 then ×1.15 |

## Follow-Ups Not Done

| Item | Workspace | Slug |
|---|---|---|
| Input binding so a human can actually upgrade | presentation | `upgrade-input` |
| **Wave 3 is an economy cliff** — 21.5% leak with 12 gold in hand. Now the single thing between this and a tunable game | content-data | `early-economy` |
| Refresh the visual baselines; the committed one is stale | presentation | `refresh-baselines` |
| Branching upgrade paths, if the linear track proves too thin a decision | game-design | `upgrade-branches` |

## Known Not Verified

- **Tower level is visible on the board** — implemented, compiles, never seen. The X display used for
  the last four slices was an xrdp session that has since ended.
- **`presentation/docs/board-baseline.png` is stale** and will produce a false failure if diffed. The
  screenshot seed now includes an upgraded tower.
- Whether upgrading *feels* like a real choice. Needs a human, and needs the input first.
