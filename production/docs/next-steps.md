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

### 2. The band is measured over two waves, and that has to be settled before any level is tuned

All twelve re-measured at 150 runs:
[`2026-08-08-example-levels-balance.md`](../../content-data/docs/reports/2026-08-08-example-levels-balance.md).
The scope call — ten passes, or two or three — turned out to be the wrong question. Three findings,
in the order they matter:

**a. `balance-targets.md` asks for 15–30% of runs lost in waves 11–20. Every wave table has 12
waves.** The late window is waves 11 and 12. Over ten waves that band is a gentle per-wave rate; over
two it needs a single wave to be a coin flip, which no level sits in stably. Either the tables grow to
20 waves or the band is restated for the 12 that exist. **Nothing else here is worth doing first** —
tuning towards a two-wave window is how six earlier passes read the number as `ok`.

**b. `comb` does not tune, and the knobs are not monotone.** Every lost run dies at wave 12 exactly,
so each global knob is a threshold rather than a dial: `hpGrowthFrom` 6→42%, 7→0%; `waveClearGold`
25→42%, 35→0%, **45→52%**, 60→0%. `hpGrowth` is already at 1.10, the floor of its band. Landing 15–30%
here would be luck. `comb` needs wave 12 spread across waves 8–15 — a composition job, after (a).

**c. `crossroads` fails both targets and `spiral` passes both.** The reference board loses 18.7% early
and 1.3% late — inverted, as recorded on 2026-08-07 and still true. `spiral`, whose wave table is
still `crossroads`'s copied verbatim, is at 0.0% early and 25.3% late. **The best-balanced board in the
repo is a generator artefact that got there via a bug fix**, which is worth a moment's suspicion of
what "tuned" has meant here.

Two rows of the old table were also just wrong — `braid` and `switchback` were measured before
`tower-range-tiers` and never re-measured. `braid` is degenerate (sd 0.2), not easy, making it five
degenerate levels rather than four.

### 3. `route-variance-metric` — **blocked on §2a**, with four predictors ruled out and one refined

`gauntlet` and `ringfort` both lose 0.0% of runs at sd ≈ 0 — the same signature from two independently
built maps, and **no metric in the repo explains either.**

Ruled out, do not re-derive:

| Candidate | Why it failed |
|---|---|
| Maze multiplier (`maze`, editor F6) | `gauntlet` 1.0× vs `crossroads` 1.1× — adjacent, outcomes sd 0.0 vs 7.1. A 1.15× threshold flagged 9 of 12 including a known-good map. |
| Buildable-share-of-route | `gauntlet` is 96%, same as everything else. Separates nothing. |
| Legibility of the motif at iso | `ringfort` and `atoll` both read well and are both degenerate. |
| Seal pressure (placements refused for walling off the route) | `ringfort` is lowest at 14k and degenerate; `crossroads` is next-lowest at 26k and the most varied. Inverts. |

**The target variable was wrong, which is the real reason three candidates failed.** The hunt has been
for a map metric predicting *sd of lives left*. That statistic mixes two unrelated shapes:

| Map | sd | Lost runs died at wave | Shape |
|---|---|---|---|
| `crossroads` | 8.1 | 3.7 avg, **earliest 3, latest 12** | a curve — many waves can kill you |
| `comb` | 5.9 | 12.0, earliest 12, latest 12 | **a wall** — one wave, pass or fail |
| `chambers` | 5.7 | 3.0, earliest 3, latest 3 | a wall, early |

`comb` and `crossroads` have near-identical sd and nothing else in common. **No property of a map could
ever separate those, because the difference lives in the wave table.** Three geometric candidates were
regressed against a number that was mixing two populations.

**Death-wave spread** (`latest - earliest` over lost runs) separates `crossroads` from all eleven
others immediately: spread 9 against spread 0 everywhere. But **n = 1** — one board has spread and no
other has any, which is as consistent with "spread is what tuning produces" as with "spread is what a
good map permits". Not a metric yet; a better-posed target than sd.

**That test has now been run, and it makes this item blocked rather than open.** `comb` extended to 20
waves on a scratch copy: spread goes 0 → 1 → 2 as runway is added, at every ramp tried including flat
counts. A map cannot show death-wave spread after the last wave in its table, and `comb`'s difficulty
crossing falls at wave 12 of 12 — so its spread is 0 *by construction*, not by geometry. `crossroads`
has spread 9 only because its crossing falls at wave 3, leaving nine waves for outcomes to separate.

Eleven of twelve maps cross at or near the last wave. **The table length has already flattened the
target variable for all of them**, so no map metric can be validated against it today. Settle §2a,
then re-measure — the discriminator may be visible then and demonstrably is not now.

Standing-tower count was checked on the way and fails too: `braid` and `ringfort` both hold 37.4 and
are both degenerate.

**`useful` is no longer a clean miss.** Raising it by *adding* cells near the route did nothing
(43%→60% on `spiral`, no change). Raising it by *deleting* cells far from the route moved `spiral` 16
points. Same metric, opposite readings — it was never a dial to turn up, it was undercounting cells
that actively mislead.

### 4. Tier 2's soft-lock question — priced, still yours to decide

The three options are now costed against the engine in
[`game-design/docs/tier2-soft-lock-options.md`](../../game-design/docs/tier2-soft-lock-options.md).
They are not close:

- **A · partial progress** is *the same line of code* — `DamageSystem.cs:101` already floors damage at
  1; a mismatch is a penalty subtracted like armour. No new state, no new hash surface.
- **B · only-answerable waves** makes `SpawnSystem` read tower state, turning wave tables into
  generator inputs and changing what a recorded trace means. It lands on determinism and static
  content, the two things the project protects hardest.
- **C · unanswered visitors slow** needs a slow mechanic, and **there is none in Core** — new
  per-creep state, new hash coverage. It also *replaces* the soft-lock with a worse one: a board with
  no correct station stalls forever, neither failing nor progressing.

Recommendation is A, because it keeps the no-soft-lock invariant rather than rebuilding it, and
because it makes the question measurable — set the penalty, run `balance`, read runs lost.

**The decision is still yours and it is not technical:** should a child be able to finish a level
without doing the arithmetic? A says "yes, slowly"; B says "the question never arises"; C says "no".

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
