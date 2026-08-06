# WF-05 · Verification

**Workspace:** `production` · **Stage:** `production/05-verify` · **Role:** verification-engineer
**Injection points:** branch resolution (2), leaf generation (3)

## Fires when

A build is green and needs judging. This is the only stage that can send a slice backwards, and it is
the reason the pipeline is trustworthy.

## Load

- `production/02-design/[slug]-design.md` — the acceptance criteria
- `production/03-architecture/[slug]-architecture.md` — the verify plan
- `production/04-build/[slug]/**` — the code and the build notes

## Never load

`production/01-requirements/*` · `presentation/docs/art-direction.md` ·
`docs/` except `conventions.md`

You are checking the slice against what it promised, not against everything the project believes.

## Steps

1. **[deterministic]** Copy the acceptance criteria into the report as a numbered checklist. All of
   them — the ones from requirements and the ones design added.
2. **[deterministic]** Run the build gate: `dotnet build` (0/0) and `dotnet test`.
3. **[deterministic]** Run the **determinism harness** if Core changed:
   `dotnet run --project Gridfall.Verify`. It replays recorded traces and diffs per-tick state hashes.
   A mismatch is a FAIL regardless of what any other criterion says — determinism is not negotiable and
   cannot be waived.
4. **[deterministic]** Run the perf assertion if the tick loop changed: worst case must stay ≤ 8 ms.
5. **[deterministic]** Check each criterion and mark exactly one of:
   - **PASS** — with the evidence: command run, output, or test name. "It works" is not evidence.
   - **FAIL** — with what actually happened.
   - **NOT-VERIFIABLE-BY-AGENT** — with what a human needs to look at and why you cannot. Visual and
     game-feel criteria land here honestly; that is what the category is for.
6. **[deterministic]** Check the **structural invariants** on every slice, whether or not the criteria
   mention them:
   - `Gridfall.Core` references no `GodotSharp`
   - No `float`/`double`/`System.Random`/`DateTime` in Core
   - The never-fully-blockable rule still holds on every shipped map
   - The state hash covers all state the slice added
7. **[model call: branch resolution]** If anything FAILed, name **exactly one** stage to loop back to,
   and say why in one sentence:
   - **→ 02 design** — the criterion itself was wrong, or two criteria contradict
   - **→ 03 architecture** — the design was right, the structure cannot deliver it
   - **→ 04 build** — the structure was right, the code does not match it
   Pick one. "Some of both" means you have not finished diagnosing.
8. **[model call: leaf generation]** Write the report.
9. **[deterministic]** On any FAIL, `advance_stage` does not run. Move the work back to the named stage
   and leave the report in place — the next pass reads it.

## Output

`production/05-verify/[slug]-report.md`

```markdown
# [Feature] — Verification
**Slug:** `[slug]` · **Status:** review · **Verdict:** PASS / FAIL

## Gates
| Gate | Result | Evidence |
| dotnet build | | |
| dotnet test | | |
| determinism trace | | |
| perf ≤ 8ms/tick | | |

## Criteria
| # | Criterion | Result | Evidence |

## Structural Invariants
| Invariant | Result |

## Branch Resolution        ← only if FAIL
**Loop back to:** 0X — <one sentence>

## Not Verifiable By Agent
| # | What a human must check |
```

## Done when

- [ ] Every criterion has a result and evidence — no blanks, no "probably"
- [ ] All four structural invariants checked
- [ ] Determinism run if Core changed
- [ ] On FAIL: exactly one loop-back stage named with a reason
- [ ] On PASS: the human-check list is explicit, so release knows what it is waiting on

## Handoff

PASS → `advance_stage` to `06-release`. FAIL → move back to the named stage; the slice keeps its slug.

## Failure modes

- **Marking PASS from reading the code.** Run it, or mark it NOT-VERIFIABLE-BY-AGENT. Reading is not
  evidence, and this is the single most damaging shortcut in the pipeline.
- **Loop-back to "02 and 04".** Diagnose harder. One stage.
- **Skipping the determinism run** because the change "obviously" didn't touch the sim. The harness is
  cheap; your judgment about what touched the sim is not free.
- **Quietly widening a criterion** so it passes. If a criterion is wrong, that is a loop-back to 02.
- **Claiming a visual result.** Agents cannot see the game. Say so; that is a complete answer.
