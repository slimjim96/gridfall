# Direction — Fulfilment, Not Defense

> **The vocabulary here shipped; the *theme* is open again.** This doc is why the code says `Station`
> and `Visitor`. Whether the game is about feeding is being reconsidered — see
> [theme-direction.md](theme-direction.md), which is the entry point now.

**Set:** 2026-08-08 · **Owner:** design-lead · **Status:** ACCEPTED 2026-08-09 · Supersedes the three options in
[positive-framing-direction.md](positive-framing-direction.md), which asked the right question and
answered it less well.

## The three ideas are one idea

Feeding animals that run off once fed. Bubbles that pop and float away when fulfilled. Math and words.

Those are not alternatives — they are **the same mechanic at three content depths**:

> A traveller arrives carrying a **need**. Stations beside the path **fill** it. A filled need
> **leaves happily**. A need that reaches the end **unmet** is the failure.

That single sentence replaces every military noun in the game, and it is a *better* fit for the
existing simulation than combat was, because nothing has to be destroyed for the loop to close.

## The vocabulary, mapped

| Now | Becomes | Note |
|---|---|---|
| Tower | **Station** | Static, on a cell, acts on what passes |
| Enemy / creep | **Visitor** | Arrives wanting something |
| HP | **Appetite** | How much it needs |
| Damage | **A serving** | How much need one shot fills |
| Kill | **Fulfilled** | It pops, or runs off happy |
| Leak | **Left still wanting** | Sad, not destructive |
| Lives | **Patience** (or supplies) | Runs out when too many leave unmet |
| Armour | **Fussiness** | How much of each serving does not count |
| Splash | **Serving a table** | Several at once |
| Repair | **Restocking** | Already exists, already fits |
| Sapper | **A visitor that empties a station** | Same mechanic, no aggression |

One detail worth keeping. `DamageSystem` floors every hit at 1 with this comment:

> *"an enemy immune to a tower is a soft-lock waiting to happen, and 'my towers do nothing' is not a
> readable failure."*

Reframed, that rule becomes **"no visitor is impossible to please."** Nicer, truer to the theme, and
the identical line of code.

## Two tiers, and only one of them costs anything

### Tier 1 — Appetite is a number

Appetite is a quantity; servings are quantities; fussiness subtracts. **This is the current simulation,
unchanged.** Not "mostly unchanged" — the same arithmetic, the same tick order, the same everything.

Every balance number survives intact: `hpGrowth 1.10 from wave 6` becomes "appetites grow from wave
6", the 15–30% band still means what it meant, ranges and the coverage metric are untouched.

Cost: a rename pass, a palette, and new placeholder silhouettes.

**Bubbles are close to free.** Placeholders already share a *death collapse* — scale to zero over
150 ms. That motion, recoloured and floated upward, **is a pop.** The one animation the theme needs
already exists and only wants renaming.

### Tier 2 — Appetite is a question

A visitor needs `3 + 4`; a station serves `7`. This is the educational product, and it is a real
mechanical change: an answer value on the station, a need on the visitor, and a match test inside
targeting and damage. Small in code, large in consequence.

**It changes what the maze is for, and improves it.** Today you maze to lengthen exposure. With
matching you maze to **route each visitor past the station that can answer it** — different visitors
want different routes through the same board. Pillar 1 gets stronger, not weaker.

**The unsolved problem is the one the floor-at-1 comment already warned about.** If a station cannot
answer, it does nothing, and "my stations do nothing" is exactly the unreadable failure that rule
exists to prevent. Options, none free:

- a wrong station still gives partial progress — you can always brute-force, matching is just faster
- the wave only ever asks questions your current stations can answer
- an unanswered visitor slows instead of passing, buying time to build the right station

That question has to be settled before Tier 2 is scheduled. It is the whole design.

## Recommendation

**Ship Tier 1. Design Tier 2 in parallel, schedule it after.**

Tier 1 is a rename and an art pass over a game that is already balanced — the cheapest possible route
to something marketable to children, and it stands on its own as a product. Tier 2 is the
differentiator and the reason a school or a parent pays, but it needs the soft-lock question answered
and it should not hold Tier 1 hostage.

Content-wise, the two tiers share an engine: **animals for younger players, questions for older ones,
same board, same code.** That is one build serving two audiences, which is the strongest argument here
and the reason to prefer this over the waterworks idea.

## What I would call things

Working names only, and the naming is genuinely the human's call:

- **Visitors** — soft, age-neutral, and true of bubbles, animals and questions alike
- **Stations** — reads as helpful; "feeder", "stall", "kitchen" all work per theme
- **Patience** rather than lives — running out is "the picnic gave up", not death

`Gridfall` still works. "Fall" reads as drifting down, which is what bubbles and leaves do.

## First slice — DONE 2026-08-09

The reframe and the names were accepted, and the mechanical rename landed in one pass. Two things the
spec below did not anticipate:

- **Traces did not need re-recording.** The note said they would "because content ids change". They
  did change — `arrow-tower` is `arrow-station` — but the state hash covers integers, and def indices
  are assigned by ordinal filename sort, where `arrow-station.json` sorts before `cannon.json` exactly
  as `arrow-tower.json` did. `replay` passed untouched, which is a stronger result: **nothing about
  behaviour moved at all.**
- **Two mappings produce nonsense and were extended.** `Damaged → Servinged` is not a word, and a
  station has no *appetite*. Stations hold **stock** and become **depleted**; visitors have
  **appetite** and receive **servings**. That follows the doc's own `Repair → Restocking` line rather
  than inventing a vocabulary.

## The slice as specified

The mechanical rename in **one pass, tests green**: `Tower → Station`, `Enemy → Visitor`,
`Damage → Serving`, `Hp → Appetite`, `Lives → Patience`, `Armour → Fussiness`. Nothing else, one
commit — a half-renamed codebase is bilingual for months.

Traces re-record because content ids change, not because behaviour does. Nothing in
`content-data/docs/balance-targets.md` needs re-measuring, and that should be stated in the release
note so nobody re-runs a balance pass out of caution.
