# WF-04 · Implementation

**Workspace:** `production` · **Stage:** `production/04-build` · **Role:** gameplay-engineer
**Injection points:** leaf generation (3), gap detection (4)

## Fires when

An architecture note is handed to production and the build folder does not exist yet.

## Load

- `production/03-architecture/[slug]-architecture.md` — **the source of truth**
- `docs/tech-standards.md`
- `docs/conventions.md`
- The existing code of the systems named in "Systems Touched"

## Never load

`production/01-requirements/*` — the architecture note supersedes it, and loading both invites you to
reconcile them, which is drift.
Also skip: `presentation/docs/art-direction.md` · `content-data/**` unless the note says you are
writing the loader.

## Steps

1. **[deterministic]** `advance_stage` the slice to `04-build`. It becomes a folder: `04-build/[slug]/`.
2. **[deterministic]** Create `04-build/[slug]/build-notes.md` **first**, before any code. You will
   write into it as you go; a notes file created at the end is a reconstruction.
3. **[deterministic]** Implement in tick-phase order. A system in phase 4 gets written before one in
   phase 6 — it makes the intermediate builds runnable.
4. **[deterministic]** Core code obeys tech-standards without exception: `Fix32` only, `SimRandom` only,
   stable iteration order, no Godot types. When something *looks* nondeterministic but isn't, leave the
   one-line comment saying why.
5. **[deterministic]** Update the state hash if the note said state was added. Do this in the same
   commit as the state, never later.
6. **[model call: leaf generation]** Write the code.
7. **[deterministic]** Record every non-obvious decision in `build-notes.md` **as you make it**:
   what you chose, what you rejected, and the one line of reasoning. If the decision has a real
   alternative and outlives this slice, it wants an ADR — say so and link it.
8. **[model call: gap detection]** If the note is silent on something that changes behavior, **ask one
   question**. Do not "implement the reasonable interpretation" — that reinterpretation becomes the
   spec by default, and nobody reviewed it.
9. **[deterministic]** If the note turns out to be wrong — not unclear, *wrong* — stop. Do not fix it
   in code. Loop the slice back to 03 with the reason. Silently correcting architecture in the build is
   how the note stops describing the game.
10. **[deterministic]** `dotnet build` must be **0 warnings, 0 errors**. `dotnet test` must be green.
11. **[deterministic]** Record a fresh determinism trace if Core changed: it becomes stage 05's input.

## Output

`production/04-build/[slug]/` — the code, plus:

```markdown
# [Feature] — Build Notes
**Slug:** `[slug]` · **Status:** review

## What Was Built
| File | New / Changed | Tick phase |
## Decisions Made While Building
| Decision | Rejected alternative | Why | ADR? |
## Deviations From the Architecture Note
## Determinism
- State hash updated: yes / no / n/a
- Trace recorded: <path>
## Build Status
`dotnet build` … `dotnet test` …
```

## Done when

- [ ] `dotnet build` is 0/0 and `dotnet test` is green
- [ ] Every file in "What Was Built" maps to a system in the architecture note
- [ ] Deviations are listed — an empty list is a real answer, an unwritten one is not
- [ ] The state hash covers new state
- [ ] `build-notes.md` was written during the work, not after

## Handoff

`advance_stage` to `05-verify`. Same workspace, so no handoff note — but the build notes must be
complete, because the verification engineer will not have you available.

## Failure modes

- **Reading the requirements "for context".** That is the one file in the skip list, and this is the
  stage where ignoring it matters most.
- **Fixing the architecture in the build.** Loop back instead. It costs an hour now and a week later.
- **Retrospective build notes.** They record what you remember, not what you decided.
- **A `float` in Core** because "it's just for the visual offset". Nothing in Core is just for visuals.
- **Deferring the hash update.** The harness will pass, and it will be lying.
