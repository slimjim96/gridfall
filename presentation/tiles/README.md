# Terrain Tiles

Drop a folder in here and it becomes a selectable board theme. No code change, no registration, no
Godot import step.

```
presentation/tiles/
└── desert/               ← the folder name IS the theme id
    ├── buildable/
    │   ├── ground.png
    │   ├── ground-2.png
    │   └── ground-3.png
    ├── path/
    │   ├── ns.png        ← a straight
    │   ├── es.png        ← a corner
    │   ├── nesw.png      ← a crossroads
    │   └── … 16 in all
    ├── blocked/
    │   ├── slab.png
    │   ├── slab-2.png
    │   └── mound.png
    ├── spawn/pad.png
    ├── goal/pad.png
    └── background/
        └── surround.png  ← tiled around and under the board
```

All seven registered themes ship a full set — `slate`, `forest`, `desert`, `ocean`, `underwater`,
`mountain`, `space`.

Then in the board editor: `F4` cycles themes, `F7` re-reads this folder without relaunching.
In the game: set `"theme": "desert"` in the map's JSON, or `./run-game.sh --theme desert`.

Implemented in [`godot/View/TileLibrary.cs`](../../godot/View/TileLibrary.cs).

---

## A tile changes how a cell looks and nothing else

`CellKind` decides every rule — what visitors walk on, what you can build on, what the pathfinder
treats as solid. A tile is chosen *after* those decisions and cannot influence them. There is no
code path by which an image reaches the simulation, and `TheThemeIsNotSimulationState` in
`Gridfall.Tests` holds the line: two maps identical but for their theme hash identically at tick 0.

So painting a bush over a corridor does not block it, and a road tile on open ground does not make
it path-only. If you want the behaviour, paint the cell kind.

## The folders

Five of them name a **cell kind** — one image per cell on the grid:

| Folder | Cell kind | Painted with |
|---|---|---|
| `buildable/` | `Buildable` | `1` |
| `path/` (or `path-only/`) | `PathOnly` | `2` |
| `blocked/` | `Blocked` | `3` |
| `spawn/` | `Spawn` | `4` |
| `goal/` | `Goal` | `5` |

One does not:

| Folder | What it is |
|---|---|
| `background/` | The **surround** — ground the board sits in. Not on the grid, never walked on, never clicked. |

Worth keeping that line sharp. Every kind folder answers "what does this cell look like" and is
therefore downstream of a simulation concept. `background/` is scenery. If a second scene folder
ever appears, it belongs beside `background/`, not in the first table.

**Every folder is optional.** A kind with no folder falls back to the theme's flat colour, so you
can build a theme one folder at a time — drop in `blocked/` alone and you get stone walls on a plain
coloured board. A theme with no `background/` keeps the empty scene colour behind it, exactly as
before backgrounds existed. A folder whose name is in neither table is ignored, with a line in the
console saying so.

### `background/`

One image, tiled across a single large quad that extends well past the board and sits **below** it.
Take the first PNG in the folder, ordinally — variants make no sense here, since there is no
per-cell choice to make.

Three things are decided in code and worth knowing before you draw one
([`godot/View/Backdrop.cs`](../../godot/View/Backdrop.cs)):

- **It tiles at 4 cells per repeat**, coarser than the board's one-image-per-cell. A surround
  tiling at the grid's own pitch reads as more playable board, and where the playable area ends is
  information the player needs at a glance.
- **It sits 0.35 world units below the board**, so the board reads as a plateau standing in a
  landscape rather than a decal lying on it.
- **It can never be clicked.** `IsoGrid.TryPick` solves the ground plane analytically and
  bounds-checks against the map, so no amount of backdrop steals a pick.

Draw it **darker and lower-contrast than the board**. Nothing enforces this — a bright backdrop is
not modulated down, because silently darkening art somebody authored is worse than letting them see
it — but the board is what the player reads, and a busy surround fights it.

> **Think twice before overriding `spawn/` and `goal/`.** They are the only two markers that look
> identical on every board, deliberately: a player learns "purple is where they come from, green is
> what I am defending" once, and a theme that re-hues them makes that knowledge worthless. The same
> reasoning as the one-red rule in [art-direction.md](../docs/art-direction.md). Change the texture,
> keep the hue.

## File names

A file is read as `[mask]-[variant].png`.

### `[mask]` — how the tile connects

If the part before the first `-` is made only of the letters `n` `e` `s` `w` (or the word `none`),
it is a **connection mask**: the tile is used when exactly those neighbours are the same kind of
thing. Canonical order is NESW, but any order parses.

| File | Used when | Reads as |
|---|---|---|
| `ns.png` | north and south connect | a straight |
| `ew.png` | east and west connect | a straight, the other way |
| `es.png` | east and south connect | a corner |
| `nes.png` | three connect | a tee |
| `nesw.png` | all four connect | a crossroads |
| `n.png` | only north connects | a dead end |
| `none.png` | nothing connects | an orphan |

Sixteen masks exist. You do not need all sixteen — an absent mask is substituted, and the editor
tells you how many are being substituted. See *Themes do not have to hold the same files*, below.

**What counts as connected:**

- `PathOnly`, `Spawn` and `Goal` are one group, so a road visibly runs *into* the spawn and the goal
  rather than stopping a cell short.
- `Blocked` is its own group, and **off the edge of the board counts as blocked** — otherwise every
  wall along the map border would draw as a row of dead ends.
- `Buildable` is its own group. A road never connects into open ground; if it did, every path tile
  beside a buildable cell would draw as a junction and the road would dissolve into noise.

### `[variant]` — more than one tile for the same slot

