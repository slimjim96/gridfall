# WF-X3 · Architecture Decision Record

**Workspace:** `engine-systems` · **Role:** systems-architect
**Injection points:** leaf generation (3)

## Fires when

You write, think, or say **"we could also…"** about a technical choice that will outlive the slice.
Concretely: a data structure that other systems will build on, anything affecting determinism, anything
crossing the Core/View boundary, or a choice you would have to re-explain in three months.

Not every choice needs one. The test: **would reversing this decision later be expensive?** If yes,
write the ADR — even if it is six lines.

## Load

- `engine-systems/decisions/` — the index and any ADR this one touches or supersedes
- `docs/tech-standards.md`
- The architecture note that raised the question

## Never load

The design spec's player-facing prose · `content-data/**` · `presentation/**`

An ADR argues about structure. Product reasoning belongs in the design spec, and mixing them produces
an ADR nobody can evaluate on its merits.

## Steps

1. **[deterministic]** Take the next number. `ls engine-systems/decisions/` — four digits, sequential,
   never reused, never renumbered.
2. **[deterministic]** State the decision as a **sentence in the imperative**, in the title. Not
   "Pathfinding" — `ADR-0003 — Flow Field Pathfinding Over Per-Unit A*`. A title with no verb is a topic,
   not a decision.
3. **[deterministic]** Write the **context**: what forces this choice, right now. Include the numbers
   that constrain it — 300 creeps, 8 ms, 30 Hz. Context without constraints reads as opinion.
4. **[model call: leaf generation]** Write the **options**, at least two, each stated well enough that a
   reader could pick the one you rejected. A strawman alternative makes the ADR worthless; the whole
   value is in a fair account of the road not taken.
5. **[deterministic]** State the **decision** and the deciding factor — the *one* thing that broke the
   tie. Not a list of advantages; the factor.
6. **[deterministic]** State the **consequences**, including the ones you dislike. What gets harder.
   What is now expensive to change. What this forecloses.
7. **[deterministic]** Set the status: `proposed` → `accepted` → (later) `superseded by ADR-NNNN`.
   An ADR is never deleted and never edited after acceptance — it is superseded.
8. **[deterministic]** Link it from the architecture note that raised it, and from any ADR it supersedes.

## Output

`engine-systems/decisions/ADR-[nnnn]-[slug].md`

```markdown
# ADR-[nnnn] — [Imperative decision sentence]
**Status:** proposed | accepted | superseded by ADR-nnnn
**Date:** YYYY-MM-DD · **Raised by:** [slice slug]

## Context
## Options
### A. …
### B. …
## Decision
Chose **X**. Deciding factor: …
## Consequences
### Good
### Bad
### Forecloses
```

## Done when

- [ ] The title is a sentence with a verb
- [ ] At least two options, both stated fairly
- [ ] Exactly one deciding factor named
- [ ] Bad consequences written down — an ADR with only good consequences is marketing
- [ ] Linked from the architecture note that raised it

## Failure modes

- **Strawman alternatives.** The rejected option should be one a competent engineer would have picked.
- **A list of pros instead of a deciding factor.** If you cannot name the one thing, you have not
  finished deciding.
- **Editing an accepted ADR.** Supersede it. The old reasoning is the record of what you knew then.
- **Writing it after implementing.** Then it documents what you did, not why — and the alternatives
  will be described the way losers get described.
- **An ADR for a reversible choice.** Not everything is a decision record. Reversible choices go in
  build notes.
