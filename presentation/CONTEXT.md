# Presentation

## What This Area Is

Everything the player sees, clicks, and hears: the isometric projection, the camera, tile and unit
rendering, depth sorting, the HUD, input picking, and game feel — plus the **asset pipeline**, from
cheap placeholders to the Ludo.ai prompts that replace them. This layer **reads** simulation state and
never mutates it — a click becomes a queued sim command, not a direct state change.
Upstream: `game-design`, and `docs/iso-grid.md` as the standing contract. Downstream: `production`.

All art is currently a **placeholder**: procedural C#, minimal detail, built to make the game playable
today. Finals come from Ludo.ai, run by the human. Both live behind `IUnitView`
([ADR-0004](../engine-systems/decisions/ADR-0004-view-asset-abstraction.md)).

## What to Load

| Task | Load These | Skip These |
|------|-----------|------------|
| Render / camera work | `../docs/iso-grid.md`, `docs/art-direction.md`, the render spec | `../engine-systems/decisions/**`, `../content-data/**`, `prompts/**` |
| HUD or input work | the UI spec, `../docs/iso-grid.md` §Picking | art direction, sim internals |
| Game feel pass | `docs/art-direction.md`, the sim event list it hooks | balance data, architecture notes |
| Build a placeholder | `docs/placeholder-standard.md`, `docs/art-direction.md` §Palette | `prompts/**`, `../docs/engine-guide/**` |
| Add or change terrain tiles | `tiles/README.md` alone — it is the whole contract | `../engine-systems/**`, `../content-data/**`, sim internals |
| Write asset prompts | `docs/ludo-prompt-guide.md`, `prompts/README.md`, the two nearest prompt files | the full prompt catalogue, `../engine-systems/**` |
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
| `./run-game.sh --shot <png> [--shot-seed <name>] --shot-after 40` | Every visual change | **Captures a real frame.** Byte-reproducible; diff against the seed's baseline in `docs/`. Use the launcher, never `godot-mono` directly — it builds the C# first, and Godot otherwise renders the assembly already in `.godot/mono` |
| A new `--shot-seed` in `GameplayScene` | A slice making a *new* visual claim | One seed per claim, so verifying a new cue never perturbs a committed baseline. Seeds: `upgrades`, `sappers` |
| `md5sum` on two captures | Before saying "no visual change" | Your eye is wrong about downscaled frames more often than you think |
| Ludo.ai | Human-operated, after a prompt set is written | Generates the final asset; you write the prompt, they run it |
| Human sign-off | Before any presentation slice reaches `06-release` | Agents cannot judge how it looks |

## What NOT to Do

- Don't mutate simulation state from the view layer. Ever. Queue a command.
- Don't claim a visual result you did not capture. Frames *can* be captured now, so "compiles; not
  visually verified" is no longer good enough for anything a still frame would show. Motion, feel, and
  taste still go to the human.
- Don't hardcode a projection constant that already lives in `../docs/iso-grid.md`.
- Don't polish a placeholder. It has an hour budget and a silhouette requirement; everything past that
  is work on something scheduled for deletion.
- Don't write prompts before the placeholder exists — its silhouette is the spec the generated art gets
  checked against.
