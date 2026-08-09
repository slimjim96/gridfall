# Theme — Deliberately Open

**Owner:** the human · **Status:** **OPEN, on purpose** · Last touched 2026-08-09

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

## Candidates, unranked

Kept short on purpose. None is vetted; none needs to be until someone wants to build art.

| Theme | The joke | Best thing about it |
|---|---|---|
| **Fulfilment / feeding** *(the current skin)* | none — it is warm rather than funny | Already in the code. Risk: the loop reads as force-feeding, which is why it is under review |
| **The Wash** | grubby things go in, gleaming things come out | The visitor **is** its own progress bar — you watch the dirt go |
| **Please Hold** | a queue of magnificently irate citizens at Window 4 | Tier 2 is native: the form *has* the sum on it |
| **Nothing to Declare** | alien customs, illegal luggage | The most toyetic silhouettes; suits the name `Gridfall` |
| **Bin Night** | urban wildlife raids the street, Tuesdays | The best characters — this is the one with plushies |

The framing worth keeping whichever wins: **you defeat them by helping them.** It removes the
militarism without removing the conflict, which is the trick PvZ pulls and the reason it reads as
funny rather than gentle.

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
