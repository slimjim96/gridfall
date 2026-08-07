#!/usr/bin/env python3
"""
Generate a placeholder tileset for every registered terrain theme.

    python3 presentation/tiles/make-placeholder-tiles.py

Placeholder art is disposable and regenerable -- that is the whole point of it
existing as a script rather than as committed pixels somebody once drew.

## Where the colours come from

**This file defines no palette.** It parses the three ramp colours of each theme
out of `godot/View/TerrainTheme.cs` and builds the tiles from those.

That is deliberate and not just tidiness. Those seven ramps were validated
against rendered frames with units on the board -- `desert` was rotated away from
the brute's khaki band, `underwater` away from the goal marker's green -- and a
second palette written here would drift out of agreement with the first and
quietly un-validate all of it. Same reasoning as MapThemeTests reading the
registry out of the view's source rather than copying the list.

Add an eighth theme to TerrainTheme.cs, re-run this, and it gets a tileset.

## Shapes are theme-agnostic, colours are not

Every theme gets the same geometry: a road band for the 16 path masks, masonry
slabs and a rounded boulder for blocked, speckled ground for buildable. Only the
colours differ. A "bush" would be wrong on `space` and a "sand dune" wrong on
`underwater`, so nothing here is named for a material -- the theme's own hues do
that work.

Output is **byte-identical** on every run. Every speckle comes from a fixed LCG
seeded from the tile's own name, so regenerating never churns git and "the tiles
changed" always means somebody meant it.

Written with nothing but zlib and struct: this machine has no image library, and
a tileset that needs a dependency nobody has installed is one nobody regenerates.

Read tiles/README.md for the folder contract this produces.
"""

import os
import re
import struct
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
REGISTRY = os.path.join(REPO, "godot", "View", "TerrainTheme.cs")

SIZE = 64
BACKGROUND_SIZE = 128       # tiles at 4 cells per repeat, so it wants more room

# The 24px road band, centred, is what makes a corner tile meet a straight tile
# without a seam: both draw the band from the same centre to the same edge.
ROAD_HALF = 12
EDGE = 2


# ---- colour ---------------------------------------------------------------

def from_hex(text):
    return tuple(int(text[i:i + 2], 16) for i in (0, 2, 4))


def lift(colour, gain):
    """
    Brighten by SCALING the channels, which keeps the hue.

    Not by blending toward white, which does not. Half these ramps are dark and
    deliberately low-saturation -- forest blocked is 17211a -- and blending that
    30% toward white gives a neutral grey. The first pass did exactly that and
    forest's boulders came out the same colour as mountain's.
    """
    return tuple(min(255, round(c * gain)) for c in colour)


def darken(colour, amount):
    return tuple(max(0, round(c * (1.0 - amount))) for c in colour)


class Rng:
    """A fixed LCG. Seeded by tile name so output never varies between runs."""

    def __init__(self, seed):
        self.state = seed & 0xFFFFFFFF

    def next(self, n):
        self.state = (self.state * 1103515245 + 12345) & 0x7FFFFFFF
        return self.state % n


def seed_of(name):
    h = 2166136261
    for ch in name:
        h = ((h ^ ord(ch)) * 16777619) & 0xFFFFFFFF
    return h


# ---- raster ---------------------------------------------------------------

def blank(colour, size=SIZE):
    return [[colour for _ in range(size)] for _ in range(size)]


def speckle(px, rng, palette, density=6):
    for y in range(len(px)):
        for x in range(len(px)):
            if rng.next(density) == 0:
                px[y][x] = palette[rng.next(len(palette))]


def fill(px, x0, y0, x1, y1, colour):
    size = len(px)
    for y in range(max(0, y0), min(size, y1)):
        for x in range(max(0, x0), min(size, x1)):
            px[y][x] = colour


def disc(px, cx, cy, r, colour):
    size = len(px)
    for y in range(cy - r, cy + r):
        for x in range(cx - r, cx + r):
            if 0 <= x < size and 0 <= y < size and (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                px[y][x] = colour


def write_png(path, px):
    size = len(px)
    raw = b"".join(b"\x00" + bytes(v for p in row for v in p) for row in px)

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body))

    png = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(raw, 9))
        + chunk(b"IEND", b"")
    )
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(png)


# ---- tiles ----------------------------------------------------------------

