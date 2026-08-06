# Path Recompute — v1

**Slug:** `path-recompute` · **Status:** done · **Verified at trace:** `a7f3c9e1`
*Workflow: WF-06. Would live at `production/06-release/path-recompute-v1.md`.*

## What Shipped

Creeps re-route when the maze changes. A flow field, rebuilt by reverse BFS from the goal on any tick
where the grid changed, replaces per-creep pathing entirely. Builds that would seal a lane are refused
at command time, before the grid mutates, using the same BFS on a scratch grid — so the refusal and the
drag preview cannot disagree with each other.

Pathing cost is now independent of creep count: 300 creeps cost the same as one.

## Player-Facing Change

Placing a tower changes where the enemy walks. Mazing works — a good route costs the attacker real
time, and the game shows you the route you are building while you drag. Trying to seal a lane
completely is refused rather than punished; the game tells you, keeps your gold, and outlines the cell
that would have done it.

Creeps commit to the cell they are crossing and turn at the far side. Placing a tower directly in front
of a creep does not teleport it — it turns one cell later. This is a rule to play around, not a bug.

## New Tuning Knobs

| Knob | Owner | Default set? |
|---|---|---|
| `routePreviewOpacity` | content-data | No — shipped at the placeholder value |
| `maxMazeMultiplier` | content-data | No — currently unenforced; the balance targets cap it at 3× |
| `refusalFlashDuration` | content-data | No — and it is **too short**, per the human sign-off on criterion 9 |

Three knobs shipped untuned. They are backlog items in `content-data` as of this release, not memories.

## Follow-Ups Not Done

| Item | Workspace | Suggested slug |
|---|---|---|
| Tune the three knobs above; `refusalFlashDuration` first | content-data | `path-recompute-knobs` |
| Enforce `maxMazeMultiplier` at build time — currently advisory only | engine-systems | `maze-length-cap` |
| Second flow field for flying creeps, when flyers exist | engine-systems | `flyer-pathing` |
| Re-target wave balance: mazing meaningfully raises effective time-in-range, and the wave tables predate it | content-data | `post-maze-rebalance` |

The last one matters most. The requirements file predicted it under wave pressure, and it is now real:
every balance number in the game was tuned against a fixed path.

## ADRs Accepted

- ADR-0003 — Flow field pathfinding over per-unit A*

## Known Not Verified

Criteria 8 and 9 were never machine-verified — they are visual. Both were signed off by a human on
2026-08-06. Criterion 9's sign-off came with the note that the refusal flash is too brief to notice,
which is captured above as a knob and a follow-up.

Nothing else in this slice is unverified.
