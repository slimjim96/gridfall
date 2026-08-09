# WF-02 · Game Design

**Workspace:** `game-design` · **Stage:** `production/02-design` · **Role:** design-lead
**Injection points:** leaf generation (3), gap detection (4)

## Fires when

A requirements file is `done` and the *mechanics* are not yet decided — how the feature behaves, moment
to moment, and which knobs exist.

## Load

- `production/01-requirements/[slug]-requirements.md`
- `game-design/docs/pillars.md`
- `game-design/features/*` for features this one touches
- `docs/glossary.md` if you are introducing a term

## Never load

`docs/tech-standards.md` · `docs/iso-grid.md` · `engine-systems/decisions/**` ·
`content-data/**` (the values will anchor you) · any code

## Steps

1. **[deterministic]** Copy the acceptance criteria forward verbatim. They are the contract; this stage
   may *add* criteria but may not weaken one.
2. **[model call: leaf generation]** Write the **behavior spec**: what happens, in order, from the
   player's point of view. Cover the ordinary case first, then the edge cases the TD checklist implied.
3. **[deterministic]** For every state the feature can be in, say what the player sees. A state the
   player cannot perceive is a bug the player will call unfair.
4. **[model call: leaf generation]** Name the **tuning knobs**. For each: the knob's name, what raising
   it does to the player experience, and the direction you *expect* balance to push it. No values.
5. **[model call: leaf generation]** Write the **interaction rules** — what this feature does when it
   meets the features it touches. In a tower defense this is where the real design lives: stacking,
   priority, and what happens when two effects contradict.
6. **[deterministic]** State the **rejection cases**: inputs the game must refuse, and how it tells the
   player. Every build-time rule needs a refusal message, or players learn the rule by losing.
7. **[model call: gap detection]** If an interaction rule has two defensible answers and the pillars do
   not decide between them, **ask one question**. Do not pick the one that is easier to build.
8. **[deterministic]** Add the criteria this stage introduced to the acceptance list, numbered
   continuing from the requirements.

## Output

`production/02-design/[slug]-design.md`

```markdown
# [Feature] — Design
**Slug:** `[slug]` · **Status:** ready · **Supersedes for implementation:** the requirements file

## Behavior
## Player-Visible States
| State | What the player sees |
## Tuning Knobs
| Knob | Raising it… | Expected direction |
## Interaction Rules
## Rejection Cases
| Refused input | Message to the player |
## Acceptance Criteria (carried + added)
```

## Done when

- [ ] Every requirements criterion appears unchanged or strengthened
- [ ] Every knob is named with an intent and carries no value
- [ ] Every player-visible state has a visible representation
- [ ] Every rejection case has a message
- [ ] Interactions with adjacent features are stated, including "no interaction" where that is the answer

## Handoff

`handoff` to `engine-systems`. The note says which systems you believe are touched — as a hint, not an
instruction. Architecture decides.

## Failure modes

- **Designing the implementation.** "Recompute the flow field" is architecture. "Visitors re-route within
  a moment of the maze changing" is design.
- **Silent states.** A slow that the player cannot see is a slow the player will not believe.
- **Interaction rules deferred to "later".** Later is the bug report. Decide now, even if the decision
  is "these two never stack."
- **Weakening a criterion to make the design easier.** If a criterion is wrong, say it is wrong and
  loop back to 01 — do not soften it in passing.
