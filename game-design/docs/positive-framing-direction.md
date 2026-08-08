# Direction — A Tower Defense With No Towers, No Enemies, and No Defense

**Set:** 2026-08-08 · **Owner:** design-lead · **Status:** proposed, not decided

Brief: keep the genre, drop the military. Kid-friendly, still interesting to adults.

## The good news, and it is bigger than it looks

**Not one of the five pillars mentions combat.**

> 1. The maze is the game · 2. Legible at a glance · 3. Deterministic, therefore fair ·
> 4. Every loss is explainable · 5. Small numbers, big decisions

Read them again with the theme stripped out and nothing breaks. "The maze is the game" is about
*reshaping a route*. "Every loss is explainable" is about *causality*. None of it needs a war.

The genre's actual mechanics are also neutral:

- things travel a path toward a place you care about
- you place static things that act on them as they pass
- acting on them yields the resource you place more with
- **your placements change the route** — the one that makes it Gridfall

That is a description of a **sorting yard, an irrigation network, a pinball table, or a garden**. The
military reading is entirely in the nouns.

## What is actually military: a vocabulary audit

| Identifier | Count in Core | Verdict |
|---|---|---|
| `Tower` | 228 | Rename. Mechanically neutral — a static thing on a cell |
| `Damage` | 63 | Rename. It is "progress toward resolving" |
| `Lives` | 30 | Rename. Neutral-ish already |
| `Enemy` | 29 | Rename |
| `Attack` | 29 | Rename |
| `Died` / `Death` / `Destroyed` | 8 | **Reframe, not rename** — see below |
| `Armour` | 3 | Rename |

Content ids: `arrow-tower`, `cannon`, and creeps named `brute`, `husk`, `mite`, `runner`, `sapper`.
`cannon` and `sapper` are the only two that are *inherently* weapons; the rest are just words.

**So ~95% of this is a rename**, and a rename is cheap here because the simulation does not care what
things are called. `CellKind`, the flow field, the tick order — none of it changes.

## The 5% that is not a rename

Four things are violent in *behaviour*, not just in name. Renaming them would be a costume, and
children's media is unusually good at detecting a costume.

| Now | Problem | Reframe |
|---|---|---|
| Creeps **die** | A thing that dies is dead whatever you call it | They **resolve** — sorted, absorbed, popped, swept, delivered. It reaches an end state that is *good* |
| Towers take **damage** and are **destroyed** | Your things being wrecked | They **wear out / clog / need winding**. Repair already exists and fits this perfectly |
| `sapper` **attacks structures** | An enemy that targets you | It **overloads** the thing it reaches — same mechanic, no aggression |
| **Lives** lost when something reaches the goal | Losing *lives* | A **mess / flood / backlog** builds. Reaching zero is "the place is overwhelmed", not death |

Every one is a presentation and naming change over identical simulation code. `LivesCost` becomes
`MessCost`; the number and the tick order are untouched.

> **The proof this works commercially is Bloons TD.** Monkeys pop balloons. Nothing dies, nothing
> bleeds, there is no army — and it is one of the most durable, most profitable properties in the
> genre, with a large adult playerbase. Non-violence is not a handicap in tower defense. It is the
> pattern the biggest kid-facing successes share.

## Three directions

Weighted toward what this project has already shown it cares about — the request for wave shapes
"like fireworks, nature or the golden ratio", the "backstage opera" framing for authoring cadence, the
SimCity comparison, and boards that read as *places*.

### A. Waterworks — recommended for marketability

Rain and meltwater run downhill toward the village. You place **stones, reeds, cisterns and mills**
that soak, slow and divert. Water that reaches the village floods it.

- **Mazing is literal.** Redirecting flow is the most intuitive possible reading of "your placements
  change the route" — a child understands damming a stream without being taught.
- Nothing dies; water is absorbed. Bounty is water collected.
- Adults: a beautiful systems/zen game. Kids: rocks and streams.
- Fits the existing board themes almost unchanged — mountain, forest, desert, ocean already read as
  watersheds.

### B. The music box — recommended for adults, and for *this* designer

Marbles roll a track toward the edge. You place **bells, pins, chimes and paddles** that catch and
redirect them. Each catch plays a note; a wave is a phrase; the last wave is a **finale**.

- This makes the `wave-shape` work *native* rather than bolted on. "Pauses of anticipation" stop
  being a design goal and become the medium — a rest is a rest.
- Wintergatan's marble machine is proof of the adult appetite; marble runs are proof of the child one.
- Risk: the audio bar is suddenly high. A music game with placeholder sound is not testable the way a
  visual placeholder is, and this repo's whole discipline depends on placeholders being cheap.

### C. The sorting yard

Parcels ride belts toward the loading bay. You place **chutes, scanners and stampers**. Unsorted
parcels pile up.

- Cleanest fit for "resolve, don't destroy" and for an economy.
- Weakest fit for *mazing* — belts imply fixed routes, and pillar 1 is the whole game.
- Listed because it is the obvious one, and to say plainly why it is third.

## Recommendation

**A, with B's vocabulary of rhythm.** Waterworks is the more marketable and the easier to prototype;
the musical framing of *wave cadence* can be layered on as sound design without betting the game on
audio.

## What this actually costs

| Layer | Work |
|---|---|
| `Gridfall.Core` | **Rename only.** No logic, no tick order, no determinism impact. Traces re-record because ids change, not because behaviour does |
| `content-data` | New ids and names. Numbers unchanged — the balance work survives entirely |
| `presentation` | The real cost. New palette, new placeholder silhouettes, and every Ludo prompt rewritten |
| `docs` | Wide but shallow. "Tower" appears in most workspace docs |

**The balance work survives.** Ranges, curves, `hpGrowth 1.10 from 6`, the 15–30% band, the coverage
metric — all of it is numbers about a route and things beside it, and none of it knows what the things
are called.

## What I am not claiming

I cannot make this profitable, and nothing here is market research. What it does is remove the
blockers that would keep it off a kids' storefront, and point at the pattern that the successful
kid-facing games in this genre share. Pricing, platform and audience are decisions with no engineering
answer.

**Also unresolved:** whether to rename `Gridfall`. It is a good name and it is not military — "fall"
reads as *falling*, which suits water. Probably keep it.

## If this is agreed, the first slice

Rename in **one pass, mechanically, with the tests green** — `Tower → Emitter`/whatever the theme
picks, `Enemy → Drift`, `Damage → Progress`, `Lives → Capacity`. Nothing else. Doing it in one commit
keeps the diff reviewable; drip-feeding it leaves the codebase bilingual for months.

The four behavioural reframes come after, and each is small.
