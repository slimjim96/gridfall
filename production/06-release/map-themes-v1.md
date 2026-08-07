# Map Themes — v1

**Slug:** `map-themes` · **Status:** done · **Verified at trace:** unchanged

## What Shipped

A map declares a ground palette and the view draws it.

- **`theme` on the map format**, defaulting to `slate` — the palette the game shipped with, so a map
  written before themes existed looks exactly as it did.
- **Seven themes**: `slate`, `forest`, `desert`, `ocean`, `underwater`, `mountain`, `space`.
- **`F4` in the board editor** cycles the palette and marks the draft dirty. The editor spec's v1
  exclusion of theming is lifted, narrowly.
- **`--theme <id>`** on the capture path, so any theme can be shot on a loaded board.
- crossroads ships `slate`; gauntlet ships `mountain`.

**Nothing about the simulation changed.** There is a test asserting two maps identical but for their
theme hash the same at every tick — if that ever fails, a presentational field has leaked into the sim
and every recorded trace has become theme-dependent.

## The shape of a theme

**Three colours, not five.** Blocked, path-only, buildable.

**Spawn and goal keep the same hue on every board.** They are functional markers, not terrain: a player
learns "purple is where they come from, green is what I am defending" once, and a theme that moved them
would make that knowledge worthless. Same reasoning as the one-red rule.

## What the captures taught

Two of the seven were wrong on the first attempt, and neither was predictable from the hex. Both were
caught by the rule the palette already carried — *judge terrain contrast from a screenshot, never from
the hex values* — applied with units on the board.

**`desert` was an ochre.** It cleared the tower slot comfortably, which is the constraint I had written
down, and still failed: it landed in the **brute's** khaki band, and a capture showed khaki cubes
camouflaged on the ground. Now a clay, rotated toward red and away from the khaki/husk band.

**`underwater` was a teal.** It swallowed the **goal marker** outright — a functional marker vanishing
is worse than a creep losing contrast. Now ocean's hue taken much darker, separated from `ocean` by
value rather than hue, which is also the physically honest version of depth.

That produced a rule that was not in `art-direction.md` and now is:

> A theme's ramp must clear the hue band of **every unit and both functional markers** — not just the
> towers. The roster already owns most of the warm spectrum (khaki brute, orange-brown husk, two orange
> towers, one red), which is *why* the original rule says terrain is cool, and why `desert` is the
> tightest theme in the set.

## Design notes

**Core holds no list of valid themes.** It carries the string and never reads it, so nothing below the
boundary knows a colour exists. The cost is that a typo reaches the renderer, where it falls back to
`slate` — a board in the wrong palette beats a map that will not open. The typo is caught instead by a
test that reads the registry out of the view's source rather than duplicating the list, following
`SourcePurityTests`.

**Editor scope, lifted narrowly.** v1 excluded theming because "the grid is flat and the art is
procedural". The second half stopped being true. Picking one of the shipped ramps is choosing what the
map *declares*, not authoring art — terrain height and per-cell decoration are still out.

## A miss in the previous slice, found here

`early-economy-2` changed the crossroads wave table and **did not refresh the two visual baselines that
depend on it**. Its verify report recorded "Visual capture: n/a — no visual claim; no renderer change",
which was true about the renderer and wrong about the baselines: the `sappers` and `repair` seeds play
to wave 5+, so different creep HP renders a different frame. Their sim hash had moved from
`a15d4919788939c8` to `3efe266df68d3e3a`.

Caught only because this slice re-captured everything to prove `slate` was unchanged — `board-baseline`
(one wave, scale 1.0 under both curves) was byte-identical while the two long seeds were not.

Both refreshed here, byte-reproducible.

> **A content change can invalidate a visual baseline with no renderer change at all.** "No renderer
> change" is not a reason to skip the capture gate; "no seed reaches the changed content" is.

## Player-Facing Change

Boards read as places. gauntlet is stone; crossroads is unchanged.

## Follow-Ups Not Done

| Item | Workspace | Slug |
|---|---|---|
| The editing components themselves — fill, rect, copy/paste, symmetry. Waiting on which are actually painful | tooling | `board-editor-2` |
| Ground *images* rather than flat colours: UVs, an atlas, a theme→atlas mapping | presentation | `tile-art-pipeline` |
| Ludo prompts for the first theme's finals, once one theme works end to end in placeholders | presentation | `ludo-tile-prompts` |
| Grow the tower roster toward ~8 with per-theme availability | content-data | `tower-pool` |
| Theme-aware unit palettes — a theme currently constrains the roster's hue space and the roster wins | presentation | `themed-unit-palettes` |

## Known Not Verified

- `forest`, `ocean`, `mountain`, `space` and `slate` were each checked on a loaded board; `desert` and
  `underwater` were checked, fixed, and re-checked. None has been seen at peak wave density on a map
  other than crossroads.
- Whether seven themes is the right number, or whether some collapse into each other in play. `ocean`
  and `underwater` are the pair most at risk, now separated by value rather than hue.
