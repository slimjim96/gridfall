# Engine Systems

## What This Area Is

The architecture of the **deterministic simulation core** and the boundary between it and Godot.
A design spec comes in; an architecture note goes out, saying which systems change, in what tick order,
with what data, and what could break determinism.
Upstream: `game-design`. Downstream: `production` (04-build implements this note, not the spec).

`Gridfall.Core` is a plain `net8.0` class library. If a proposed design requires a Godot type below the
boundary, the design is wrong — not the boundary.

## What to Load

| Task | Load These | Skip These |
|------|-----------|------------|
| Architecture note | the design spec, `../docs/tech-standards.md`, `decisions/` index, the adjacent system's note | the requirements file, `../presentation/**`, `../content-data/waves/**` |
| Write an ADR | the competing options, `decisions/ADR-*` for precedent | the design spec's player-facing prose |
| Determinism review | `../docs/tech-standards.md` §Determinism, the diff under review | everything else — this is a narrow check |
| Performance work | the profile output, the system's own note | design docs, art direction |

## The Process

1. Read the design spec. Identify **which existing systems change** and which are new.
2. Place every new piece of work in the tick order (see `../docs/tech-standards.md` §Tick order).
   Ambiguous tick placement is the single most common source of non-determinism here.
3. State the data: what is stored, in what structure, owned by whom, mutated when.
4. Run the **determinism checklist**: no float accumulation across ticks · no dictionary/hash-set
   iteration order dependence · no wall-clock or `Random` without the seeded `SimRandom` · no
   Godot types · no LINQ over unordered collections in state-affecting paths.
5. List the acceptance criteria the verify stage will run, including a trace-diff criterion if the
   core changed.
6. Any choice with a real alternative gets an ADR. Link it from the note; don't inline the argument.

## Skills & Tools

| Skill / Tool | When (trigger) | Purpose |
|--------------|----------------|---------|
| `dotnet build` | Before handing a note to production if it includes signatures | Prove the sketched API compiles |
| `Gridfall.Verify` | When the note changes anything in the tick loop | Establish the baseline trace before the change |
| ADR workflow | Any time you write "we could also…" | `../workflows/cross-cutting/architecture-decision-record.md` |

## What NOT to Do

- Don't write production code here. This area emits notes and ADRs; `production/04-build` writes code.
- Don't let a "small" convenience pull `Godot` into Core. That is how determinism dies.
- Don't skip the ADR because the choice feels obvious today — write the two-line version instead.
