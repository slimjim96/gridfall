# Tooling

## What This Area Is

Developer tools: the in-Godot **board editor** and the headless CLIs (`Gridfall.Verify` — the
determinism harness and the balance sim runner). Things that make the team faster, not things the
player sees.
Upstream: whatever is slow. Downstream: `content-data` (maps the editor writes) and `production`.

The standing rule here: **a tool that disagrees with the game is worse than no tool.** Every tool reuses
the game's own code — the same renderer, the same loader, the same validator — rather than
reimplementing it.

## What to Load

| Task | Load These | Skip These |
|------|-----------|------------|
| Board editor work | `docs/board-editor-spec.md`, `../docs/iso-grid.md`, `../docs/engine-guide/07-content-loading.md` | `../game-design/**`, `../presentation/prompts/**`, `../content-data/waves/**` |
| Harness / CLI work | `../docs/engine-guide/08-determinism-playbook.md`, `../docs/engine-guide/04-state-and-entities.md` | `../presentation/**`, art direction, balance targets |
| Map format questions | `../docs/engine-guide/07-content-loading.md` only | everything else — the format is defined in one place |
| Adding a tool | `docs/board-editor-spec.md` as the shape to copy | the rest of `docs/` |

## The Process

1. Name what is slow. A tool with no measured friction behind it is a side project.
2. Check the spec before extending. The board editor's v1 scope is deliberately closed — geometry,
   playtest, and live validation. Wave editing is out **by decision**, not by omission.
3. Reuse, never reimplement. Picking uses `IsoGrid`. Saving and validating use `ContentLoader`.
   The route overlay uses `PathSystem`. Playtest uses the real `Sim`. If you are writing a second
   version of something the game has, stop.
4. Surface verdicts, never form them. The editor shows the game's errors earlier and the balance
   targets alongside them. It has no opinion of its own about what a legal map is.
5. Keep dev-only code in `godot/Dev/`, excluded from release exports. Verify the exclusion; don't
   assume it.
6. Tools get the same build gate as the game: `dotnet build` 0/0. They do not get the same determinism
   gate — they are not in Core — but anything they *write* is validated by the game's own rules.

## Skills & Tools

| Skill / Tool | When (trigger) | Purpose |
|--------------|----------------|---------|
| `godot-mono --editor` | Board editor work | The only way to exercise it; agents cannot see the result |
| `dotnet build` | Every change | Compile gate |
| Release export check | Any change under `godot/Dev/` | Prove dev tools are absent from a shipped build |
| Human sign-off | Any UI change | An editor's usability is not agent-verifiable |

## What NOT to Do

- Don't invent a second map format, a second validator, or a second picker. One of each, owned by the
  game. If the editor and the game disagree, the editor is wrong.
- Don't let a warning block a save. Only validator errors do — a tool that refuses to let you build the
  strange thing is a tool you stop using.
- Don't grow the editor past its spec without a new spec. "While I was in there" is how a small tool
  becomes a product nobody asked for.
- Don't let dev code reach a release build. Check the export, every time.
- Don't claim a UI works. You cannot see it — hand it to the human and say what to try.
