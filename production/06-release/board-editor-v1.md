# Board Editor — v1

**Slug:** `board-editor` · **Status:** done · **One spec requirement unmet — see below**

## What Shipped

A dev-only scene for painting maps and immediately playing them.

```bash
godot-mono --path godot --scene res://Dev/BoardEditor.tscn -- --map crossroads
```

Paint cell types, place spawns and the goal, watch the validator judge the map as you paint, and press
`F5` to play the unsaved draft. `F6` runs a greedy maze estimate, labelled a lower bound everywhere it
appears.

## The architectural point

The spec's central rule was that the editor implements **no validation of its own**. Making that
literally true meant extracting the loader's checks into `MapValidator`, which returns findings instead
of throwing, and having `ContentLoader` call it and throw on the first error.

One function now decides what a legal map is, called by both. The editor only decides how to *draw* the
answer: errors block a save with the validator's own message, warnings from `MapTargets` never block
anything.

`MapDraft` owns serialisation, so the editor writes exactly the format the loader reads. Painting keeps
the spawn list in sync with the `S` glyphs and moves the goal rather than allowing two — the editor
structurally cannot produce the malformed maps hand-editing produces constantly.

## Player-Facing Change

None — it is a development tool, excluded from release builds.

## Requirement Not Met

**`godot/export_presets.cfg` does not exist**, so "`Dev/` is absent from a release export" cannot be
checked, and the spec says *verified, not assumed*. The editor cannot ship by accident only because
nothing can be exported at all, which is not the same thing. Recorded as unmet rather than waived.

## Follow-Ups Not Done

| Item | Workspace | Slug |
|---|---|---|
| Export preset with a `Dev/` exclusion, then verify the scene is absent | tooling | `release-export` |
| Resize panel — `MapDraft.Resize` exists and is tested, no UI is wired to it | tooling | `editor-resize-ui` |
| `Ctrl+O` open — use `--map <id>` for now | tooling | `editor-open-dialog` |
| A map with a sealable pinch, so the refusal paths are reachable in a real game | content-data | `pinch-map` |

## Known Not Verified

- **Undo/redo, `F5` playtest and `Esc` back, `F6` estimate, the `F1` overlay.** Wired and compiling,
  never exercised by hand.
- Human sign-off 2026-08-06 **does** cover painting, live validation, and save refusal — the whole
  error path end to end, and the one path a scripted capture could not reach.

## A Bug Worth Remembering

The validator extraction broke 53 of 86 tests with the message `fixture:  (0,0)`. Three compounding
causes: `MapSeverity.Error` was the **zero value**, so `default(MapFinding)` claimed to be an error;
`MapFinding` is a **struct**, so LINQ's `FirstOrDefault` returns that default rather than `null`; and
the cell defaulted to `(0,0)`, which I had special-cased as "no cell" when **(0,0) is a legal cell** —
spawns sit on the west edge. The enum now starts at 1, with a comment saying why.
