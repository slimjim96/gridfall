# Work Log

One section per working period, newest first. **What happened and what it taught** — not a changelog;
`git log` is the changelog. This exists because the durable findings of a session are otherwise spread
across a dozen commit messages, and the next person needs the conclusions without the archaeology.

Open threads live in [`next-steps.md`](next-steps.md), not here.

---

## 2026-08-09 · rivers, bridges, and two design calls

Five boards have rivers now, with bridges where the routes already crossed. View-only, and this time
**enforced rather than promised**: water is a load error on any cell that is not already `Blocked`.

**A view-only layer that makes a claim about the rules cannot rest on a code review.** Elevation is
safe because a height says nothing — it is decoration, and nobody reading it wrongly can mislead a
player. A *surface* says "nothing walks here", so a layer that could say that falsely would produce a
board that looks like it has a river, plays like it does not, and validates either way. That is the
board-editor capture defect again in a new costume. The legality rule is the whole slice; everything
else is plumbing.

**The bridges placed themselves.** The generator draws one straight line across the board; wall cells
on it become water and walkable cells become deck. Nothing decides where a crossing goes — it lands
wherever the route already was. Two boards asked for a north–south river, had no legal line, and took
the east–west fallback; `driftway` had none on either axis and is reported as `NONE FIT` rather than
silently dropped.

Three things that were wrong first, all of them the same shape — **a rule that is right per cell and
wrong per thing**:

- Water took the +0.28 wall raise (it *is* blocked terrain) and rendered as a raised blue wall.
- The channel was carved below the *average* neighbour, which leaves it proud of the shallower bank.
  It goes below the lowest.
- "Does this span cell touch water?" warns about the middle of every bridge three cells or longer.
  Flood the bridge, ask once.

And a fourth, cheaper to hit than to explain: a cell below the ground plane draws its side quads
inverted, so the render height clamps at zero. On a flat board a river is a colour and nothing more,
which is the right amount to promise on a board with no terrain in it.

Two design calls came out of the same conversation and neither is code:

- **Ten stations differentiated by "slower but stronger, faster but weaker" needs the *visitor* roster
  to spread first.** That axis is a decision only when something punishes many-small-hits, and the one
  thing that does — `fussiness` — is inert at shipped composition (measured the same day). Ten stations
  on a dead axis is ten stations where the cheapest wins.
- **The terrain direction quietly killed three of the five theme candidates.** Boards are places with
  elevation and now rivers; The Wash is a room, Please Hold is an office, Bin Night is a street. The
  board direction and the theme list were written two days apart and had never been read against each
  other.

---

## 2026-08-09 · policy fussiness

The balance harness ranked stations on **base** serving-per-gold, so it had never bought a cannon on
any board in any run ever measured. Fixed. All twelve maps came back **byte-identical**, and that is
the finding.

**A heuristic can be wrong for two independent reasons, and fixing the interesting one changes
nothing.** Ranking was the obvious defect. The second was structural: the policy bought the best
station it could *afford this tick* and kept no reserve, so on any roster the cheapest station is
bought the instant its price is reached and gold never approaches the price of anything else. A 90-gold
station is unreachable while a 50-gold one exists, whatever the census says. Census-awareness alone
left it building 2 arrows and 0 cannons on a board of pure husks. Neither fix is sufficient; I found
the second only because the end-to-end test failed after the first.

**A mechanic can be present in the content and absent from the game.** The crossover where burst beats
rapid fire is at average `fussiness` **4**, weighted by appetite. Every shipped wave table peaks at
**1.53**. `husk` is 16.5% of wave 12 and would need ~48%. The arrow station is 22.5% better value even
on the most armoured wave in the repo — so the husk's `_asks` field ("do you have burst?") was claiming
something twelve maps do not deliver, and no measurement could have caught it because nothing printed
which stations were bought.

**Fussiness has a ceiling, and it is lower than it looks.** It subtracts from *every* station's hit.
Once it reaches `serving - 1` of the cheapest station — 11, here — that station is floored at 1 and
cannot get worse, while the burst station keeps losing damage. **Past 11, more armour makes burst a
worse answer, not a better one.** So "just make husks fussier" is not a lever at all: at 19 husks in
wave 12, no value of `fussiness` flips the choice.

**Weight by appetite, not by head count.** A husk is 120 and a runner 60, so half the appetite is a
third of the bodies. "One visitor in five is a husk" sounds like plenty and is off by a factor of two.

The method worth repeating: publish the pre-change binary to a scratch directory and sweep the baseline
**before editing a line**. The committed 08-08 report had drifted from the tree (`spiral` 25.3% →
26.7%), so "compare against the last report" would have invented a delta the change did not cause. One
trap in doing that — `ContentFiles.FindRepoRoot` walks up from the *binary*, so a published snapshot
needs `content-data` symlinked next to it or it cannot find the game.

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
