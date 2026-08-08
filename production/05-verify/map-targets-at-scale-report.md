# Map Targets At Scale — Verification

**Slug:** `map-targets-at-scale` · **Status:** done · **Verdict:** PASS

## What the investigation actually found

The follow-up said "`MapTargets` bands are size-absolute and wrong at scale", citing a 64×64 board
reporting 89% buildable and path 63. **Half of that was wrong, and the slice is smaller than
advertised.**

| Target | Size-absolute? | Verdict |
|---|---|---|
| Unmazed path 18–30 | **Yes** | The real defect |
| Buildable 35–55% | No — it is a percentage | The 89% reading was a genuinely open test board, not a false positive |
| Maze ≤ 3× | No — a ratio | Fine |
| Lanes 1–3 | No — cognitive load, not area | Fine |

So only one band was broken, and the fix is not the obvious one.

## Why the path band was not made relative

The 18–30 band is about **time under fire** — cells of exposure against tower DPS — not geometry.
Scaling it with board size would have silenced the warning on a 64×64 map while asserting a combat
model nothing has tested. That is a worse failure than the one being fixed: a quiet wrong answer
instead of a loud one.

The band stays absolute. What changed is that its **consequence is now stated**: `MaxSpawnGoalDistance`
makes the supported board size an explicit target rather than something you discover by getting a
confusing warning.

## The distinction that is the whole slice

A map's **geometric floor** is the Manhattan distance from spawn to goal — exact, not an estimate,
because `Directions` is four-way so no diagonal shortcut exists. It is the shortest route any map
with those endpoints can have.

| Before | After |
|---|---|
| `unmazed path 63 is outside 18-30` | `board too large for the tuned combat model: spawn and goal are 63 cells apart, over the 30 cap, so no layout can reach the 18-30 path band` |

Both are true. Only the second names something you can change. The first reads as "repaint your map"
when no painting can help, and the band warning is now **suppressed** in that case rather than emitted
alongside — two warnings where one is unactionable is noise.

## Gates

| Gate | Result | Evidence |
|---|---|---|
| `dotnet build` (5 projects) | PASS | 0 warnings, 0 errors |
| `dotnet test` | PASS | **187 passed**, 0 failed (was 182; +5) |
| Determinism trace | PASS | `Verify replay` 30/30 |
| No shipped map changes verdict | PASS | `Verify maps` — crossroads and gauntlet unchanged |

```
map            size     buildable   path   vs floor  per route  spawns  verdict
crossroads     20x9     42%         19     1.0x      4.0        1       density 4.0 …
gauntlet       10x10    49%         29     1.9x      1.7        1       ok
```

## The new column earns its place

`path ÷ floor` is the genuinely size-relative quality: how much a map's design lengthens the route
beyond the minimum possible. It works at any board size, which is what the original follow-up was
reaching for.

It also says something immediately. **crossroads is `1.0x`** — a completely straight lane, zero
design lengthening — sitting next to its 4.0 buildable-per-route-cell. Those two numbers together are
a sharper statement of crossroads' known problem than either alone: a straight route with three
towers per cell of it.

gauntlet is `1.9x`, and passes.

## Core discipline

`MapValidator` lives in Core, which forbids floating point. The ratio is computed in **integer
tenths** and formatted as a string — exact, and it cannot drift between machines the way a formatted
double eventually would. `Tenths_IsExactAndNeedsNoFloats` pins it.

## Not Verified

| What | Why |
|---|---|
| Whether a 64×64 board is *actually* unbalanceable | Not measured. The cap says the combat model was not tuned for it, which is a different and weaker claim — and the honest one. Measuring it is `large-board-balance`. |
| Whether 30 is the right cap | It is `MaxUnmazedPath` by construction, so it inherits that band's provenance and no more. |
| Whether large boards are wanted | `camera-pan-zoom` made them *viewable*. Nothing has said they should be *shipped*. |

## Branch Resolution

None — verdict is PASS. Scope was narrowed on evidence during stage 04 and the record corrected in
`balance-targets.md` and in the `camera-pan-zoom` release note.
