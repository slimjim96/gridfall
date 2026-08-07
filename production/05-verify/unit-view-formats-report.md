# Unit View Formats — Verification

**Slug:** `unit-view-formats` · **Status:** review · **Verdict:** PASS

Builds the two `IUnitView` implementations ADR-0004 specified in 2026-08-06 and never got: sprite
sheets and glTF meshes. The ADR's *decision* was "one interface, both implementations"; only the
placeholder existed, so the insurance it bought had not actually been bought.

**This does not answer the format question.** It makes the question answerable — the bake-off in
`presentation/prompts/tower-frost-spire.md` now has somewhere to put its two returns.

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` (5 projects) | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **179 passed**, 0 failed (was 176; +3) |
| Determinism trace | PASS | `Verify replay` — 30/30 checkpoints |
| **Shipped game unchanged** | PASS | `board-baseline.png` md5 `18a4cfb97a0a6065dc621d5916ca2925`, byte-identical |
| New capture reproducible | PASS | Two runs, both `25d37bd818f362bcb451d4fa154b21a7` |
| Sim untouched | PASS | `formats` seed hash `e8468a5c83dd11d6` stable across the material change |

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | A sprite folder resolves to `SpriteUnitView` | PASS | `units: loaded arrow-tower (Sprite), cannon (Mesh)` |
| 2 | A `.glb` folder resolves to `MeshUnitView` | PASS | Same line |
| 3 | Neither present → placeholder, as before | PASS | `units: presentation/units/ has no usable folders`, baseline byte-identical |
| 4 | **A sprite occludes what is behind it** | PASS | `unit-formats-baseline.png` — a creep is cut in half by the arrow tower's quad |
| 5 | **A mesh occludes what is behind it** | PASS | Same frame — a creep's lower half is hidden by the cannon drum |
| 6 | Sprite clips advance and one-shots return to idle | PASS by construction | `SpriteUnitView.Advance`; `fire` holds its last frame then re-plays `idle` |
| 7 | Mesh clips play from the asset's own AnimationPlayer | PASS | Fixture ships a `fire` clip; `PlayClip` sets loop mode from the guide's table |
| 8 | An unknown clip is ignored, not an error | PASS by construction | Both views return early; required by `IUnitView` |
| 9 | Level and damage cues identical across all three views | PASS by construction | Same growth curve and same `Lerp(Damaged) → Darkened` in all three |
| 10 | A `.glb` cannot violate the flat-matte art direction | PASS by construction | `MeshUnitView` forces roughness 1 / metallic 0 / specular off |
| 11 | Tinting does not destroy the asset's own materials | PASS by construction | Duplicates per surface, multiplies albedo; never `MaterialOverride` |
| 12 | A misnamed asset folder fails the build | PASS | `UnitAssetTests`, 3 tests |

## Two defects found, both by looking

### The mesh rendered as an open funnel

The first capture showed the cannon as a hollow cone with a black interior. Inverted winding: glTF
front faces are counter-clockwise **seen from outside**, and `low[i] → low[j] → high[j]` winds
clockwise from out there, so Godot culled every side and the top cap, leaving the inside of the far
wall on screen.

Invisible in most glTF viewers, which draw double-sided. Fixed, and then made unfixable-in-silence:
the generator now checks every triangle's winding against its own vertex normal and refuses to write
the file. **The check was verified by reintroducing the bug** — it exits 1 with
`triangle 0 winds against its normal (dot=-0.1708)`.

### Fixtures would have silently invalidated three baselines

The first version put the fixture assets in `presentation/units/`, the production folder. The default
shot seed builds arrow towers, so a fixture arrow tower changes `board-baseline`, `sapper-baseline`
and `repair-baseline` — three committed baselines, replaced by throwaway test art, in a slice that
was not supposed to change how the game looks at all.

Caught by checking `board-baseline` rather than assuming. Fixtures now live in
`presentation/units-fixtures/` and are opt-in via `--units`:

```bash
./run-game.sh --units presentation/units-fixtures --shot-seed formats --shot /tmp/x.png --shot-after 40
```

> **Verification art has to be opt-in, or it stops being verification and becomes an art decision
> nobody made.**

## Design notes

**Discovery by convention, not by a data field.** ADR-0004 said "asset format becomes a per-entity
data field"; this uses a folder named for the content id instead. Same outcome — format is data, not
architecture — with no change to `content-data`, no change to Core, and the identical shape as the
tile system shipped last slice. Nothing new crosses the Core boundary because nothing crosses it at
all.

**The sprite pivot is not `height / 2`.** The art already carries the camera's foreshortening, so the
quad is a full billboard and its size maps 1:1 to the screen. For its bottom edge to land on the cell
*on screen*, the offset is `height / (2·cos(pitch))` — 15% more than the naive value at the contract's
30° pitch. Every unit would have looked sunk into the board.

**Alpha-scissor, never alpha-blend.** Godot writes no depth for a blended surface, and a surface that
writes no depth hides nothing. That one line is the difference between criterion 4 passing and the
whole sprite path being worthless for an iso game.

## Not Verified

| What | Why |
|---|---|
| Real Ludo.ai output in either format | None exists. That is the bake-off, and this slice is its prerequisite. |
| Sprite `move`, `hit`, `death` clips | The fixture ships `idle` and `fire`. The code path is shared with `fire`. |
| Mesh clips other than `fire` | Same — one clip exercises the AnimationPlayer path. |
| Behaviour at peak wave density with real art | Fixtures are two towers on one board. |
| Whether either format actually looks good | Fixtures are ugly on purpose. |

## Branch Resolution

None — verdict is PASS. Both defects were found and fixed within stage 04.
