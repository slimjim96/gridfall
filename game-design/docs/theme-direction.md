# Theme — Deliberately Open

**Owner:** the human · **Status:** **OPEN, and being revisited** · Last touched 2026-08-09

> **2026-08-09 — reopened for better candidates, and three of the five old ones are now disqualified.**
> The reason is not taste. It is that `board-themes-direction.md` committed to boards being *places* —
> mountain, ocean, forest, desert — elevation shipped, and rivers and bridges are next. **A theme now
> has to host a landscape.** An office queue and a wheelie-bin street cannot. See §Candidates.

The single entry point for anything about what this game *is about*. The mechanic is settled and is
not waiting on this; the skin is undecided and is not blocking anything either. Both of those are
deliberate, and this file exists so the next person does not have to reconstruct that from five
scattered docs and a commit log.

**Do not open a theme discussion without reading §"What a theme change actually costs".** It is
cheaper than it looks, which is the reason it can safely stay open.

---

## The loop, with no theme on it

> A **visitor** arrives carrying a **need**. **Stations** beside the path reduce it. A need reduced to
> zero **resolves** and leaves. A need that reaches the end **unresolved** costs **patience**.

Everything else is dressing. Feeding, washing, mending, watering, stamping paperwork and shooting
arrows are all the same five nouns in different hats — which is exactly why the theme can wait.

## What is theme-locked, and what is not

This is the useful part. After the 2026-08-09 rename, the codebase's vocabulary is **mostly neutral
already**:

| Identifier | Theme-locked? | Reads fine as… |
|---|---|---|
| `Station` | **no** | a stall, a desk, a sprinkler, a workbench, a turret |
| `Visitor` | **no** | anything that arrives |
| `Patience` | **no** | any depleting tolerance |
| `Fussiness` | **no** | resistance to each application — a stain, a stubborn form, armour |
| `Stock` / `Depleted` / `Restock` | **no** | any station consumable |
| **`Appetite`** | **YES — feeding** | the amount of need a visitor carries |
| **`Serving`** | **YES — feeding** | one application of a station's effect |

**Two words out of seven carry the theme.** Swapping them to something neutral — `Need` and
`Service` are the obvious pair, and `Need` is already the word this document's loop uses — would make
the codebase theme-free and cost a single mechanical pass. That has **not** been done: it is a real
option, not a plan, and it is recorded here so it stays a five-minute decision instead of an
archaeology exercise.

## What a theme change actually costs

Measured, not estimated — the Tower→Station rename on 2026-08-09 is the evidence:

- **67 C# files, every content JSON, both content directories, one pass.**
- **Behaviour did not move.** `replay` passed untouched, `comb` still measured 42.0% digit for digit.
  A rename cannot change the simulation, and now there is a trace proving it.
- **The traps are known and written down**, which is why a second pass would be faster than the
  first: a mapping that reads as nonsense on one side of the boundary needs splitting (`Damaged` is
  not `Servinged`; a station has no appetite, it has stock), and the silent failure mode is a
  generator still emitting a field the loader no longer reads.

**So the theme is not a one-way door and never was.** Pick late, pick on taste.

## What a candidate now has to clear

The old list was written when a theme only had to skin five nouns. Three things have shipped or been
committed to since, and together they are a filter:

| Requirement | Where it came from | What it kills |
|---|---|---|
| **Hosts a landscape** — mountain, ocean, forest, desert, and now rivers, bridges and height | `board-themes-direction.md`; elevation shipped 2026-08-09 | Any theme set in one room, one street or one building |
| **Supports ~10 station roles** on one advanced board | `station-pool`, opened 2026-08-09 | Any theme with three plausible props in it |
| **The need is visible on the visitor** | Pillar 2 — silhouette carries identity, colour carries state | Any theme where the need is a number, not a look |
| **Resistance reads as something** — `fussiness` is per-application stubbornness | The mechanic exists and is inert today | Themes where "hard to please" has no natural picture |

The framing worth keeping whichever wins: **you defeat them by helping them.** It removes the
militarism without removing the conflict, which is the trick PvZ pulls and the reason it reads as
funny rather than gentle.

## The twist breaks that framing, and it is the sharpest filter yet

**2026-08-09 — the game will ship both directions** ([`inverted-mode`](../../production/01-requirements/inverted-mode-requirements.md)).
In the inverted mode you spend a budget sending visitors and score the ones that arrive, while the
game builds the stations trying to stop you.

That is a problem for *"you defeat them by helping them"*, and the problem is not cosmetic:

> If a station **helps** the visitor, then in the inverted mode the player is a visitor who is trying
> to **avoid being helped** — and the opposition stops making sense. A pilgrim dodging the well. A
> traveller running past the free soup.

So a theme now has to answer one more question: **is being stopped by a station something the visitor
would object to?** The warm framing survives in normal mode either way. It is the inverted mode that
needs the station to be genuine opposition.

| Candidate | Reads from the defender's side | Reads from the attacker's side | |
|---|---|---|---|
| **Nothing to Declare** | You are customs; you process arrivals | **You are the smuggler.** Getting through is the entire fantasy | **the only candidate that is native in both directions** |
| **Fulfilment / feeding** | You feed the crowd | You are the crowd getting past the feeders — odd, but legible as a queue you are trying to skip | workable |
| **The Long Road** | You provision the pilgrims | A pilgrim avoiding waystations is a pilgrim with no reason to stop | **incoherent under inversion** |
| **The Crossing** | You water the herd | Same problem: the animal wants the water | **incoherent under inversion** |
| **The Quiet Road** | You ease the spirits on | A spirit fleeing the shrines that would settle it is a *different and darker game* | inverts, but into something else |
| **Basecamp** | You supply the climbers | You are the mountain? | **incoherent under inversion** |

