# WF-03 · Engine Systems Design

**Workspace:** `engine-systems` · **Stage:** `production/03-architecture` · **Role:** systems-architect
**Injection points:** leaf generation (3), gap detection (4)

## Fires when

A design spec is handed in and needs to become something implementable: which systems change, where in
the tick order, what data, and what could break determinism.

This is the stage that makes the difference between a tower defense that can be balanced and one that
cannot. **Everything below the Core/View boundary must be reproducible.**

## Load

- `production/02-design/[slug]-design.md`
- `docs/tech-standards.md` — §Determinism and §Tick order especially
- `engine-systems/decisions/` — the ADR index, and any ADR the design brushes against
- The architecture notes of the systems you are about to change
- `docs/iso-grid.md` **only if** the feature touches grid coordinates

## Never load

`production/01-requirements/*` (superseded) · `presentation/**` · `content-data/waves/**` ·
`content-data/towers/**` — values must not shape the structure

## Steps

1. **[deterministic]** List the systems the design touches. For each: new, changed, or unchanged-but-
   affected. The third category is the one that bites.
2. **[deterministic]** Place every new piece of work in the **nine-phase tick order**. Write the phase
   number next to each. Work you cannot place is work you do not understand yet.
3. **[model call: leaf generation]** Specify the **data**: what is stored, in what structure, who owns
   it, when it mutates. Arrays indexed by entity id, not object graphs. Give the memory shape for the
   worst case in the performance budget (300 creeps / 60 towers / 64×64).
4. **[model call: leaf generation]** Specify the **algorithm** for anything non-obvious, at the level
   of "a competent engineer could implement this without asking you". Include the complexity and when
   it runs — every tick, or only on a dirty grid?
5. **[deterministic]** Run the **determinism checklist** and write the result in the note, item by item:
   - No floats in Core — all `Fix32`?
   - No `Random`, `DateTime`, or wall-clock?
   - No iteration over `Dictionary`/`HashSet` in a state-affecting path?
   - Ties broken by a fixed rule (direction order, then entity id)?
   - No parallelism, or an order-independent merge proven in an ADR?
   - Does the state hash need new fields? **If state was added, the hash must cover it** — a hash that
     ignores new state makes the harness lie.
6. **[deterministic]** State the **boundary**: exactly what the view layer may read, and which
   `SimEvent`s this feature emits. Presentation builds against this list.
7. **[model call: gap detection]** If two structures are genuinely defensible and the design does not
   decide, **ask one question** — or, if the tradeoff is technical rather than a product call, write the
   ADR instead of asking. Prefer the ADR.
8. **[deterministic]** Write the **verify plan**: the specific checks stage 05 will run, including the
   trace-diff criterion if Core changed and the perf assertion if the tick loop changed.
9. **[deterministic]** Emit ADRs for every real choice. Link them; don't inline the argument.

## Output

`production/03-architecture/[slug]-architecture.md`

```markdown
# [Feature] — Architecture
**Slug:** `[slug]` · **Status:** ready · **Supersedes for implementation:** the design spec

## Systems Touched
| System | New / Changed / Affected | Tick phase |
## Data
## Algorithm
## Determinism Checklist
| Check | Result |
## Boundary — What the View Sees
| Read | SimEvent |
## Verify Plan
## ADRs
```

## Done when

- [ ] Every touched system has a tick phase number
- [ ] The determinism checklist is answered item by item, not summarized
- [ ] The state hash covers any state this feature adds
- [ ] The view boundary lists reads and events explicitly
- [ ] The verify plan names runnable checks, not intentions
- [ ] Every "we could also…" became an ADR

## Handoff

`handoff` to `production`. The note names the first file to create and the check that must pass first.

## Failure modes

- **Unplaced work.** "Recompute pathing" with no phase number will end up in the wrong place and the
  trace diff will fail a week later, in a slice that did not cause it.
- **A hash that ignores new state.** The harness goes green while determinism is already broken. This
  is the most expensive mistake available in this project.
- **Godot creeping into Core** for "just one convenience type."
- **Values in the note.** If you find yourself writing `35%`, you are doing content-data's job.
- **Skipping the ADR because it is obvious.** Write the two-line version. Obvious today is archaeology
  in three months.
