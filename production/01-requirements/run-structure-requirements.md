# Run Structure — Requirements

**Slug:** `run-structure` · **Status:** backlog · **Owner:** design-lead

## In One Sentence

Decide what a *run* is — where it ends, how it ends, and whether boards form a sequence — because
right now you cannot lose, cannot win, and there is nowhere to go next.

## What actually exists, verified

Three findings, and the confusion behind this slice is reasonable because all three are invisible
from inside the game.

**1. `GameOver` fires and nobody listens.** `EconomySystem` emits `EventKind.GameOver` the tick lives
reach zero, and says so deliberately:

> *"The sim reports the loss; it does not stop itself. Whether the run ends is the caller's decision."*

**No caller decides.** `grep GameOver` across `godot/`, `Gridfall.Verify/` and `Gridfall.Tests/`
returns nothing. At zero lives the game keeps running indefinitely — creeps keep spawning, towers
keep firing, lives stay pinned at 0. The Core boundary is right; the view side of it was never built.

**2. There is no victory.** `CommandSystem` line 220: `if (state.WaveIndex >= content.Waves.Length)
return;`. After wave 12, pressing space does nothing, silently. No event, no state, no acknowledgement
that you just survived the thing.

**3. There is no progression.** `GameplayScene.MapId` is `private const string MapId = "crossroads"`.
Boards are not a sequence, a selection, or an unlock — the game is one hardcoded map.

So "enemies can get to the end and the player can still move to the next board" is half right: they
can indeed reach the end with nothing happening, but there is no next board to move to either.

## The data that should drive the decision

30-run balance sim, beginner policy, read as a floor on difficulty:

| Map | Runs lost | Lives left | Where it is decided |
|---|---|---|---|
| `crossroads` | **23.3%** | avg 8.7, sd 7.0, **range 0–20** | wave 11.9 avg (earliest 11, latest 12) |
| `gauntlet` | **0%** | 20, **sd 0.0, range 20–20** | never |

**The two shipped maps are not on the same game.** One is decided in its final two waves with a
full-width spread of outcomes; the other cannot be lost at all. No game-over threshold reconciles
that — a threshold change cannot make `gauntlet` threatening or `crossroads` gentle.

That matters for the question asked: **the "happy medium between enemy iterations and board
promotions" is not currently a tuning problem, it is a structure problem.** There is no unit of
progression to tune.

## The decision, and it is yours

Three readings, and they lead to materially different work.

### A. A run is one board (recommended)

12 waves, lives reset, boards are *selectable* rather than sequential. Win by clearing wave 12; lose
at 0 lives.

Why this first: it is what the content already **is** — every map carries its own wave table and its
own `startingLives`, which is the shape of a self-contained run. It needs no re-tuning, and it makes
both missing states (win, lose) buildable this week.

### B. A run is a sequence of boards

Lives and possibly gold carry across; waves keep scaling. A campaign.

The cost is real and it is a balance cost, not a code cost: with `crossroads` at 23.3% loss and
`gauntlet` at 0%, a sequence is either trivial or brutal depending purely on order. Sequencing needs
the maps to sit on one difficulty scale first, and they do not.

### C. A run is one board, but clearing it unlocks the next

The middle. Cheap to build on top of A, and worth treating as A's follow-up rather than as a rival.

**Recommendation: A now, C later, B only if the maps are ever put on one scale.**

## On the game-over threshold specifically

The threshold is very likely not the interesting knob. **0 lives = loss is legible**, and pillar 4
(every loss is explainable) is better served by a hard, visible zero than by a fractional rule.

The real gap is that zero currently *does nothing*. Build the reaction before tuning the number.

One thing worth checking when it exists: lost runs die at **wave 11.9 of 12**. A loss that lands on
the last wave, after 20 minutes, is the most expensive kind of loss to get wrong — it needs to be
visibly coming, not sudden.

## Scope Sketch

**In:**
- A view-side reaction to `GameOver` — the run stops, and says why
- A win state when the last wave is cleared, with its own event in Core
- Whichever of A/B/C is chosen, at minimum a way to reach a second board
- Lives displayed as *at risk* before they run out (pillar 4)

**Out:**
- Re-tuning either map. `crossroads` density and `gauntlet`'s zero-variance cliff are already
  tracked (`map-density-target`, `route-variance-metric`) and are not this slice.
- Meta-progression, unlocks, persistence between sessions.

## Open Questions

1. **A, B, or C.** Everything else follows.
2. **Does clearing wave 12 end the run, or continue endlessly?** An endless mode is a different game
   and would make the wave table a curve rather than a list.
3. **Does the player see a loss coming?** At 8.7 average lives with sd 7.0, the spread is wide enough
   that "I was at 3 lives and did not notice" is a real outcome.

## Downstream

| Workspace | What it needs |
|---|---|
| `engine-systems` | A `RunComplete`/victory event in Core, and whether run state belongs in `SimState` (it is hashed, so it is determinism-relevant) |
| `presentation` | The two end screens, and the at-risk lives cue |
| `content-data` | If B: the maps must be put on one difficulty scale before ordering means anything |
