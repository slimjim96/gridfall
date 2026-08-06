# Path Recompute — Design

**Slug:** `path-recompute` · **Status:** done · **Supersedes for implementation:** the requirements file
*Workflow: WF-02. Would live at `production/02-design/path-recompute-design.md`.*

## Behavior

1. The player drags a tower over the board. While dragging, the board shows the route creeps *would*
   take if the tower were placed there.
2. On release, the game checks the placement. If it would seal a lane, the build is refused: the tower
   snaps back, the sealing cell flashes, and no gold is spent.
3. Otherwise the tower is placed, the route updates, and every creep on the board turns onto the new
   route at the next cell boundary it reaches.
4. Selling works the same way in reverse. Selling never needs a refusal — removing an obstacle cannot
   seal a lane.
5. Creeps do not react instantly. A creep entering a cell commits to crossing it; it turns at the far
   side. This is a rule the player learns and plays around, not a limitation to hide.

## Player-Visible States

| State | What the player sees |
|---|---|
| Dragging, placement legal | Ghost tower, and the projected new route drawn over the board |
| Dragging, placement would seal | Ghost tower in the refusal red; the sealing cell outlined |
| Build refused | Tower snaps back, sealing cell flashes once, refusal cue plays, no gold spent |
| Route changed | The route highlight redraws; creeps visibly turn at their next cell boundary |
| Creep committed to a cell | No visual — the creep simply finishes its crossing before turning |

## Tuning Knobs

| Knob | Raising it… | Expected direction |
|---|---|---|
| `routePreviewOpacity` | Makes the projected route more prominent while dragging | Tune for legibility at wave 18, not wave 3 |
| `maxMazeMultiplier` | Allows longer mazes relative to the unmazed path | Down. Long mazes break wave timing (see balance targets) |
| `refusalFlashDuration` | Holds the refusal feedback longer | Short — long enough to notice, short enough not to block a retry |

No values here. `content-data` sets them.

## Interaction Rules

- **Vs. flying creeps** (not yet built): flyers ignore the grid entirely and are unaffected by mazing.
  When flyers arrive, they do not participate in the block check either. Stated now so the pathing
  design does not have to change later.
- **Vs. slow effects:** a slowed creep re-routes on the same rule — at its next cell boundary, which
  simply arrives later. No special case.
- **Vs. multiple lanes:** the block check runs per lane. A build that seals lane B is refused even if
  lane A is wide open. Partial sealing is not a thing.
- **Vs. selling during a wave:** allowed. Selling is always legal and always re-routes.

## Rejection Cases

| Refused input | Message to the player |
|---|---|
| Build would seal a lane | "That would block the only route." Sealing cell outlined. |
| Build on a non-buildable cell | No message; the ghost never turns legal there. Silence is the message. |
| Build with insufficient gold | Cost shown in the refusal red on the HUD. |

## Acceptance Criteria (carried + added)

1–7 carried unchanged from the requirements.

8. While dragging over a legal cell, the projected route shown is the route creeps actually take if the
   build is confirmed.
9. While dragging over a sealing cell, the ghost shows the refusal state before release, not after.
10. Selling during an active wave re-routes creeps on the same next-cell-boundary rule.
