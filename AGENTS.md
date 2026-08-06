# AGENTS.md

Gridfall — isometric tower defense, Godot 4 + C#.

**Read [`CLAUDE.md`](CLAUDE.md) first.** It is the map and it applies to every agent, not just Claude
Code. Then `CONTEXT.md` to route your task, then the one workspace `CONTEXT.md` you were routed to,
then the one workflow in `workflows/`.

Do not read this file for anything else — everything is in the three layers, and the point of the
layers is that you only load what your task needs.

## The short version

- Route with `CONTEXT.md`. Don't start in `production` just because it is the default workspace.
- Obey the Skip column. It is a constraint, not advice.
- `Gridfall.Core` is a plain `net8.0` library: no Godot, no floats, no `Random`, no clock. See
  `docs/tech-standards.md`.
- Filenames are the state. One slug follows a slice from requirements to release.
- If you could not verify something, say you could not verify it.
