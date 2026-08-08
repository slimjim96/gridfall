# Next Steps

**Written:** 2026-08-08 · **State:** branch `main`, tree clean, `dotnet build` 0/0 · **200 tests** ·
replay 30/30 · 12/12 maps valid, all twelve selectable, and now all twelve **actually looked at**.

A session-crossing handoff. The filename-is-the-status rule still holds everywhere else — this file
exists only because the open threads span four workspaces and their ordering is not derivable from any
one folder.

---

## The decision that gates everything else

[`game-design/docs/fulfilment-direction.md`](../../game-design/docs/fulfilment-direction.md) is
**`proposed`**, not accepted. It re-frames the whole game — Tower→Station, Enemy→Visitor, HP→Appetite,
Lives→Patience — and argues Tier 1 is a rename and an art pass over a simulation that is already
balanced, with no arithmetic touched.

**That is the human's call, and the naming especially.** Nothing below assumes it either way, but if it
is accepted the rename should land *before* more content is built, because a half-renamed codebase is
bilingual for months. The first slice is defined at the bottom of that doc: one pass, one commit, tests
green.

---

## Fixed already

**Board select could only reach nine maps, and there are twelve.** Slots now run `1`–`9` then `a`–`z`,
bounded by one number in one place. All twelve are playable.

**The editor's capture path painted over the map it was told to show.** `--shot` ran
`SeedForScreenshot()` unconditionally, so `./run-editor.sh meander --shot x.png` captured *meander with
a wall and a road drawn through it* — silently, because the result is still a legal board that still
validates. Seeding is now blank-boards-only. This is why item 1 below had been open: the one tool for
doing it produced a wrong answer that looked right.

**Three maps were shipping validator warnings.** `spiral`, `stepwell` and `driftway` had 5, 6 and 2
buildable cells walled off. `make-example-levels.py` re-implemented `MapValidator` instead of calling
it and omitted that check, and `Verify -- maps` never calls the validator at all, so nothing anybody
ran disagreed. The generator now seals stranded cells and refuses to write a map with any left.

**And sealing them was not cosmetic.** Same seed, same 150 runs, `spiral` went **41.3% → 25.3% runs
lost** on five cells. A walled-off buildable cell is a *decoy*: the policy builds there, the tower never
fires, the run is down a tower. `spiral` is now the only generated level inside the 15–30% band.

**`Verify -- maps` now calls `MapValidator`.** It was a geometry report that had separately
re-implemented the buildable band, the path/floor split and the lane cap — three rules in two places —
and carried no stranded-cell check at all, which is how it printed a clean sheet for three warning
maps. It now prints the validator's findings verbatim and keeps only what the validator does not do
(`cover`, `useful`, `maze`, density), and **exits 1 on any error** rather than always 0.
`ShippedMapValidityTests` runs the same validator over every map in CI, so the generator, the report
and the editor cannot drift apart again. 203 tests.

**`editor-baseline.png` was stale and is re-recorded.** It diverged in one 191×15 box of HUD text:
`MapValidator`'s info line gained the `vs floor` clause after the baseline was taken, so it read
`path 19, spawns 1` where the build now prints `path 19, 1.0x the 19-cell floor, spawns 1`. Every
other pixel matched, which is the useful part — **the renderer reproduces byte-for-byte across the
two environments**, so the drift was text, not rendering. `board-baseline.png` still matches exactly.

## Ordered

### 1. The levels have been seen. Four of ten do not read.

[`presentation/docs/level-atlas-iso.png`](../../presentation/docs/level-atlas-iso.png) is a real
in-engine contact sheet of all twelve, regenerable with:

```bash
python3 content-data/maps/capture-iso-atlas.py    # needs a display + godot-mono
python3 content-data/maps/render-atlas.py         # schematic fallback, headless
```

`comb`, `ringfort`, `atoll` and `switchback` are legible as their motif. **`spiral`, `chambers`,
`braid` and `stepwell` are not** — a spiral reads as a C, a braid as a single route. The motif is a
top-down claim and the game is not top-down; walls have height and hide what is behind them.

Two things fell out that need a decision, not more measurement:

