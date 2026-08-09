# Next Steps

**Written:** 2026-08-08 · **State:** branch `main`, pushed, tree clean, `dotnet build` 0/0 ·
**212 tests** · replay 30/30 · `maps` exits 0 · 12/12 maps valid, all twelve selectable, all twelve
**actually looked at**, and `arrow-tower` is real art.

A session-crossing handoff. The filename-is-the-status rule still holds everywhere else — this file
exists only because the open threads span four workspaces and their ordering is not derivable from any
one folder.

**One decision blocks the rest and it is yours:** the direction doc below. The wave-table question in
§2a is settled (restated for twelve). §3 is closed as far as measurement can take it — five predictors
ruled out and a reason none of them can work — so what is left in §2 and §3 is content judgement, not
analysis. Everything measurable has been measured.

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
lost** on five cells. A walled-off buildable cell is a *decoy*: the policy builds there and the gold is
worse spent than it would have been. (It does fire — `TargetingSystem` acquires on range alone, with no
reachability test on the tower's cell. Corrected 2026-08-08: the measurement stands, the explanation
did not.) `spiral` is now the only generated level inside the 15–30% band.

**`Verify -- maps` now calls `MapValidator`.** It was a geometry report that had separately
re-implemented the buildable band, the path/floor split and the lane cap — three rules in two places —
and carried no stranded-cell check at all, which is how it printed a clean sheet for three warning
maps. It now prints the validator's findings verbatim and keeps only what the validator does not do
(`cover`, `useful`, `maze`, density), and **exits 1 on any error** rather than always 0.
`ShippedMapValidityTests` runs the same validator over every map in CI, so the generator, the report
and the editor cannot drift apart again.

**`editor-baseline.png` was stale and is re-recorded.** It diverged in one 191×15 box of HUD text:
`MapValidator`'s info line gained the `vs floor` clause after the baseline was taken, so it read
`path 19, spawns 1` where the build now prints `path 19, 1.0x the 19-cell floor, spawns 1`. Every
other pixel matched, which is the useful part — **the renderer reproduces byte-for-byte across the
two environments**, so the drift was text, not rendering. `board-baseline.png` still matches exactly.

## Ordered

### 1. The levels have been seen, and the four that did not read are redrawn

[`presentation/docs/level-atlas-iso.png`](../../presentation/docs/level-atlas-iso.png) is a real
in-engine contact sheet of all twelve, regenerable with:

```bash
python3 content-data/maps/capture-iso-atlas.py    # needs a display + godot-mono
python3 content-data/maps/render-atlas.py         # schematic fallback, headless
```

**Fixed 2026-08-08.** `spiral` read as a C, `chambers` as one open field, `braid` as a single route and
`stepwell` as an edge with four detached strips. All four are redrawn and all ten now read. Two rules
came out of it:

- **A one-cell wall is a scratch; solid masses read.** Every divider is two cells thick now — which is
  why `ringfort` and `comb` always read and the thin-walled ones never did.
- **A motif you only draw is not a motif.** `spiral`'s goal sat on the east edge, so its route was the
  Manhattan minimum and never turned; the spiral existed only in scenery the route ignored. Goal in the
  middle now, enclosed, **1.9×** the floor. `chambers` went 1.0× → 1.5× and 20 → 30 path the same way.

**Legibility moved; difficulty did not.** `spiral` held ~26% through a complete redesign; `chambers`,
`braid` and `stepwell` are still 0.0%. Same decoupling as everywhere else in §2/§3.

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

### 2. The band is restated; `comb` still cannot be tuned, and now we know why

All twelve re-measured at 150 runs:
[`2026-08-08-example-levels-balance.md`](../../content-data/docs/reports/2026-08-08-example-levels-balance.md).
The scope call — ten passes, or two or three — turned out to be the wrong question. Three findings,
in the order they matter:

**a. RESOLVED — the band is restated for twelve waves.** The split is `waveCount / 2`, so 1–6 / 7–12,
and `Verify balance` prints the range it used. Band values unchanged; only the window was wrong.
Growing the tables to 20 was the alternative and was not chosen.

