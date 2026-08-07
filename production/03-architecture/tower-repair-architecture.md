# Tower Repair — Architecture

**Slug:** `tower-repair` · **Status:** done · **Owner:** systems-architect
**Implement against this note, not the design spec.**

> **Revision 2**, after the slice looped 04 → 02. The between-waves rule below did not exist in
> revision 1; it was added when verification showed repair-at-any-time drove towers lost to zero at
> every legal price. Structurally it changed almost nothing — the gate is `WaveActive`, which was
> already hashed — which is why the "no new state" property survived the redesign.

## The shape of this slice

Repair is a fifth command. It reads two numbers, writes two numbers, and touches nothing else.

The load-bearing property, stated up front because it determines how much of the determinism work is
actually needed:

> **This slice adds no simulation state.**

Repair mutates `SimState.TowerHp` and `SimState.Gold`. Both are already hashed, already snapshotted,
already swap-remove-safe — `tower-combat` did that work. Criteria 9 and 10 are therefore *inherited*
rather than built, and the risk profile of this slice is closer to `tower-upgrades` than to
`tower-combat`.

What is genuinely new is **arithmetic**: a cost function with two walls, one provable and one measured.
That is where the attention goes.

## Systems Changed

| System | Change |
|---|---|
| `Commands.cs` | `CommandKind.Repair = 5`; `RepairCommand(int TowerId)`; a case in `CommandQueue.Enqueue` |
| `Systems/CommandSystem.cs` | `Repair(...)`, dispatched in phase 1 |
| `Content/Defs.cs` | `TowerDef.RepairPercent`; `TowerDef.RepairCostFor(level, missingHp)` |
| `Content/ContentLoader.cs` | parse `repairPercent`; **validate the cost bound and throw** (ADR-0007) |
| `Events/SimEvent.cs` | `EventKind.TowerRepaired`, `EventKind.RepairRejected`; `RejectReason.NotDamaged = 10`, `RejectReason.WaveInProgress = 11` |
| `Gridfall.Verify/PlayPolicy.cs` | the scripted player repairs below a health threshold, between waves, before building |
| `Gridfall.Verify/Program.cs` | balance report gains repairs bought, gold spent repairing, and **towers lost** |

**Not changed:** `SimState` (no new field), `PathSystem` (no grid mutation), `SimStateView` (the view
already exposes `TowerHp` and reads max HP from the def), and phases 2–9.

## Tick Placement

Phase 1, `CommandSystem`, alongside `Upgrade`. Same reasoning, and it is short:

- Repair occupies the cell the tower already occupies. The walkable grid does not change, so the dirty
  flag is never set and phase 2 is never triggered. **No block check, no `WouldRemainConnected` call.**
- Repair is applied, not simulated. It deals no damage and moves nothing, so it respects phase 1's rule.
- Commands drain in insertion order, so two repairs of the same tower in one tick resolve in the order
  enqueued: the first heals and charges, the second is refused as `NotDamaged`. Deterministic by
  construction, no special case needed.

No ADR needed for the placement — unlike ADR-0006's enemy attacks, this is not a new kind of work. It is
the fifth instance of an existing one.

## Data

### `TowerDef.RepairPercent` — `int`, authored

JSON key `repairPercent`. Design bound: `0 < repairPercent < 100`, where 100 sits exactly on the
sell-and-rebuild wall.

**Default when absent:** `0`, meaning *unrepairable*, and every repair on such a tower is refused as
`NotDamaged`… no. That conflates "cannot" with "need not". **Default is `60`**, and both shipped tower
defs author it explicitly. Rationale: repair is not an opt-in property of a tower the way `attackDamage`
is an opt-in property of an enemy — every tower can be repaired unless a future design says otherwise,
and ADR-0007 records why "unrepairable" must eventually be its own field rather than an extreme cost.

### `TowerDef.RepairCostFor(int level, int missingHp)` — `int`

Lives in `Defs.cs` directly beneath `SellValueAt`, because the two functions define each other's bound
and reading one without the other is how the wall drifts.

```
S    = Cost + sum(Upgrades[0..level-2].Cost)      // same S as SellValueAt halves
num  = (long)S * RepairPercent * missingHp
den  = 100L * Hp
cost = (int)((num + den - 1) / den)               // ceiling
```

Three properties the implementation must preserve:

1. **`long` intermediate.** `S × percent × missingHp` reaches ~10⁹ at plausible values (S≈2000,
   percent=100, missingHp=5000) and int overflow is silent. Integer arithmetic is exact and therefore
   deterministic; overflow is exact and therefore *deterministically wrong*, which is worse.
2. **Ceiling, not truncation.** Truncating division makes ten small repairs cheaper than one large one —
   a free heal for anyone willing to click. Rounding up makes granular repair strictly non-advantageous,
   so the exploit closes arithmetically instead of being policed. This is acceptance criterion 14.
3. **No `Fix32`, no float.** The whole computation is integral. Introducing fixed-point here would add a
   rounding regime to a calculation that does not need one.

### Load-time validation (ADR-0007)

