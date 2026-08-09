# Work Log

One section per working period, newest first. **What happened and what it taught** — not a changelog;
`git log` is the changelog. This exists because the durable findings of a session are otherwise spread
across a dozen commit messages, and the next person needs the conclusions without the archaeology.

Open threads live in [`next-steps.md`](next-steps.md), not here.

---

## 2026-08-09 · elevation

Boards climb. A per-cell height field, **view-only** — the simulation is still computed on the flat
grid, a hilly board hashes identically to the same board flat, and `replay` passed untouched. The five
game baselines are byte-identical because `crossroads` and `gauntlet` are hand-authored and flat.

Three wrong turns, all worth not repeating:

- **Independent per-cell jitter reads as static**, not as ground. Noise has to be spatially correlated
  — a coarse lattice, smoothstepped between.
- **Height must follow *walking* distance to the goal**, not straight-line, or the ground cuts through
  the board's own walls instead of bending around them.
- **Pulling the route's shoulders to *within* one level is not enough.** At a 30° pitch one level of
  0.22 hides 0.38 of a cell and a route marker is 0.46 wide, so `stepwell` showed three markers of
  twenty-two. The route needs a flat shelf either side; scenery keeps its height.

And one distinction worth keeping: the editor's route overlay draws **through** terrain, the game's
does not. In the editor it is diagnostic UI that should not be occluded by its own subject; in the game
the same markers would draw over a visitor's feet. Scoping it to the editor is why the game baselines
did not move.

The trap: picking. A ray tested against `y = 0` flies over raised terrain and lands ~1.7 cells behind
per unit of height, so clicking a hilltop selects a cell up the slope. It iterates now — and the editor
was on a separate flat overload that would have kept mis-picking on the very boards it can sculpt.

---

## 2026-08-08 → 09 · twenty commits

**State at the end:** `main` pushed, tree clean, build 0/0, **217 tests**, replay 30/30, `maps` exits 0.

### What shipped

| Area | Outcome |
|---|---|
| Board editor | `--shot` no longer paints over the map it was told to capture |
| Maps | Three maps had validator warnings nobody could see; generator now seals stranded cells |
| Maps | Four motifs redrawn so they survive the iso projection |
| Verify | `maps` calls `MapValidator` instead of re-implementing three of its rules; exits 1 on error |
| Verify | `balance` reports a death-wave histogram and a killing-wave count |
| Balance | The late band restated for the twelve waves that exist |
| HUD | Station palette bar driven by a per-board roster; between-wave countdown ring |
| Content | Per-map station roster, enforced in `CommandSystem`, honoured by `PlayPolicy` |
| Art | WebP support; `arrow-station` is the first real asset; `fit-sprite.sh` |
| Vocabulary | The fulfilment rename, one pass, behaviour untouched |
| Docs | `theme-direction.md` as the single theme entry point |
| Portability | Locale, line-ending and enumeration-order guarantees, with tests |

### What it taught — the durable part

**A tool that produces a wrong answer confidently is worse than no tool.** The editor's capture path
seeded a synthetic board over whatever map you asked for. The result was a legal board that still
validated, so the frame *looked* right. Ten levels were signed off from it. Every claim about how they
read was false, and nothing anywhere said so.

**A rule with one authority and three paraphrases has no authority.** `MapValidator` is the game's
definition of a legal map. The generator re-implemented it and omitted a check; `Verify maps` never
called it at all while separately re-implementing three of its rules. Three maps shipped warnings.
The fix was deletion, not addition — both callers now ask the one authority.

**This system is chaotic in its inputs and stable in its seed.** Three independent hunts for a
tuning dial all came back knife-edged: `waveClearGold` 25→42%, 35→0%, **45→52%**; `comb` composition
242→0%, 244→42%, 246→17%; `spiral` geometry one capacity point apart giving 0% and 33%. Meanwhile
`comb` measures 42.0/42.0/42.0/41.3 across four seeds. **A predictor needs the predicted thing to vary
smoothly with its inputs, and here it does not** — which is why five candidate metrics failed and why
the sixth would too. Full account: [`route-variance`](../../content-data/docs/reports/2026-08-08-example-levels-balance.md).

**Legibility and difficulty are decoupled.** Redrawing four maps changed how they *read* completely and
moved their outcomes almost not at all — `spiral` held ~26% through a total redesign. Treat readability
and balance as separate passes; neither falls out of the other.

**A target can outlive its content.** The late-difficulty band asked for 15–30% over "waves 11–20"
while every table had 12 waves — a two-wave window nothing could sit in except by coin flip. Six
earlier passes read that number as `ok`.

**Measure before believing your own write-up.** Two rows of the committed balance table did not
reproduce, and the sealed-cell explanation ("the tower never fires") was wrong — `TargetingSystem`
acquires on range alone, so it fires; it is simply bad value. The measurement stood, the story did not.

### Corrections made to earlier claims

Kept because a doc that quietly fixes itself teaches nothing:

- *"A tower on a walled-off cell never fires."* It fires. The mechanism is opportunity cost.
- *"A soft alpha fringe breaks occlusion."* Not here — `SpriteUnitView` hardcodes `AlphaScissor`, so
  the fringe is clipped and depth write is never lost. A quality note, not a correctness one.
- *"Defence capacity is the first metric that works."* It orders the twelve shipped boards and then
  fails the causal test. Ruled out.

### Traps worth not rediscovering

- A `Control` parented to a `CanvasLayer` inherits no rect — it stays 0×0 and renders as nothing,
  which is indistinguishable from never having been added.
- The **first** capture after a rebuild differs from every one after it (cold shader cache). Capture
  twice and compare before trusting a baseline diff.
- `double.Parse("0.06")` is **6** under a comma-decimal locale.
- The Ludo style anchor is copied *verbatim* into every prompt, so one stale word themes every asset
  ever generated from it.