**This is the strongest single argument on this page, and it points at `Nothing to Declare`** — which
was already one of the two survivors of the landscape filter, already the best fit for the name, and
already the most toyetic silhouettes. Three independent filters, one answer.

The counter-argument worth stating: *"you defeat them by helping them"* is the reason this game does
not read as militaristic, and a customs theme is opposition on both sides. It buys coherence with
warmth. That is a taste call and it is the human's.

## Candidates — the old five, filtered

| Theme | Hosts a landscape? | Verdict |
|---|---|---|
| **Fulfilment / feeding** *(the current skin)* | weakly — a table is not a valley | **Alive, unloved.** Already in the code. The force-feeding read is still the risk |
| **Nothing to Declare** | **yes** — a border is a place, and borders have rivers | **Alive.** Still the most toyetic silhouettes and still the best fit for the name |
| **The Wash** | no — it is a room | **Cut.** Best progress bar in the set, and nowhere to put a mountain |
| **Please Hold** | no — it is an office | **Cut.** Tier 2's soft-lock was native here, which is a real loss |
| **Bin Night** | no — it is a street | **Cut.** Best characters in the set |

**Losing the three good ones to a single constraint is the signal.** The board direction and the theme
list were written two days apart and had never been checked against each other.

## Candidates — new, built to the filter

Unranked, unvetted. Each is here because the landscape is not a backdrop but *the reason the visitors
are walking*.

| Theme | The premise | Need reads as | Fussiness reads as | Best thing about it |
|---|---|---|---|---|
| **The Long Road** | Pilgrims cross a country to a shrine. You build the waystations — wells, kitchens, cobblers, bathhouses, ferries | Footsore, thirsty, blistered; wear that visibly comes off | The traveller who will not stop for just anything | **Ten stations write themselves.** Every terrain is a leg of the road, and a bridge is a *place*, not scenery |
| **Basecamp** | Climbers ascend. You place camps, caches, ladders, fixed ropes, rope bridges | Cold, spent, thin-aired | The one who insists on doing it their own way | **Elevation stops being decoration.** Height is the subject, which turns §2 of `next-steps` into a theme beat rather than a stat |
| **The Crossing** | A herd migrates through a valley. You place water, salt, shade, ramps, shallows | Thirst and heat, on the animal | The stubborn old bull who ignores the good water | Wordless and ageless, and rivers and bridges are the *whole drama* of a migration |
| **The Quiet Road** | Spirits walk on to rest. You set the lanterns, shrines, offerings, ferrymen | Unfinished business, worn visibly lighter | The one who is not ready to go | **"You defeat them by helping them" is literal.** A river you must ferry them across is the oldest image there is |

### How to read the four

- **The Long Road** is the safe one: the widest station vocabulary and the least tonal risk.
- **Basecamp** fits the *engine* tightest — one axis, up, which is exactly the axis the renderer just
  learned. Also the narrowest: a mountain is the only board it can host.
- **The Crossing** is the prettiest and the least verbal. It is also the hardest to make funny.
- **The Quiet Road** has the best premise and the most tonal risk. A warm idea about death is either
  the whole charm or the thing nobody wants to explain.

**None is picked, and nothing is blocked on picking.** The terrain work (rivers, bridges, height) is
theme-free by construction, and the station roster is specified by **role**, never by flavour, exactly
so it can be built before this closes.

## Where the rest of it lives

Nothing below is duplicated here — go to the source:

| Thing | File |
|---|---|
| The look: palette, silhouette rules, motion, audio hooks | [`presentation/docs/art-direction.md`](../../presentation/docs/art-direction.md) |
| Board/terrain themes (mountain, ocean, forest, …) | [`board-themes-direction.md`](board-themes-direction.md) |
| How to write an asset prompt | [`presentation/docs/ludo-prompt-guide.md`](../../presentation/docs/ludo-prompt-guide.md) |
| The prompts themselves + the style anchor | [`presentation/prompts/README.md`](../../presentation/prompts/README.md) |
| Placeholder standard (what art must beat) | [`presentation/docs/placeholder-standard.md`](../../presentation/docs/placeholder-standard.md) |
| The accepted reframe that produced the current vocabulary | [`fulfilment-direction.md`](fulfilment-direction.md) |
| Tier 2's open design question | [`tier2-soft-lock-options.md`](tier2-soft-lock-options.md) |

## What not to do while this is open

- **Do not run a Ludo.ai batch on theme-specific units.** Terrain tiles and generic props are safe —
  a mountain is a mountain under every candidate. A *station* is a turret, a stall or a service desk
  depending on the answer, and that is the expensive thing to regenerate.
- **Do not bake the theme into the style anchor.** It said *"Isometric tower defense game asset"* until
  2026-08-09, and that block is copied **verbatim** into every prompt — so one stale phrase would have
  themed every asset ever generated, silently. It now describes the look and not the genre.
- **Do not re-theme the code speculatively.** One rename is a pass; three is churn, and each one moves
  every baseline.
- **Do not name a new station after a prop.** `station-pool` specifies ten stations by **role** —
  rapid, burst, lobber, slower, anchor — for this reason. A roster half-named after ferries and half
  after catapults is a theme decision taken by accident, in ten files, by whoever wrote them.