- **Ten of twelve maps have no road.** Only `crossroads` (42 cells) and `ringfort` (32) use
  `PathOnly`. On the rest the route exists only in the flow field, drawn only by the *editor's*
  overlay — which the game does not have. Should the generator emit `PathOnly`, or is an unmarked
  route intended?
- **The palette set is crowded at the desaturated end.** The three new themes are no worse than the
  seven already shipped — the tightest pair, `ocean`/`slate` at ΔE 8.6, predates them — but six of ten
  themes are now blue-grey. `atoll` (tundra) and `switchback` (slate) read as one board at thumbnail
  size.

The five ten-second questions for a human are at the bottom of
[`content-data/docs/example-levels.md`](../../content-data/docs/example-levels.md).

### 2. Eight of the ten are untuned, and four are degenerate

Wave tables are copied from `crossroads` verbatim. At 150 runs (that doc has the table):

- `comb` 42.0% lost — above the 15–30% band
- `spiral` 25.3% — in band, and only after the decoy fix
- `chambers`, `braid`, `switchback`, `atoll` — 0.0–0.7%, too easy
- `ringfort`, `meander`, `stepwell`, `driftway` — 0.0% lost at sd ≤ 0.4, **degenerate**

Still a scope call: a per-level balance pass (ten passes), or pick the two or three that earn a tuning
budget and mark the rest as generator output. Note that four of the untuned maps are also four that do
not read (§1) — **tuning a level nobody can parse is the expensive half of a job whose cheap half was
never done.** `comb` is the strongest candidate: hardest, most legible, the only one whose geometry
does real work at 2.1× floor.

### 3. `route-variance-metric` — open, with three predictors ruled out and one refined

`gauntlet` and `ringfort` both lose 0.0% of runs at sd ≈ 0 — the same signature from two independently
built maps, and **no metric in the repo explains either.**

Ruled out, do not re-derive:

| Candidate | Why it failed |
|---|---|
| Maze multiplier (`maze`, editor F6) | `gauntlet` 1.0× vs `crossroads` 1.1× — adjacent, outcomes sd 0.0 vs 7.1. A 1.15× threshold flagged 9 of 12 including a known-good map. |
| Buildable-share-of-route | `gauntlet` is 96%, same as everything else. Separates nothing. |
| Legibility of the motif at iso | `ringfort` and `atoll` both read well and are both degenerate. |

**`useful` is no longer a clean miss.** Raising it by *adding* cells near the route did nothing
(43%→60% on `spiral`, no change). Raising it by *deleting* cells far from the route moved `spiral` 16
points. Same metric, opposite readings — so it was never measuring a dial to turn up, it was
undercounting cells that actively mislead. Untested beyond one map.

Suggestion, not a conclusion: the next attempt is probably **simulation-derived** — sample tower
placements and measure the spread of outcomes — since every purely geometric candidate has failed the
same way, and the one result that did move a map came from removing bad placements rather than from
describing the shape.

### 4. Tier 2's soft-lock question is unanswered

If a station cannot answer a visitor's question it does nothing, and *"my stations do nothing"* is the
exact unreadable failure that `DamageSystem`'s floor-at-1 rule exists to prevent. The direction doc
lists three options (partial progress, waves that only ask answerable questions, unanswered visitors
slow instead of pass). **Settle it before Tier 2 is scheduled** — it is the whole design.

---

## Where the state actually lives

| Thing | File |
|---|---|
| The ten levels, their metrics, what was ruled out, and what a human must still eyeball | `content-data/docs/example-levels.md` |
| Bands, cover/useful figures, balance history | `content-data/docs/balance-targets.md` |
| Product direction (proposed) | `game-design/docs/fulfilment-direction.md` |
| Regenerate maps / schematic atlas / iso atlas | `content-data/maps/make-example-levels.py`, `render-atlas.py`, `capture-iso-atlas.py` |
| Per-map balance reports, newest first | `content-data/docs/reports/` |

Regenerating is all-or-nothing and validates before writing:

```bash
python3 content-data/maps/make-example-levels.py
python3 content-data/maps/render-atlas.py
```
