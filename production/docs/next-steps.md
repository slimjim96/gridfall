# Next Steps

**Written:** 2026-08-08 · **State:** branch `ten-example-levels`, tree clean, `dotnet build` 0/0 ·
**200 tests** · replay 30/30 · 12/12 maps valid and all twelve selectable.

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

**Board select could only reach nine maps, and there are twelve.** `spiral`, `stepwell` and
`switchback` were unreachable — silently, because the list simply stopped. Slots now run `1`–`9` then
`a`–`z`, bounded by one number in one place instead of the two disagreeing literals that caused it, and
anything past the thirty-fifth is counted on screen rather than dropped. All twelve are playable.

## Ordered

### 1. Nobody has seen these levels at the iso angle

The display was down when they were generated, so
[`presentation/docs/level-atlas.png`](../../presentation/docs/level-atlas.png) is a **top-down schematic
rendered from JSON**, not a screenshot. Every claim about how these boards *read* is unverified.

```bash
./run-editor.sh <id>     # meander, spiral, chambers, switchback, comb,
                         # ringfort, braid, stepwell, atoll, driftway
```

This also gates the three new palettes — `tundra`, `ash`, `marsh` — whose 75 tiles were generated from
the ramp registry and have never been looked at.

### 2. Eight of the ten are untuned, and four are degenerate

Wave tables are copied from `crossroads` verbatim. At 150 runs
([`content-data/docs/example-levels.md`](../../content-data/docs/example-levels.md) has the table):

- `comb` 42.0% and `spiral` 41.3% lost — above the 15–30% band
- `chambers`, `braid`, `switchback`, `atoll` — 0.0–0.7%, too easy
- `ringfort`, `meander`, `stepwell`, `driftway` — 0.0% lost at sd ≤ 0.2, **degenerate**

Two ways forward, and it is a scope call: a per-level balance pass (ten passes), or pick the two or
three that earn a tuning budget and mark the rest as generator output. `crossroads` remains the only
tuned board in the repo.

### 3. `route-variance-metric` — open, with three predictors ruled out

`gauntlet` and `ringfort` both lose 0.0% of runs at sd ≈ 0 — no variance at all, the same signature
from two independently built maps, and **no metric in the repo explains either**. Both were built with
path-only corridors; `lane()` is recorded as a trap.

Ruled out, do not re-derive:

| Candidate | Why it failed |
|---|---|
| Maze multiplier (`maze`, editor F6) | `gauntlet` 1.0× vs `crossroads` 1.1× — adjacent, outcomes sd 0.0 vs 7.1. A 1.15× threshold flagged 9 of 12 including a known-good map. |
| Buildable-share-of-route | `gauntlet` is 96%, same as everything else. Separates nothing. |
| `useful` (share of buildable cells in range of the route) | Catches an *unwinnable* map at the floor, and orders nothing above it. Raising `spiral` from 43% to 60% changed its outcome not at all. |

The honest summary already committed: **no geometric metric here predicts whether a map plays.** The
only rule that has held is width — a one-cell corridor is undefendable because range is measured from
cell centres, and nothing in `MapTargets` sees it.

Suggestion, not a conclusion: the next attempt is probably **simulation-derived** rather than geometric
— sample tower placements and measure the spread of outcomes — since every geometric candidate so far
has failed the same way.

### 4. Tier 2's soft-lock question is unanswered

If a station cannot answer a visitor's question it does nothing, and *"my stations do nothing"* is the
exact unreadable failure that `DamageSystem`'s floor-at-1 rule exists to prevent. The direction doc
lists three options (partial progress, waves that only ask answerable questions, unanswered visitors
slow instead of pass). **Settle it before Tier 2 is scheduled** — it is the whole design.

### 5. Nothing is pushed

`main` is 1 ahead of `origin/main`; `ten-example-levels` is 3 ahead of `main`. Merge and push, or say
why not.

---

## Where the state actually lives

| Thing | File |
|---|---|
| The ten levels, their metrics, and what was ruled out | `content-data/docs/example-levels.md` |
| Bands, cover/useful figures, balance history | `content-data/docs/balance-targets.md` |
| Product direction (proposed) | `game-design/docs/fulfilment-direction.md` |
| Regenerate maps / atlas | `content-data/maps/make-example-levels.py`, `render-atlas.py` |
| Per-map balance reports, newest first | `content-data/docs/reports/` |

Regenerating is all-or-nothing and validates before writing:

```bash
python3 content-data/maps/make-example-levels.py
python3 content-data/maps/render-atlas.py
```