For every tower def, for every level `1..MaxLevel`:

```
RepairCostFor(level, Hp) < SellValueAt(level)
```

throwing on violation with a message naming the tower id, the level, both values, and `repairPercent`.
A message that says only "invalid repair cost" is worse than no check — the whole point is that the two
numbers live apart.

This is acceptance criterion 6, enforced rather than tested.

## Control Flow — `CommandSystem.Repair`

Order matters: **every rejection path must leave state byte-identical**, the same discipline `Build`
uses for its block check.

```
slot = state.SlotOfTower(towerId)
if slot < 0                    → silent return          (matches Sell; a dead tower is not an error)
if state.WaveActive            → RepairRejected(WaveInProgress)
def  = content.Tower(state.TowerDefIndex[slot])
missing = def.Hp - state.TowerHp[slot]
if missing <= 0                → RepairRejected(NotDamaged)
cost = def.RepairCostFor(state.TowerLevel[slot], missing)
if state.Gold < cost           → RepairRejected(InsufficientGold)

state.Gold -= cost
state.TowerHp[slot] = def.Hp
events: TowerRepaired(towerId, missing), GoldChanged(state.Gold, -cost)
```

Notes:

- **`WaveActive` is checked after the tower lookup**, so repairing a tower that no longer exists stays
  silent (matching `Sell`) rather than reporting the wave rule at a tower that is not there.
- **The rate limit costs no state.** `WaveActive` is already hashed and snapshotted, so the mechanic
  that makes this slice work adds nothing to `SimState`. That is luck, and it is worth noticing: a
  per-tower repair cooldown would have been the conventional answer and would have cost a new array,
  a new hash field, and a new snapshot field.

- **`missing <= 0`, not `== 0`.** Nothing should push HP above max today, but a guard that only catches
  the exact case would turn a future overshoot into free gold.
- **Repair is always to full.** Partial repair is a second knob (how much?) for no extra decision — the
  player's choice is *whether and when*, and cost already scales with damage, so repairing at 50% twice
  costs the same as repairing at 0% once (modulo the ceiling, which rounds against the clicker).
- **Level is read, never written.** Criterion 13. Repair restores health and only health.
- **`TowerCooldown` is untouched.** A repaired tower does not get a free shot.

## Determinism Checklist

| Check | Result |
|---|---|
| Float accumulation across ticks | None. The whole path is integer. |
| Dictionary / hash-set iteration order | None. `SlotOfTower` is an array lookup. |
| Wall clock or unseeded `Random` | None. `SimRandom` is not touched. |
| Godot types below the boundary | None. `Commands.cs` and `Defs.cs` are `net8.0`. |
| LINQ over unordered collections | None. `SellValueAt`'s loop is `for` over an array, and `RepairCostFor` reuses that shape. |
| New state to hash | **None** — the reason this slice is cheap. |
| Iteration by id, not slot | N/A. Repair addresses a single tower by id. |
| Int overflow | **The one real risk.** `long` intermediate, specified above. |

## Presentation Surface

`tower-upgrades` shipped with no player input binding — upgrade is reachable only from the screenshot
seed, and its design spec deferred the affordance because no build UI existed. Repair cannot do that:
criterion 12 requires the affordance be discoverable on the board.

| Piece | Change |
|---|---|
| `GameplayScene._UnhandledInput` | Middle click on a tower's cell → `RepairCommand`. Left and right are taken by build and sell. |
| `Hud` | On hover over a damaged tower, show `repair N (mmb)`. This is the discoverability claim. |
| `Hud._help` | Add `middle click: repair` to the help line. |
| `Hud.ShowRefusal` | A case for `NotDamaged` → "Not damaged." |
| `GameplayScene._Process` | Feed `RepairRejected` into `ShowRefusal`, which today only listens to `BuildRejected`. |

The HUD computes the cost by calling `TowerDef.RepairCostFor` — reading content and read-only state,
which is what the view is allowed to do. It does not reimplement the arithmetic; a second copy of the
cost formula in the view is a divergence waiting to happen.

## Acceptance Criteria the Verify Stage Will Run

1–15 from the design spec, plus:

16. **Trace diff.** Core changed, so the determinism harness runs. Repair is inert unless a
    `RepairCommand` is enqueued and no existing trace enqueues one, so **the recorded traces must be
    unchanged** — a hash shift with no repair in the input means something moved that should not have.
    This is a stronger signal than a re-record and must be checked before any re-record is considered.
17. Loading a tower def whose repair curve violates the wall throws, and the message names the tower and
    the level.
18. `RepairCostFor` does not overflow at the maximum plausible inputs.

## Risks

| Risk | Mitigation |
|---|---|
| Repair silently reverts `tower-combat` | Criterion 11 measures it in the balance sim. **This is the one that can fail** — and in revision 1 it did. |
| Int overflow in the cost function | `long` intermediate; criterion 18 tests it directly |
| Cost formula duplicated in the HUD | HUD calls `RepairCostFor`; no second copy |
| Trace churn from an inert feature | Criterion 16 asserts traces are unchanged rather than re-recording by reflex |
