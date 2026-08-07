# Content & Data

## What This Area Is

Every **number** in Gridfall, and the maps and waves those numbers act on. Towers, enemies, wave tables,
and map layouts are authored as plain JSON — never as code, never as Godot resources hand-edited in the
editor. A change here is a data change with a balance sim behind it.
Upstream: `game-design` (intent). Downstream: `production` (the slice that loads the data).

## What to Load

| Task | Load These | Skip These |
|------|-----------|------------|
| Tune an existing value | `docs/balance-targets.md`, the one file being changed, the last balance report | other tower/enemy files, `../engine-systems/**`, `../presentation/**` |
| Author a new tower or enemy | `docs/balance-targets.md`, two nearest-neighbor definitions, the design spec | the full catalogue, wave tables |
| Build a wave table | `docs/balance-targets.md`, the map layout, the enemy defs it uses | tower defs, art direction |
| Build a map | `../docs/iso-grid.md`, `docs/balance-targets.md` | tower/enemy internals |

## The Process

1. Find the design intent for the knob. A number with no stated intent is a number nobody can defend —
   go get the intent first.
2. Change the smallest thing that could work. One knob per pass — **or a swept grid, if the curve needs
   two ends moved in opposite directions.** See the note under What NOT to Do.
3. Run the balance sim: `Gridfall.Verify --balance --map <map> --runs 200`.
4. Report the deltas against `docs/balance-targets.md`: leak rate, gold curve, time-to-clear, and the
   share of runs that fail. A pass that moves a number without reporting these is not finished.
5. If a map changed, re-check it is **solvable and never fully blockable** — see the pathing rule in
   `../docs/tech-standards.md`.
6. Hand off to `production` with the report attached.

## Skills & Tools

| Skill / Tool | When (trigger) | Purpose |
|--------------|----------------|---------|
| `Gridfall.Verify --balance` | Every data change, without exception | Headless N-run sim; emits the deltas above |
| Schema check | Before handoff | Data must validate against the def schema or the loader will fail at runtime |
| `handoff` (MCP) | Data is `review` and the sim is green | Push to production with the report as the note |

## What NOT to Do

- Don't tune by feel and file it as balanced. The sim is cheap; run it.
- Don't bake behavior into data. If a tower needs a new *rule*, that is `engine-systems`, not a new field.
- Don't change two knobs in one pass **without sweeping them as a grid** — you lose the ability to
  attribute the result. The rule is about attribution, not arity: a 3x4 sweep moves two knobs and keeps
  attribution, and `early-economy-2` needed exactly that. Six passes failed to fix wave 3 because the
  difficulty curve needed its opening flattened *and* its ending steepened, which no single knob can do.
  Reading the rule as "never two knobs" is what kept the bug alive.
- Don't edit `.tres` files by hand; JSON is the source of truth and the resources are generated.