Everything after the first `-` is ignored when matching. `stone.png` and `stone-2.png` are two
variants of the same thing; so are `ns.png` and `ns-cracked.png`.

Which variant a cell gets is a **fixed hash of its coordinates** — never random. The board is
rebuilt on every brush stroke, and a random pick would reshuffle every tile on the map each time you
painted one cell. It also means a screenshot is reproducible and a map looks the same on every
machine.

> **A name made only of compass letters is a mask even when you meant a word.** `sew.png` is read as
> the mask S|E|W. Add a dash suffix — `sew-1.png` — if you want it treated as a variant.

### Resolution order

For each cell: **exact mask** → **the theme's unmasked variants** → **the nearest mask it does
have** → the theme's flat colour.

Which is why dropping a single `dirt.png` into `path/` works: it matches no mask, so every path cell
uses it.

## Themes do not have to hold the same files — and that is handled

Two themes are unrelated folders. One may have all sixteen path masks, three grass variants and a
goal pad; the next may have two masks and nothing else. Cycling between them with `F4` is normal and
does not error.

**A missing mask is substituted, never dropped.** The nearest mask the theme *does* have is used —
fewest differing edges, ties broken toward the lower mask so the choice is identical on every
machine. A corner drawn as a straight is wrong, but it still reads as a road; the first version fell
through to the flat theme colour and punched a hole in the road at every turn, which reads as a
rendering bug.

**And you are told.** A theme is *incomplete* when a kind uses connection masks, lacks some, and has
no unmasked variant to fall back on. Then:

- the console prints `tiles: patchy -- PathOnly: 14 of 16 connection masks missing, substituted`
- the editor's brush bar turns amber: `theme: patchy (3 tiles, 14 gaps, F4)`
- `F4` onto that theme says what is missing on the status line

Two cases that are **not** gaps, because neither is an accident:

- A kind with no masked tiles at all — a pile of variants never asked to auto-tile.
- A kind with masks *and* an unmasked variant — you supplied your own fallback deliberately.

So the smallest complete `path/` folder is one file with a non-compass name.

## Drawing the images

- **Any square size.** 64×64 is what the placeholders use. Bigger is fine; the tile is mapped onto
  the cell quad regardless.
- **North is up in the image.** UV (0,0) is the cell's north-west corner and V increases south. The
  camera's 45° yaw then puts the image's north edge toward the upper *right* of the screen — so a
  road drawn straight up the image renders as a diagonal. That is correct, and it is the single most
  common surprise when authoring a tile by hand.
- **Alpha is ignored.** Terrain tiles are opaque.
- **Filtering is nearest-neighbour**, so pixel art stays crisp and does not turn to mush at zoom.
- **Judge from a rendered capture, never from the hex.** ACES tonemapping plus ambient light
  compresses the dark end hard. The placeholder bush was authored within ~14 levels of its own
  ground and arrived on screen as one flat green square — the same mistake, and the same fix, as the
  original slate terrain ramp.
- The **average colour** of each tile is computed at load and painted onto the side walls of raised
  cells, so a wall's sides match its top without you supplying a second image.

## Themes with a colour ramp AND tiles

They compose. `TerrainTheme.cs` registers seven colour ramps (`slate`, `forest`, `desert`, `ocean`,
`underwater`, `mountain`, `space`); this folder can add tiles to any of them, or introduce a theme
that exists only as tiles — a folder with no ramp falls back to `slate`'s colours for
anything it does not cover.

`TerrainTheme.AllIds` is the union, and it is what `F4` cycles.

## Regenerating the placeholders

Every shipped tile is placeholder art and is meant to be replaced. It is produced by a script rather
than committed as pixels somebody once drew:

```bash
python3 presentation/tiles/make-placeholder-tiles.py
```

Byte-identical on every run — every speckle comes from a fixed LCG seeded from the tile's own name —
so regenerating never churns git, and "the tiles changed" always means somebody meant it.

**The script defines no palette.** It parses each theme's three ramp colours out of
`godot/View/TerrainTheme.cs` and builds the tiles from those. That is not tidiness: those
ramps were validated against rendered frames with units on the board — `desert` rotated away from the
brute's khaki band, `underwater` away from the goal marker's green — and a second palette written in
the generator would drift and quietly un-validate all of it. Add an eighth theme to `TerrainTheme.cs`,
re-run, and it gets a tileset.

Shapes are theme-agnostic and only colours differ, which is why nothing is named for a material: a
"bush" would be wrong on `space` and a "dune" wrong on `underwater`, so the blocked variants are
`slab` and `mound` and the theme's own hues decide whether a mound reads as foliage, rubble or hull
plating.

### The one hard constraint if you retune it

Across all seven ramps, `Buildable` sits **1.6x–1.8x above `PathOnly`**. So the road band cannot be
lifted much past ~1.3x or it lands *on* the buildable tier and the road stops being visible. The
first pass used 1.55x: desert's band came out `(99,65,47)` against a buildable of `(107,74,55)`, and
the rendered road vanished into the board, leaving only its dark verge as thin channels.

The verge is likewise not the buildable colour, however good it looks — a path-only cell you cannot
build on must not have corners that read as ground you can.

## Known limits

- **A theme is chosen per map, not per cell.** You cannot place *this specific* bush on *that*
  cell; you choose the folder, and variants are distributed by coordinate hash. Deliberate per-cell
  tile placement would need a new per-cell layer in the map format — a real change, not an
  extension, and not built.
- **Tiles live outside `res://`.** That is what makes "drop it in, press `F7`" work with no import
  step, and it means they would not be packed into a release export as things stand. There is no
  export preset yet either way (follow-up slug `release-export`).
