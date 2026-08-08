# Wave Shape — Requirements

**Slug:** `wave-shape` · **Status:** backlog · **Owner:** design-lead

## In One Sentence

A wave should have a shape — build, lull, finale — instead of dumping most of itself in the first few
seconds and trickling out, which is what every wave in the game currently does.

## The measurement that makes this urgent

Spawns per quarter of each wave, `crossroads`, derived from the authored table:

| Wave | Length (ticks) | Q1 | Q2 | Q3 | Q4 | |
|---|---|---|---|---|---|---|
| 1 | 126 | 2 | 2 | 2 | 2 | even |
| 4 | 240 | 10 | 13 | 7 | 5 | even |
| 7 | 363 | **29** | 23 | 12 | **5** | front-loaded |
| 10 | 515 | **55** | 30 | 13 | **12** | front-loaded |
| 12 | 785 | **92** | 27 | 20 | **8** | front-loaded |

**Wave 12 fires 92 spawns in its first quarter and 8 in its last — an 11:1 diminuendo.** The climax of
the game structurally fades out, and it gets *worse* as waves grow.

**Nobody authored this.** It is emergent: every entry starts at roughly the same moment and spawns at
a constant rate, so the dense fast streams (mites at `spacingTicks: 3`) exhaust in the opening seconds
while the slow ones (sappers at 45) dribble for another twenty. The shape is a side effect of the
spacing numbers, which were tuned for composition, not for rhythm.

So the request is not "add a nice-to-have". It is: **the last thing the player experiences in every
wave is the weakest part of it.**

## The format is not the blocker

`SimState.MaxWaveEntries` is **16**. The busiest wave uses **5**. Eleven free slots per wave, and
`delayTicks` already exists per entry — so a build, a lull and a finale burst are *expressible today*
by splitting one enemy across several entries with different delays.

Nothing needs inventing. What is missing is the **concept**: nobody has named wave shape, so nobody
has authored one, and nothing reports when a wave has none.

That makes this cheaper than it looks, and it means the first version can be pure content plus a
report — no simulation change at all.

## Shape is a difficulty lever, and this is now measured

The `wave-variance` slice measured what happens when start offsets shift: concentrated pressure is
harder than spread pressure, because **overlap hurts more than a gap helps**. A finale is deliberate
overlap.

So re-shaping a wave changes its difficulty even at identical composition and count. Every shape
change needs a balance run beside it — and at **150 runs minimum**, since the same slice showed a
30-run baseline swinging four points on sample size alone.

## What a shape is

| Shape | Rhythm | Use |
|---|---|---|
| `steady` | Constant rate | Today's intent; correct for teaching waves |
| `crescendo` | Rate tightens toward the end | The default most waves probably want |
| `pulse` | Bursts separated by lulls | Gives the player windows to re-maze |
| `finale` | Build → **pause** → everything at once | The one being asked for. Rare, so it lands |

**The pause is the feature, not the burst.** A finale with no lull before it is just a busy wave. The
anticipation beat is what makes the burst read as a burst — which is the whole point of the fireworks
comparison.

## The session arc, not just the wave arc

"Most of the time having a finale" implies waves are not all alike. Today the only thing that changes
across the twelve is HP scale and count — every wave has the same (bad) shape, just bigger.

A session wants a mix: ordinary waves, a couple of pulse waves that create build windows, and a small
number of genuine finales. Which waves get which is a design decision, not a generated one.

## One beat already exists

The gap **between** waves is player-controlled — the next wave starts when the player presses space.
That is already an anticipation pause, and it is already well-placed. Nothing here should take it
away; the missing rhythm is *inside* a wave.

## Acceptance Criteria

- [ ] A wave can declare a shape, or keep its authored entries verbatim
- [ ] `Verify` reports **spawns per quarter** for every wave, so a diminuendo is visible without
      hand-computing it
- [ ] No wave past the tutorial waves is front-loaded worse than ~2:1 unless deliberately marked
- [ ] Wave 12 ends on its heaviest quarter, not its lightest
- [ ] Every shape change carries a 150-run balance measurement
- [ ] Existing tables that declare no shape are byte-identical in behaviour

## Open Questions

1. **Authored or generated?** A `shape` field that rewrites timings at load is the least code and
   keeps the sim untouched — but hand-authored entries give exact control. Probably: generate the
   common shapes, allow explicit entries to override.
2. **How much harder is a finale?** Unknown, and it must be measured before shipping one. My estimate
   is that it is a larger effect than `waveVariance`'s +2.7pp, because a finale concentrates
   deliberately rather than by accident.
3. **Does the player see it coming?** Third time this has come up. A finale the player cannot
   anticipate is an unexplainable loss (pillar 4); a finale they *can* see is the best moment in the
   game. **The preview is probably a prerequisite for this slice, not a companion to it.**

## Suggested First Slice

**Report before authoring.** Add spawns-per-quarter to `Verify maps` or a new `waves` command, so the
diminuendo is visible and any re-shaping can be checked rather than eyeballed. That is small, needs no
balance run, and makes the actual re-shaping measurable.

Then re-shape **wave 12 only** — one wave, one 150-run measurement, one clear before/after — before
touching the other eleven.