No map's verdict moved — deaths land on wave 3 or wave 12, the same side of either boundary. What it
bought is a target a level can *reach*, and a new one that catches what the percentages missed:
**waves that can kill you ≥ 3.** Measured: `crossroads` 5 of 12, every other map 1 or 0. `spiral`
passes both percentage bands and is still not a good level, because all 25.3% of its lost runs die at
wave 12. A single lethal wave is a gate, not a difficulty curve.

**b. `comb` does not tune, and now there is a target that says why.** Every lost run dies at wave 12
exactly — 1 killing wave of 12 — so each global knob is a threshold rather than a dial: `hpGrowthFrom`
6→42%, 7→0%; `waveClearGold` 25→42%, 35→0%, **45→52%**, 60→0%. `hpGrowth` is already at 1.10, the
floor of its band. Nothing global can widen a single lethal wave, because every knob moves that wave
through the threshold all at once.

**Tried, 2026-08-08: fifteen configurations across waves 9–12. It does not work, and the failure is
the useful part.** Two killing waves is the ceiling and only at 42% — landing in band always cost the
spread. Worse, it is not monotone: effective 242 → 0.0%, 244 → 42.0%, 246 → 16.7%, 248 → 42.0%.
Weakening the late waves made the level harder, then trivial, then harder. Any config that lands in
band is luck, because its neighbour two points away is 42%.

And 42.0% is structural — it recurs across five unrelated configurations and holds at seeds 1–4
(42.0/42.0/42.0/41.3). **42% of the boards this policy builds on `comb` cannot hold the endgame
whatever the endgame is made of.** Composition moves which wave kills, not how many runs die.

`comb.json` is unchanged; everything ran against a scratch copy.

**c. `crossroads` fails both targets and `spiral` passes both.** The reference board loses 18.7% early
and 1.3% late — inverted, as recorded on 2026-08-07 and still true. `spiral`, whose wave table is
still `crossroads`'s copied verbatim, is at 0.0% early and 25.3% late. **The best-balanced board in the
repo is a generator artefact that got there via a bug fix**, which is worth a moment's suspicion of
what "tuned" has meant here.

Two rows of the old table were also just wrong — `braid` and `switchback` were measured before
`tower-range-tiers` and never re-measured. `braid` is degenerate (sd 0.2), not easy, making it five
degenerate levels rather than four.

### 3. `route-variance-metric` — five predictors ruled out, and the reason none can work

`gauntlet` and `ringfort` both lose 0.0% of runs at sd ≈ 0 — the same signature from two independently
built maps, and **no metric in the repo explains either.**

Ruled out, do not re-derive:

| Candidate | Why it failed |
|---|---|
| Maze multiplier (`maze`, editor F6) | `gauntlet` 1.0× vs `crossroads` 1.1× — adjacent, outcomes sd 0.0 vs 7.1. A 1.15× threshold flagged 9 of 12 including a known-good map. |
| Buildable-share-of-route | `gauntlet` is 96%, same as everything else. Separates nothing. |
| Legibility of the motif at iso | `ringfort` and `atoll` both read well and are both degenerate. |
| Seal pressure (placements refused for walling off the route) | `ringfort` is lowest at 14k and degenerate; `crossroads` is next-lowest at 26k and the most varied. Inverts. |
| **Defence capacity / route** (2026-08-08) | Splits the twelve shipped boards cleanly, then fails the causal test: two `spiral` geometries one capacity point apart, same route and buildable, give 0.0% and 32.7%. |

**Stop looking for a column.** Three independent hunts for a dial on this system have all come back
knife-edged — `waveClearGold` (25→42%, 35→0%, 45→52%), `comb` composition (242→0%, 244→42%, 246→17%),
`spiral` geometry (1.09→0%, 1.14→33%). Outcomes are **stable to seed** (`comb` is 42.0/42.0/42.0/41.3
at seeds 1–4) and **chaotic in inputs**: the policy places greedily, so a one-cell change sends it down
a different build order, which mazes the route differently. A predictor needs the predicted thing to
vary smoothly with its inputs, and here it does not. The next attempt should measure a **distribution
over perturbed boards** rather than one number — or accept that maps are judged by playing them.

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

