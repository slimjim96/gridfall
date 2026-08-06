# Router

<!-- LAYER 2: read once per task. Classify, then get out of the way. -->

## Pick your task

| The task sounds like... | Go to | Workflow |
|---|---|---|
| "Should we build X?", "what does X need to do?", raw idea, acceptance criteria | `game-design/CONTEXT.md` | `workflows/pipeline/01-requirements-analysis.md` |
| "How should X play?", mechanics, tuning knobs, player-facing behavior | `game-design/CONTEXT.md` | `workflows/pipeline/02-game-design.md` |
| "How do we build X?", tick order, pathfinding, targeting, data structures, determinism | `engine-systems/CONTEXT.md` | `workflows/pipeline/03-engine-systems-design.md` |
| A technical choice with a real alternative | `engine-systems/CONTEXT.md` | `workflows/cross-cutting/architecture-decision-record.md` |
| Numbers: cost, HP, DPS, wave composition, map shape | `content-data/CONTEXT.md` | `workflows/cross-cutting/content-balance-pass.md` |
| Anything the player sees, clicks, or hears | `presentation/CONTEXT.md` | `workflows/cross-cutting/iso-presentation-pass.md` |
| Write the code for a slice that already has an architecture note | `production/CONTEXT.md` | `workflows/pipeline/04-implementation.md` |
| Test it, run determinism, decide if it passed | `production/CONTEXT.md` | `workflows/pipeline/05-verification.md` |
| Ship it, stamp a version | `production/CONTEXT.md` | `workflows/pipeline/06-release-slice.md` |
| "Where is X / what state is X in?" | Don't route. Look at the folder — the filename is the answer. | — |

## Skip rules that apply everywhere

- Never load a workspace you were not routed to. Cross-workspace knowledge arrives as a **handoff file**,
  not as context you go fetch.
- Never load `docs/` wholesale. Load the one reference the Scope's Load column names.
- Never load `production/01-requirements/*` while implementing. The architecture note supersedes it.
- Never load the worked example in `_examples/` unless you are learning the layout on purpose.

## If nothing matches

Ask the user **one** clarifying question that would change which row above applies. Do not guess, and do
not start work in `production` just because it is the default workspace.
