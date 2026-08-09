# WF-01 · Requirements Analysis

**Workspace:** `game-design` · **Stage:** `production/01-requirements` · **Role:** design-lead
**Injection points:** leaf generation (3), gap detection (4)

## Fires when

Someone says "I want X" and there is no requirements file for it. If a requirements file already exists
and is being revised, this is still the right workflow — bump it rather than starting a second one.

## Load

- The raw request, verbatim
- `game-design/docs/pillars.md`
- `docs/glossary.md` — only if a term in the request is ambiguous
- The requirements file of the nearest adjacent feature, if one exists

## Never load

`engine-systems/**` · `content-data/**` · `presentation/**` · `docs/tech-standards.md` ·
`docs/iso-grid.md`

You are deciding *what* and *why*. Every one of those files answers *how*, and loading them makes the
requirements drift toward whatever is easy to build.

## Steps

1. **[deterministic]** Choose the slug. Lowercase kebab-case, and it will follow this slice to release.
   Check `find_by_slug` first — if it is taken, you are revising, not creating.
2. **[deterministic]** Restate the request in one sentence, in the player's terms, not the system's.
   "Visitors find a new route when the maze changes" — not "expose a repath API".
3. **[model call: gap detection]** If you cannot write that sentence, or it needs an assumption you
   cannot justify from the pillars, **stop and ask one question.** Do not proceed on a guess.
4. **[deterministic]** Check the pillars. If the feature fights one, say which, and either reject it or
   propose the pillar change explicitly. Never quietly do both.
5. **[model call: leaf generation]** Answer the **TD checklist**. Each gets a real answer or `n/a` with
   a reason:
   - **Player fantasy** — what does the player get to feel or do that they couldn't before?
   - **Pathing** — does this change the walkable grid, the route, or the cost of a route?
   - **Economy** — does it move gold in or out, or change what gold buys?
   - **Wave pressure** — does it make waves easier, harder, or differently shaped?
   - **Failure state** — how does the player lose *because of* this feature, or fail to use it?
6. **[model call: leaf generation]** Write the constraints: what must stay true. Determinism and the
   never-fully-blockable rule are constraints on almost everything — name them when they apply.
7. **[model call: leaf generation]** Write the acceptance criteria. Each one is a sentence a
   verification engineer can mark PASS or FAIL **without asking you anything**. Numbered, 3–8 of them.
8. **[deterministic]** Anything you could not resolve goes in `## Open Questions`, addressed to a named
   owner. An empty section is deleted, not left as a heading.

## Output

`production/01-requirements/[slug]-requirements.md`

```markdown
# [Feature] — Requirements
**Slug:** `[slug]` · **Status:** ready · **Owner:** design-lead

## In One Sentence
## Pillar Check
## TD Checklist
| Question | Answer |
## Constraints
## Acceptance Criteria
1. …
## Open Questions
```

## Done when

- [ ] The one-sentence statement is in player terms
- [ ] All five TD checklist rows answered or explicitly `n/a` with a reason
- [ ] Every acceptance criterion is pass/fail checkable by someone else
- [ ] No numbers appear anywhere in the file (knobs are named, not valued)
- [ ] No implementation is described

## Handoff

`handoff` to `game-design` stage 02 (same workspace, next workflow) or straight to `engine-systems` if
the mechanics are already settled. Note fields: `summary`, `nextOwner`, `openQuestions`.

## Failure modes

- **Numbers leak in.** "Slows by 35%" is content-data's job. Write "slows".
- **The criteria need you.** "The feature feels responsive" is not checkable. Rewrite or move it to a
  presentation feel note, which is judged by a human on purpose.
- **The TD checklist gets skipped as boilerplate.** The pathing and economy rows are exactly where a
  tower defense feature turns out to be three features.
- **Two features in one file.** If the checklist answers split cleanly in two, so should the slice.