**`Verify balance` now reports the whole distribution**, not just its ends — `by wave` and
`waves that can kill`. That is the statistic the hunt should have been using:

| Map | By wave | Waves that can kill |
|---|---|---|
| `crossroads` | w3:17% w4:1% w5:1% w11:1% w12:1% | **5 of 12** |
| `comb` | w12:42% | 1 |
| `spiral` | w12:25% | 1 |
| `chambers` | w3:1% | 1 |
| the other eight | — | **0** |

**n is still 1** — one board has more than one killing wave — so this is a better-posed target, not yet
a validated metric.

**And the runway result still stands, but now points somewhere.** `comb` extended to 20 waves on a
scratch copy: spread goes 0 → 1 → 2 as runway is added, at every ramp tried including flat counts. A
map cannot show killing waves after the last wave in its table, and `comb`'s difficulty crossing falls
at wave 12 of 12. `crossroads` has five only because its crossing falls at wave 3.

**§2b has now answered this, and the answer is "geometric, but not the geometry anyone measured."**
`comb` could not be spread by composition at all, and its 42% held across every wave table tried and
every seed. What does explain it is placement capacity: **17.8 towers standing from 243.7 built, with
102,682 placements refused by the seal check** — the highest in the set against `crossroads`'s 25,879.
The teeth that make its route 2.1× the floor also mean nearly every buildable cell would wall the route
off, so the policy's defence caps out around wave 5 and the rest is decided by which cells it took.

**Built, 2026-08-08. `Verify -- maps` now reports `capacity` and `cap/route`** — towers placeable by
best-coverage order, each checked against the game's own `WouldRemainConnected`, route re-traced after
every placement. Across the eleven maps sharing one wave table:

| cap/route | Maps | Late runs lost |
|---|---|---|
| 1.16 – 2.26 | nine, `crossroads` down to `switchback` | **0.0%** every one |
| 1.03 | `comb` | 42.0% |
| 0.95 | `spiral` | 25.3% |

It was the first candidate that did not invert. **The causal test then refuted it.**

Five geometries of `spiral`, all route length 22, same waves, same seed, only wall positions changed:

| Variant | Capacity | cap/route | Late lost |
|---|---|---|---|
| shipped | 21 | 0.95 | 25.3% |
| **C** | **24** | **1.09** | **0.0%** |
| **A** | **25** | **1.14** | **32.7%** |
| D | 32 | 1.45 | 0.0% |
| B | 37 | 1.68 | 0.0% |

**C and A differ by one point of capacity** — same size, same 47% buildable, same route — and go 0.0%
against 32.7%. Reproduced. So `cap/route` orders the twelve shipped boards and predicts nothing about a
board you are editing, which is the only use a map metric has. It joins the ruled-out list; the column
stays as a diagnostic because it is a real property, and **no threshold is enforced**.

`spiral` is unchanged — every variant ran through the generator and was reverted, and the JSON is
byte-identical.

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
| All twelve measured, the knob sweeps, the runway result | `content-data/docs/reports/2026-08-08-example-levels-balance.md` |
| Bands, cover/useful figures, balance history | `content-data/docs/balance-targets.md` |
| Product direction (proposed) | `game-design/docs/fulfilment-direction.md` |
| The three soft-lock options, costed | `game-design/docs/tier2-soft-lock-options.md` |
| Regenerate maps / schematic atlas / iso atlas | `content-data/maps/make-example-levels.py`, `render-atlas.py`, `capture-iso-atlas.py` |
| Per-map balance reports, newest first | `content-data/docs/reports/` |

Regenerating is all-or-nothing and validates before writing:

```bash
python3 content-data/maps/make-example-levels.py
python3 content-data/maps/render-atlas.py
```
