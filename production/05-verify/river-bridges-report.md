# Rivers and Bridges — Verification

**Slug:** `river-bridges` · **Status:** review · **Date:** 2026-08-09
**Verdict:** PASS on everything an agent can check. **The look is unverified** — see §What a human must look at.

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **244 passed**, 0 failed (was 234; +10) |
| Determinism trace | PASS | 30/30 checkpoints, **no re-record** |
| `Verify maps` | PASS | exits 0; twelve maps, no new warnings |
| Map geometry unchanged | PASS | `cells`, `spawns`, `goal`, `stations` byte-identical on all ten generated boards |
| Balance unchanged | PASS | Six river boards, 150 runs, seed 1 — **byte-identical** reports |
| Visual capture | **NOT DONE** | Needs a display; nothing here claims the boards look right |

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | A map may carry a per-cell surface layer | PASS | `MapDef.Surfaces`, `surfaces` glyph rows |
| 2 | Absent layer means all ground, and stays absent on save | PASS | `ABoardWithNoSurfaceLayerIsAllGround`, `ADryBoardWritesNoSurfaceLayerAtAll` |
| 3 | **Water is refused on a walkable cell** | PASS | `WaterOnAWalkableCellIsRefused` — a load **error** |
| 4 | **A span is refused on an unwalkable cell** | PASS | `ASpanOnAnUnwalkableCellIsRefused` |
| 5 | An unknown glyph is refused | PASS | `AnUnknownGlyphIsRefused` |
| 6 | The layer survives an editor round trip | PASS | `SurfacesSurviveAnEditorRoundTrip` |
| 7 | **A river changes nothing the simulation can see** | PASS | `ARiverChangesNothingTheSimulationCanSee` — same seed, same commands, identical hash |
| 8 | A bridge over nothing warns and does not block | PASS | `ABridgeTouchingNoWaterWarnsButDoesNotBlock` |
| 9 | A long bridge does not warn about its own middle | PASS | `ABridgeThreeCellsLongDoesNotWarnAboutItsOwnMiddle` |
| 10 | Rivers appear on the generated set without moving any board | PASS | Five boards, geometry diffed cell by cell |
| 11 | Balance figures do not move | PASS | Six boards × 150 runs, reports diffed in full |
| 12 | The boards read as rivers with bridges over them | **UNVERIFIED** | Agent cannot see. §below |

## Criterion 3 is the slice

Everything else here is plumbing. The load-bearing decision is that **water is only legal on a cell
that is already `Blocked`**, enforced as an error.

That is what lets Core ignore the layer entirely. Visitors do not walk on water because the cell was
already blocked — not because anything consulted the surface. Without the rule, the failure mode is the
worst kind available: a board that *looks* like it has a river, *plays* like it does not, and validates
either way. The repo has shipped that defect before in a different costume — the board editor's capture
path produced legal-looking frames of the wrong map, and ten levels were signed off from them.

## Two things measured rather than asserted

**Geometry.** `cells`, `spawns`, `goal` and `stations` compared against `HEAD` for all ten generated
boards: identical. The generator only paints over cells whose kind it did not touch.

**Balance.** All six boards the rivers touched, 150 runs, seed 1, full report text diffed against the
pre-change run: zero differences. Not "should be unchanged" — diffed.

## What a human must look at

Nothing in this slice claims the boards *look* right, and five of them changed appearance.

```bash
./run-editor.sh stepwell     # 3 bridge cells over a carved channel; `,` and `.` still sculpt
./run-editor.sh chambers     # a north-south river with a single crossing
./run-editor.sh meander      # took the fallback axis — does it still read as a river?
./run-game.sh                # play a river board and watch a visitor cross a bridge
```

Four questions the capture cannot answer:

1. **Does the water read as below the banks, or as a blue floor?** Depth comes from the height field.
   `driftway` and any flat board have no channel at all, by design.
2. **Does the bridge read as a bridge** at the moment a visitor steps onto it, or as a pale stripe?
3. **Do the derived colours hold on every theme?** Water is derived from each theme's blocked tone, so
   `desert`'s river and `tundra`'s river are different colours and neither was chosen by hand.
4. **Does a station built beside water still read as a station?** The palette rules say terrain must
   never compete with a unit, and water is the newest terrain.

## Deliberately not done

- **The board editor cannot paint surfaces.** It carries them through save and load — there is a test —
  but adding a brush is a spec change, and `board-editor`'s v1 scope is closed by decision.
- **No authored water or bridge tiles.** Surfaced cells take the derived flat colour and skip the
  terrain tile set on purpose: a grass image tinted blue is not water. Authored tiles are
  `ludo-tile-prompts`.
- **Rivers do not affect pathing.** Chosen explicitly. The alternative — water as a new cell kind,
  bridges as the only crossing and therefore chokepoints worth defending — is a real game and a
  different slice, needing an ADR, a validator change and a trace re-record.

## Loop-back

None.