def ground(base, seed):
    """Buildable: the ramp colour, lightly broken up so it is not a flat slab."""
    px = blank(base)
    speckle(px, Rng(seed), (darken(base, 0.10), lift(base, 1.14), base), density=5)
    return px


def masonry(base, seed):
    """Blocked, variant A: overlapping slabs, so a wall reads as built."""
    px = blank(base)
    rng = Rng(seed)
    tones = (lift(base, 1.60), darken(base, 0.34), lift(base, 1.22))
    speckle(px, rng, tones, density=4)
    for _ in range(7):
        x, y = rng.next(SIZE - 18), rng.next(SIZE - 18)
        w, h = 10 + rng.next(8), 8 + rng.next(6)
        fill(px, x, y, x + w, y + h, tones[rng.next(len(tones))])
    return px


def boulder(base, seed):
    """
    Blocked, variant B: rounded blobs over darker ground.

    Deliberately not "a bush" -- the same geometry has to serve forest, space and
    underwater, so the shape stays generic and the theme's hue decides whether it
    reads as foliage, rubble or hull plating.

    The blobs must clear their own ground by a wide margin. The first version of
    this sat ~14 levels above it and rendered as one flat square -- the same
    mistake, and the same fix, as the original slate terrain ramp.
    """
    floor = darken(base, 0.40)
    px = blank(floor)
    rng = Rng(seed)

    # The floor speckle must stay NEAR the floor. Speckling it with the same
    # tones the blobs use makes salt-and-pepper that the blobs then dissolve
    # into -- the shape has to win against its own background, not tie with it.
    speckle(px, rng, (darken(base, 0.30), darken(base, 0.52)), density=7)

    body, rim = lift(base, 1.95), lift(base, 1.40)
    for _ in range(3):
        cx, cy, r = 18 + rng.next(28), 18 + rng.next(28), 10 + rng.next(6)
        disc(px, cx, cy, r + 2, rim)     # a darker lip, so blobs read as separate
        disc(px, cx, cy, r, body)
    return px


def marker(colour, seed):
    """A spawn or goal pad: the reserved hue, with a lighter inner square."""
    px = blank(colour)
    speckle(px, Rng(seed), (colour, lift(colour, 1.08)), density=8)
    fill(px, 18, 18, SIZE - 18, SIZE - 18, lift(colour, 1.32))
    return px


def road(base, mask, seed):
    """
    A worn track leaving through whichever edges `mask` names.

    Both tones straddle the theme's path-only colour -- the band lighter, the
    verge darker -- so the tile AVERAGES to the ramp value it replaces. That is
    what keeps the three-tier read (blocked darkest, path-only middle, buildable
    lightest) intact once tiles are switched on.

    The verge is emphatically not the buildable colour, however good it looks: a
    path-only cell you cannot build on must not have corners that read as ground
    you can.

    **The band gain has a ceiling and it is not a matter of taste.** Across all
    seven ramps Buildable sits 1.6x-1.8x above PathOnly, so a band lifted much
    past ~1.3x lands ON the buildable tier and the road stops being visible. The
    first pass used 1.55x: desert's band came out (99,65,47) against a buildable
    of (107,74,55), and the rendered road disappeared into the board, leaving only
    its dark verge showing as thin channels. Check a capture, not the numbers --
    but if you must change this, that ratio is the number to check it against.

    Every arm is drawn from the tile centre to its edge, so the geometry of a
    corner and the geometry of a straight agree by construction -- there is no
    separate "corner tile" that can be drawn a pixel off and leave a seam.
    """
    verge = darken(base, 0.22)
    band = lift(base, 1.28)
    lip = darken(base, 0.10)

    px = blank(verge)
    speckle(px, Rng(seed), (verge, darken(base, 0.40)), density=5)

    lo, hi = SIZE // 2 - ROAD_HALF, SIZE // 2 + ROAD_HALF

    arms = []
    if mask & 1: arms.append((lo, 0, hi, hi))          # north: up the image
    if mask & 2: arms.append((lo, lo, SIZE, hi))       # east
    if mask & 4: arms.append((lo, lo, hi, SIZE))       # south
    if mask & 8: arms.append((0, lo, hi, hi))          # west

    # A dead end or an orphan still needs a centre patch, or the arm starts in
    # mid-air and the road appears to float away from the junction.
    if not arms:
        arms.append((lo, lo, hi, hi))

    for x0, y0, x1, y1 in arms:
        fill(px, x0 - EDGE, y0 - EDGE, x1 + EDGE, y1 + EDGE, lip)
    for x0, y0, x1, y1 in arms:
        fill(px, x0, y0, x1, y1, band)

    rng = Rng(seed + 977)
    for y in range(SIZE):
        for x in range(SIZE):
            if px[y][x] == band and rng.next(7) == 0:
                px[y][x] = lift(base, 1.16) if rng.next(2) else lift(base, 1.40)
    return px


