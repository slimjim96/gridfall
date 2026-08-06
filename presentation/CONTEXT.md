# Presentation

## What This Area Is

Everything the player sees, clicks, and hears: the isometric projection, the camera, tile and unit
rendering, depth sorting, the HUD, input picking, and game feel. This layer **reads** simulation state
and never mutates it — a click becomes a queued sim command, not a direct state change.
Upstream: `game-design`, and `docs/iso-grid.md` as the standing contract. Downstream: `production`.

## What to Load

| Task | Load These | Skip These |
|------|-----------|------------|
| Render / camera work | `../docs/iso-grid.md`, `docs/art-direction.md`, the render spec | `../engine-systems/decisions/**`, `../content-data/**` |
| HUD or input work | the UI spec, `../docs/iso-grid.md` §Picking | art direction, sim internals |
| Game feel pass | `docs/art-direction.md`, the sim event list it hooks | balance data, architecture notes |
| Readability check | `../docs/iso-grid.md`, the wave table's peak density | everything else |

## The Process

1. Start from the projection contract in `../docs/iso-grid.md`. If your work needs to change it,
   change the doc first and say so — every other layer depends on it.
2. Drive visuals off the sim's **event stream**, not off polling state diffs. Events are deterministic;
   your reaction to them does not have to be.
3. Keep the depth-sort key derived from grid coordinates, never from world Y alone.
4. Compile-check with `dotnet build`. Then say plainly what you could not see.
5. Hand visual sign-off to the human. Attach a short "what to look at" list to the handoff.

## Skills & Tools

| Skill / Tool | When (trigger) | Purpose |
|--------------|----------------|---------|
| `dotnet build` | Every change | Validates Godot API usage; the only automated check available here |
| `godot --headless --quit` | After scene-structure changes | Catches broken scene/resource wiring without a display |
| Human sign-off | Before any presentation slice reaches `06-release` | Agents cannot judge how it looks |

## What NOT to Do

- Don't mutate simulation state from the view layer. Ever. Queue a command.
- Don't claim a visual result you did not see. "Compiles; not visually verified" is the honest line, and
  it belongs in the verify report.
- Don't hardcode a projection constant that already lives in `../docs/iso-grid.md`.
- Don't add art assets as binary files without noting the source in `docs/art-direction.md`.
