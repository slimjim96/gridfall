# Terrain Tiles

Drop a folder in here and it becomes a selectable board theme. No code change, no registration, no
Godot import step.

```
presentation/tiles/
└── roadway/              ← the folder name IS the theme id
    ├── buildable/
    │   ├── grass.png
    │   ├── grass-2.png
    │   └── grass-3.png
    ├── path/
    │   ├── ns.png        ← a straight
    │   ├── es.png        ← a corner
    │   ├── nesw.png      ← a crossroads
    │   └── … 16 in all
    ├── blocked/
    │   ├── stone.png
    │   ├── stone-2.png
    │   └── bush.png
    ├── spawn/pad.png
    └── goal/pad.png
```

Then in the board editor: `F4` cycles to it, `F7` re-reads this folder without relaunching.
In the game: set `"theme": "roadway"` in the map's JSON, or `./run-game.sh --theme roadway`.

Implemented in [`godot/Placeholders/TileLibrary.cs`](../../godot/Placeholders/TileLibrary.cs).

---

## A tile changes how a cell looks and nothing else

`CellKind` decides every rule — what creeps walk on, what you can build on, what the pathfinder
treats as solid. A tile is chosen *after* those decisions and cannot influence them. There is no
code path by which an image reaches the simulation, and `TheThemeIsNotSimulationState` in
`Gridfall.Tests` holds the line: two maps identical but for their theme hash identically at tick 0.

So painting a bush over a corridor does not block it, and a road tile on open ground does not make
it path-only. If you want the behaviour, paint the cell kind.

## The five kind folders

| Folder | Cell kind | Painted with |
|---|---|---|
| `buildable/` | `Buildable` | `1` |
| `path/` (or `path-only/`) | `PathOnly` | `2` |
| `blocked/` | `Blocked` | `3` |
| `spawn/` | `Spawn` | `4` |
| `goal/` | `Goal` | `5` |

**Every one is optional.** A kind with no folder falls back to the theme's flat colour, so you can
build a theme one folder at a time — drop in `blocked/` alone and you get stone walls on a plain
coloured board. A folder whose name is not in that table is ignored, with a line in the console
saying so.

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

Sixteen masks exist. You do not need all sixteen — an absent mask falls through to the unmasked
tiles below, and then to the theme colour.

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

For each cell: **exact mask** → **unmasked variants** → **the theme's flat colour**.

Which is why dropping a single `dirt.png` into `path/` works: it matches no mask, so every path cell
uses it.

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
that exists only as tiles — `roadway` has no ramp at all and falls back to `slate`'s colours for
anything it does not cover.

`TerrainTheme.AllIds` is the union, and it is what `F4` cycles.

## Regenerating the placeholders

`roadway` is placeholder art and is meant to be replaced. It is produced by a script rather than
committed as pixels somebody once drew:

```bash
python3 presentation/tiles/make-placeholder-tiles.py
```

Byte-identical on every run — every speckle comes from a fixed LCG seeded from the tile's own name —
so regenerating never churns git, and "the tiles changed" always means somebody meant it.

## Known limits

- **A theme is chosen per map, not per cell.** You cannot place *this specific* bush on *that*
  cell; you choose the folder, and variants are distributed by coordinate hash. Deliberate per-cell
  tile placement would need a new per-cell layer in the map format — a real change, not an
  extension, and not built.
- **Tiles live outside `res://`.** That is what makes "drop it in, press `F7`" work with no import
  step, and it means they would not be packed into a release export as things stand. There is no
  export preset yet either way (follow-up slug `release-export`).
