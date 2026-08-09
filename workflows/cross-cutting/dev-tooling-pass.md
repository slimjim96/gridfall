# WF-X5 · Dev Tooling Pass

**Workspace:** `tooling` · **Role:** tools-engineer
**Injection points:** leaf generation (3), gap detection (4)

## Fires when

Something in the development loop is slow enough to be worth fixing: hand-editing map JSON, restarting
the game to see a change, reading a trace diff by eye. Also fires for any change to the board editor or
the `Gridfall.Verify` CLIs.

## Load

- `tooling/docs/board-editor-spec.md` — for editor work, and as the shape to copy for a new tool
- `docs/iso-grid.md` — anything that picks or draws on the grid
- `docs/engine-guide/07-content-loading.md` — anything that reads or writes content
- `docs/engine-guide/08-determinism-playbook.md` — anything that touches traces

## Never load

`game-design/**` · `presentation/prompts/**` · `content-data/waves/**` · `production/01-requirements/*`

Tools serve the developer, not the player. Design intent is not an input here.

## Steps

1. **[deterministic]** Name the friction, concretely and with a number. "Editing a map takes twenty
   minutes of hand-writing JSON and two typos" is a reason. "The editor could be nicer" is not.
2. **[deterministic]** Check the spec. The board editor's v1 scope is **closed**: map geometry,
   playtest, and live validation. Wave composition is out by decision. Extending scope needs a spec
   change first, and the spec change is a conversation with the human — **gap detection: ask.**
3. **[deterministic]** Find what the game already does, and reuse it. Before writing anything:
   - Picking? → `IsoGrid` ray-to-plane. Not a new implementation.
   - Reading or writing content? → `ContentLoader` and its validator.
   - Judging whether a map is legal? → `ContentLoader`'s validator, again. The editor surfaces its
     verdict earlier; it never forms one.
   - Routes, reachability, the overlay? → `PathSystem.Build`, the same call tick phase 2 makes.
   - Running the sim? → the real `Sim`, real `ContentSet`, real renderer.
   Writing a second version of any of these is the primary failure mode of this workflow.
4. **[model call: leaf generation]** Build it. Dev-only code under `godot/Dev/`; CLI code in
   `Gridfall.Verify`.
5. **[deterministic]** Verify the release exclusion if you touched `godot/Dev/`. Run the export and
   confirm the scene is absent. Check it — an assumption here ships a dev menu to players.
6. **[deterministic]** `dotnet build` 0/0. Tools are not in Core, so no determinism gate applies to
   them — but anything a tool *writes* must pass the game's validator, and that is not optional.
7. **[deterministic]** Update `tooling/docs/board-editor-spec.md` (or write a tool note) with what
   changed, including anything newly out of scope.
8. **[deterministic]** Write the human-check list. Every keybind you added, phrased as something to try.

## Output

The tool itself, plus a spec update or `tooling/specs/[slug]-tool-note.md`:

```markdown
# [Tool] — Note
**Status:** review

## Friction It Removes
## What It Reuses
| Game code | Instead of |
## Scope Added / Explicitly Not Added
## Release Exclusion
Verified how: …
## What a Human Must Try
1. …
```

## Done when

- [ ] The friction is named with a number
- [ ] Nothing the game already does was reimplemented
- [ ] Dev code is under `godot/Dev/` and its absence from a release export was **verified**
- [ ] `dotnet build` 0/0
- [ ] Anything written to disk passed the game's validator
- [ ] Spec or tool note updated, including what was deliberately left out
- [ ] Human-check list written; no claim that the UI works

## Failure modes

- **Reimplementing the game's code.** A second picker or a second validator drifts, and then the editor
  and the game disagree about what a legal map is. That is worse than having no editor.
- **Reporting an estimate as a fact.** The editor's maze check is a greedy lower bound. Say so wherever
  it appears — a number that looks exact will be quoted as one.
- **Scope visitor past the spec.** "While I was in there I added wave editing" is a v2 with no spec, no
  review, and no end.
- **Dev code in a release build.** Verify the export. Every time.
- **Claiming a UI works.** You cannot see it. Hand it over with a list of things to try.
- **A tool with no friction behind it.** Enjoyable to build, and it competes with the game for time.
