# WF-06 · Release Slice

**Workspace:** `production` · **Stage:** `production/06-release` · **Role:** gameplay-engineer
**Injection points:** leaf generation (3)

## Fires when

Stage 05 returned PASS and every NOT-VERIFIABLE-BY-AGENT item has been signed off by the human — or
explicitly waived by them, in writing, in the report.

## Load

- `production/05-verify/[slug]-report.md`
- `production/02-design/[slug]-design.md`

## Never load

`production/01-requirements/*` · `production/04-build/[slug]/**` internals · `docs/**`

The release note is written from what was verified and what was promised, not from the code.

## Steps

1. **[deterministic]** Confirm the gate: verdict PASS, zero open FAILs, human sign-off recorded for
   every NOT-VERIFIABLE-BY-AGENT row. If any is missing, this workflow does not run — say what is
   missing and stop.
2. **[deterministic]** `advance_stage` to `06-release`. The file becomes `[slug]-v1.md`.
3. **[model call: leaf generation]** Write the release note **for a player and for the next agent**,
   in that order. What changed, what it means at the table, what to watch.
4. **[deterministic]** List the knobs this slice introduced and hand them to `content-data` — a knob
   that ships untuned is a knob nobody knows exists.
5. **[deterministic]** List the follow-ups the slice deliberately did not do. Each becomes a backlog
   item in the workspace that owns it, with a slug of its own.
6. **[deterministic]** If an ADR was accepted during this slice, mark it accepted in
   `engine-systems/decisions/` and cite it here.
7. **[deterministic]** Record the trace hash the release was verified at. A future regression can then
   be bisected against a known-good number.

## Output

`production/06-release/[slug]-v1.md`

```markdown
# [Feature] — v1
**Slug:** `[slug]` · **Status:** done · **Verified at trace:** `<hash>`

## What Shipped
## Player-Facing Change
## New Tuning Knobs
| Knob | Owner | Default set? |
## Follow-Ups Not Done
| Item | Workspace | Suggested slug |
## ADRs Accepted
## Known Not Verified
```

## Done when

- [ ] Every knob has an owner and a "default set?" answer
- [ ] Every deliberate omission is a named follow-up, not a memory
- [ ] The trace hash is recorded
- [ ] "Known Not Verified" carries forward anything the human waived, verbatim
- [ ] The slice's six artifacts share one slug, so `find_by_slug` returns the whole story

## Handoff

None — this is the end of the pipeline. New knobs go to `content-data` as fresh backlog items; they are
not a reverse handoff.

## Failure modes

- **Releasing with an open FAIL** because it seemed minor. The pipeline's only real guarantee is that
  this never happens.
- **A release note written from the diff.** It reads like a changelog and tells nobody why.
- **Untracked knobs.** Six months later a number nobody chose is balancing your game.
- **Follow-ups as prose.** "We should probably also…" is not a work item. Give it a slug.