def background(base, seed):
    """
    The surround: ground the board sits in, not ground you can play on.

    Built from the theme's BLOCKED colour taken darker still, at a coarser grain
    than the board tiles. Both choices are legibility, not taste -- a surround as
    bright and as finely detailed as the board makes the edge of the playable
    area hard to find, and the edge of the playable area is information.
    """
    floor = lift(base, 1.15)
    px = blank(floor, BACKGROUND_SIZE)
    rng = Rng(seed)

    tones = (darken(base, 0.18), lift(base, 1.16), floor)
    # Coarse patches rather than per-pixel noise: at 4 cells per repeat, pixel
    # speckle averages out to flat and the extra detail is wasted.
    for _ in range(90):
        x, y = rng.next(BACKGROUND_SIZE), rng.next(BACKGROUND_SIZE)
        r = 5 + rng.next(14)
        disc(px, x, y, r, tones[rng.next(len(tones))])

    speckle(px, rng, tones, density=9)
    return px


# ---- registry -------------------------------------------------------------

THEME_BLOCK = re.compile(
    r'\["(?P<id>[a-z-]+)"\]\s*=\s*new TerrainTheme\s*\{(?P<body>.*?)\}', re.S)


def read_themes():
    with open(REGISTRY, encoding="utf-8") as f:
        source = f.read()

    themes = {}
    for match in THEME_BLOCK.finditer(source):
        body = match.group("body")
        colours = {}
        for slot in ("Blocked", "PathOnly", "Buildable"):
            found = re.search(slot + r'\s*=\s*Color\.FromHtml\("([0-9a-fA-F]{6})"\)', body)
            if not found:
                raise SystemExit(f"{match.group('id')}: no {slot} in the registry block")
            colours[slot] = from_hex(found.group(1))
        themes[match.group("id")] = colours

    if len(themes) < 2:
        raise SystemExit("parsed suspiciously few themes -- has TerrainTheme.cs changed shape?")
    return themes


# Functional markers, identical on every board. Same hues as Palette.cs, and the
# README says why overriding them is usually a mistake.
SPAWN = from_hex("7a5aa0")
GOAL = from_hex("46a07a")

# Canonical NESW order, matching TileLibrary's mask parser.
MASK_NAMES = {
    0: "none", 1: "n", 2: "e", 4: "s", 8: "w",
    3: "ne", 5: "ns", 9: "nw", 6: "es", 10: "ew", 12: "sw",
    7: "nes", 11: "new", 13: "nsw", 14: "esw", 15: "nesw",
}


def build(theme, colours):
    root = os.path.join(HERE, theme)
    count = 0

    def emit(kind, name, px):
        nonlocal count
        write_png(os.path.join(root, kind, name + ".png"), px)
        count += 1

    for mask, name in sorted(MASK_NAMES.items()):
        emit("path", name, road(colours["PathOnly"], mask, seed_of(theme + "/path/" + name)))

    for name in ("ground", "ground-2", "ground-3"):
        emit("buildable", name, ground(colours["Buildable"], seed_of(theme + "/buildable/" + name)))

    emit("blocked", "slab", masonry(colours["Blocked"], seed_of(theme + "/blocked/slab")))
    emit("blocked", "slab-2", masonry(colours["Blocked"], seed_of(theme + "/blocked/slab-2")))
    emit("blocked", "mound", boulder(colours["Blocked"], seed_of(theme + "/blocked/mound")))

    emit("spawn", "pad", marker(SPAWN, seed_of(theme + "/spawn")))
    emit("goal", "pad", marker(GOAL, seed_of(theme + "/goal")))

    emit("background", "surround", background(colours["Blocked"], seed_of(theme + "/background")))

    return count


def main():
    themes = read_themes()
    total = 0
    for theme, colours in sorted(themes.items()):
        made = build(theme, colours)
        total += made
        print(f"{theme:12} {made} tiles")
    print(f"\n{len(themes)} themes, {total} tiles, from {os.path.relpath(REGISTRY, REPO)}")


if __name__ == "__main__":
    main()
