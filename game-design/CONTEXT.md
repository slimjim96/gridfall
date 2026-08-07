# Game Design

## What This Area Is

The authority on **what Gridfall is and why**. Raw ideas enter here and leave as requirements with
acceptance criteria, or as a design spec naming the mechanics and the tuning knobs.
Upstream: a human with an idea. Downstream: `engine-systems`, `content-data`, `presentation`, `production`.

This area names knobs; it does not set their values. `frost-spire slows creeps` is design.
`frost-spire slows creeps by 35%` is `content-data`.

## What to Load

| Task | Load These | Skip These |
|------|-----------|------------|
| Requirements analysis | `docs/pillars.md`, `../docs/glossary.md`, the raw request | all of `../engine-systems/`, all of `../content-data/`, `../docs/tech-standards.md` |
| Feature design spec | the requirements file, `docs/pillars.md`, `features/*` for adjacent features | `../docs/iso-grid.md`, `../docs/tech-standards.md`, wave/tower JSON |
| Reviewing a pillar conflict | `docs/pillars.md` only | everything else — this is a judgment call, not a research task |
| Answering "is this in scope?" | `docs/pillars.md`, the requirements file | implementation of any kind |
| Placing a feature in the current direction | `docs/board-themes-direction.md`, `docs/pillars.md` | the reports — direction is intent, not measurement |

## The Process

1. Restate the request in one sentence. If you cannot, run gap detection and ask one question.
2. Check it against the pillars. A feature that fights a pillar is rejected or the pillar changes —
   name which, explicitly.
3. Write the requirements: player-facing goal, the TD checklist (below), constraints, acceptance criteria.
4. Acceptance criteria must be **checkable by the verification engineer without you in the room**.
5. Hand off. Design does not proceed to architecture — `engine-systems` does that.

**The TD checklist** — every feature answers all five, or says "n/a" and why:
player fantasy · effect on pathing · effect on the economy · effect on wave pressure · the failure state.

## Skills & Tools

| Skill / Tool | When (trigger) | Purpose |
|--------------|----------------|---------|
| `handoff` (MCP) | Requirements or design spec is `done` | Move it downstream with a note; `requireNote` is on |
| Gap detection (Inject 4) | Any acceptance criterion you cannot phrase as pass/fail | Ask one question instead of inventing the answer |

## What NOT to Do

- Don't specify numbers. Name the knob, state the intent, let `content-data` tune it.
- Don't design the implementation. "Creeps re-path when the maze changes" is yours; flow fields are not.
- Don't write acceptance criteria that require a human to "feel" something — those belong in the
  presentation feel note, not in a verifiable criterion.
