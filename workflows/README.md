# Workflows

A **Scope** (`*/CONTEXT.md`) tells an agent *where it is and what to load*. A **workflow** tells it
*what to actually do, in order*. One workflow per file. An agent runs exactly one at a time.

## Pick one

| Workflow | Fires when | Workspace → Stage |
|---|---|---|
| [01 · Requirements Analysis](pipeline/01-requirements-analysis.md) | A raw idea or request arrives | `game-design` → `01-requirements` |
| [02 · Game Design](pipeline/02-game-design.md) | Requirements are `done` and need mechanics | `game-design` → `02-design` |
| [03 · Engine Systems Design](pipeline/03-engine-systems-design.md) | A design spec needs an implementable architecture | `engine-systems` → `03-architecture` |
| [04 · Implementation](pipeline/04-implementation.md) | An architecture note is handed to production | `production` → `04-build` |
| [05 · Verification](pipeline/05-verification.md) | The build is green and needs judging | `production` → `05-verify` |
| [06 · Release Slice](pipeline/06-release-slice.md) | Every criterion passed | `production` → `06-release` |
| [Content & Balance Pass](cross-cutting/content-balance-pass.md) | Any number changes | `content-data` |
| [Iso Presentation Pass](cross-cutting/iso-presentation-pass.md) | Anything the player sees changes | `presentation` |
| [Architecture Decision Record](cross-cutting/architecture-decision-record.md) | A technical choice has a real alternative | `engine-systems` |

Pipeline workflows 01→06 are the spine: one slice, one slug, six stages. Cross-cutting workflows can
run at any time and feed the spine.

## The shape every workflow shares

```
Fires when      the trigger — if this isn't true, you're in the wrong workflow
Load / Skip     the exact context. The Skip list is not advice; it is a constraint.
Steps           numbered. Each marked [deterministic] or [model call: which injection point]
Output          the one file this produces, at its exact path
Done when       a checkable list. Not "when it feels finished."
Handoff         who gets it and what the note must say
Failure modes   the specific ways this workflow goes wrong here
```

## The four model calls

Everything else is templating, file moves, and naming — do those without thinking about them.
A step marked `[model call]` is one of exactly four kinds:

1. **Route classification** — which area does this belong to? (`CONTEXT.md` handles this; rarely needed)
2. **Branch resolution** — given the state, what happens next? (Stage 05's pass/fail/loop-back)
3. **Leaf generation** — write the actual artifact
4. **Gap detection** — the inputs don't determine the output; ask **one** question and stop

If a step you want to take is not one of those four, a template or a file move should be doing it.

## Running a slice

```
session_start                      → orientation
CONTEXT.md                         → route to a workspace
<workspace>/CONTEXT.md             → load/skip discipline
workflows/<the one workflow>       → do the work
advance_stage                      → move the slice forward (it is a move, not a copy)
handoff                            → cross a workspace boundary, with a note
```

A full worked pass — one slice through all six stages, including a failed criterion and its loop-back
— is in [`_examples/path-recompute/`](../_examples/path-recompute/README.md).
