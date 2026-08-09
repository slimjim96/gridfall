# Rivers and Bridges — v1

**Slug:** `river-bridges` · **Status:** done · **Date:** 2026-08-09
**Verified at trace:** `crossroads-baseline`, unchanged · **Tests:** 234 → 244

## What Shipped

Boards have water in them, and roads that cross it.

- **`CellSurface`** — `Ground`, `Water`, `Span`. A second view-only layer beside `heights`, authored as
  glyph rows (`.` `~` `=`) parallel to `cells`.
- **`MapSurfaces`** — the glyphs, the legality rule and its refusal text, in **one place**, because the
  loader, the editor and `MapValidator` all need the same answer.
- **`MapValidator` refuses water on a walkable cell** and a span on an unwalkable one. An **error**,
  not a warning.
- **Renderer**: water sinks instead of taking the wall raise, a span decks slightly above the ground it
  continues, and a surfaced cell takes no terrain tile.
- **Colours are derived from the theme, never authored** — "a theme is three colours, not five".
- **Generator**: one straight line across the board; wall cells on it become water, walkable cells
  become deck. The channel is carved to one level below its lowest bank.
- Ten tests, including the one that matters: same board with and without a river, identical hash.

## The bargain, and why it is enforced rather than promised

Elevation shipped view-only on a promise: Core does not read `Heights`, and a code review is what keeps
that true. Surfaces cannot work that way, because a surface makes a **claim about the rules** — water
says "nothing walks here". A layer that could claim it falsely would produce a board that looks like it
has a river, plays like it does not, and validates either way.

So water is legal only where the pathfinder already refuses to go. Visitors do not walk on water
because the cell was already `Blocked` — the simulation never learns the river exists. That single rule
is what makes the whole layer free.

## The result

**Five boards gained rivers and not one number moved.**

| Board | Axis | Water | Bridges |
|---|---|---|---|
| `meander` | east–west *(fallback)* | 11 | 2 |
| `chambers` | north–south | 11 | 1 |
| `switchback` | east–west | 11 | 1 |
| `braid` | east–west *(fallback)* | 12 | 3 |
| `stepwell` | east–west | 10 | 3 |
| `driftway` | — | — | — |

Geometry compared against `HEAD` cell by cell on all ten generated boards: identical. Balance re-run on
all six affected boards, 150 runs each: byte-identical reports.

`driftway` wanted a river and no legal line exists on either axis. It is reported as `NONE FIT` in the
generator's output rather than quietly dropped — a cosmetic layer must never block a map from being
written, and a silent downgrade is how a set loses three rivers nobody meant to remove.

## Two things that were wrong first

**Water stood proud of its own banks.** Water is `Blocked` terrain and was taking the +0.28 wall raise,
so the first river rendered as a raised blue wall. It drops now — but the *depth* comes from the height
field carving the channel, not from the drop, which is a hairline.

**The bridge warning fired on every bridge worth having.** "Does this span cell touch water?" warns
about the middle of any bridge three or more cells long. It floods the connected run and asks the
question once per bridge.

## What is deliberately not here

- **Rivers do not affect pathing.** Water as a real cell kind, with bridges as the only crossing and
  therefore chokepoints worth defending, is a different and more interesting game. It needs an ADR, a
  validator change and a trace re-record. Not smuggled in under a cosmetic slice.
- **The editor cannot paint surfaces.** It carries them through open-and-save — there is a test — but a
  brush is a spec change and `board-editor` v1 is closed.
- **No authored water or bridge tiles.** Derived flat colour until `ludo-tile-prompts`.

## Records

- [Verification](../05-verify/river-bridges-report.md) — including the four things a human must look at
- `docs/iso-grid.md` §Surfaces — the drop, the lift, and why depth is not the drop's job
- `docs/engine-guide/07-content-loading.md` §The map format — the two optional layers
- `content-data/docs/example-levels.md` §Five of them have rivers — which boards, and why four are dry
