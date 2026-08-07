#!/usr/bin/env python3
"""
Generate the `roadway` placeholder tileset.

Placeholder art is disposable and regenerable -- that is the whole point of it
existing as a script rather than as committed pixels somebody once drew. Run
this to rebuild the set from scratch:

    python3 presentation/tiles/make-placeholder-tiles.py

Output is **byte-identical** on every run. Every "random" speckle comes from a
fixed LCG seeded from the tile's own name, so regenerating never churns git and
"the tiles changed" always means somebody meant it.

It writes PNGs with nothing but zlib and struct, because this machine has no
image library and a tileset that needs a dependency nobody has installed is a
tileset nobody regenerates.

Read tiles/README.md for the folder contract this produces.
"""

import os
import struct
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
THEME = os.path.join(HERE, "roadway")

SIZE = 64

# The 24px road band, centred, is what makes a corner tile meet a straight tile
# without a seam: both draw the band from the same centre to the same edge.
ROAD_HALF = 12
EDGE = 2          # darker lip either side of the band

GRASS = (74, 98, 71)
GRASS_SPECKLE = ((69, 93, 66), (79, 103, 76), (71, 96, 69))
ROAD = (150, 132, 104)
ROAD_SPECKLE = ((143, 125, 98), (157, 139, 111), (147, 130, 102))
ROAD_EDGE = (112, 97, 74)
STONE = (58, 58, 66)
STONE_SPECKLE = ((66, 66, 75), (51, 51, 58), (61, 61, 69))
# Foliage over dark earth. The first pass put the blobs within ~14 levels of
# their ground and a rendered tile showed one flat green square -- the same
# mistake, and the same fix, as the original slate terrain ramp. Judge these
# from an image, never from the tuples.
BUSH_GROUND = (38, 54, 40)
BUSH = (62, 96, 58)
BUSH_LIT = (84, 120, 70)
# Functional markers keep the hues Palette.cs reserves for them -- see the note
# in README.md about why overriding these is usually a mistake.
SPAWN = (122, 90, 160)
GOAL = (70, 160, 122)


class Rng:
    """A fixed LCG. Seeded by tile name so output never varies between runs."""

    def __init__(self, seed):
        self.state = seed & 0xFFFFFFFF

    def next(self, n):
        self.state = (self.state * 1103515245 + 12345) & 0x7FFFFFFF
        return self.state % n


def blank(colour):
    return [[colour for _ in range(SIZE)] for _ in range(SIZE)]


def speckle(px, rng, palette, density=6):
    for y in range(SIZE):
        for x in range(SIZE):
            if rng.next(density) == 0:
                px[y][x] = palette[rng.next(len(palette))]


def fill(px, x0, y0, x1, y1, colour):
    for y in range(max(0, y0), min(SIZE, y1)):
        for x in range(max(0, x0), min(SIZE, x1)):
            px[y][x] = colour


def write_png(path, px):
    raw = b"".join(b"\x00" + bytes(v for p in row for v in p) for row in px)

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body))

    png = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(raw, 9))
        + chunk(b"IEND", b"")
    )
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(png)


# ---- tiles ----------------------------------------------------------------

def grass(seed):
    px = blank(GRASS)
    speckle(px, Rng(seed), GRASS_SPECKLE, density=5)
    return px


def stone(seed):
    px = blank(STONE)
    rng = Rng(seed)
    speckle(px, rng, STONE_SPECKLE, density=4)
    # Blocky masonry: a few larger slabs so a wall reads as built, not noisy.
    for _ in range(7):
        x, y = rng.next(SIZE - 18), rng.next(SIZE - 18)
        w, h = 10 + rng.next(8), 8 + rng.next(6)
        fill(px, x, y, x + w, y + h, STONE_SPECKLE[rng.next(len(STONE_SPECKLE))])
    return px


def bush(seed):
    px = blank(BUSH_GROUND)
    rng = Rng(seed)
    speckle(px, rng, (BUSH, BUSH_LIT), density=3)
    # Three overlapping blobs. Round-ish, so a bush is not mistaken for masonry.
    for _ in range(3):
        cx, cy, r = 16 + rng.next(32), 16 + rng.next(32), 9 + rng.next(6)
        tone = BUSH if rng.next(2) else BUSH_LIT
        for y in range(cy - r, cy + r):
            for x in range(cx - r, cx + r):
                if 0 <= x < SIZE and 0 <= y < SIZE and (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                    px[y][x] = tone
    return px


def marker(colour, seed):
    """A spawn or goal pad: the reserved hue, with a lighter inner square."""
    px = blank(colour)
    speckle(px, Rng(seed), (colour,), density=8)
    inner = tuple(min(255, c + 28) for c in colour)
    fill(px, 18, 18, SIZE - 18, SIZE - 18, inner)
    return px


def road(mask, seed):
    """
    A dirt road leaving through whichever edges `mask` names.

    Every arm is drawn from the tile centre to its edge, so the geometry of a
    corner and the geometry of a straight agree by construction -- there is no
    separate "corner tile" that can be drawn one pixel off and leave a seam.
    """
    px = grass(seed)
    lo, hi = SIZE // 2 - ROAD_HALF, SIZE // 2 + ROAD_HALF

    arms = []
    if mask & 1:  arms.append((lo, 0, hi, hi))          # north: y-  (up the image)
    if mask & 2:  arms.append((lo, lo, SIZE, hi))       # east:  x+
    if mask & 4:  arms.append((lo, lo, hi, SIZE))       # south: y+
    if mask & 8:  arms.append((0, lo, hi, hi))          # west:  x-

    # A dead end or an orphan still needs a centre patch, or the arm starts in
    # mid-air and the road appears to float away from the junction.
    if not arms:
        arms.append((lo, lo, hi, hi))

    for x0, y0, x1, y1 in arms:
        fill(px, x0 - EDGE, y0 - EDGE, x1 + EDGE, y1 + EDGE, ROAD_EDGE)
    for x0, y0, x1, y1 in arms:
        fill(px, x0, y0, x1, y1, ROAD)

    rng = Rng(seed + 977)
    for y in range(SIZE):
        for x in range(SIZE):
            if px[y][x] == ROAD and rng.next(7) == 0:
                px[y][x] = ROAD_SPECKLE[rng.next(len(ROAD_SPECKLE))]
    return px


# Canonical NESW order, matching TileLibrary's mask parser.
MASK_NAMES = {
    0: "none", 1: "n", 2: "e", 4: "s", 8: "w",
    3: "ne", 5: "ns", 9: "nw", 6: "es", 10: "ew", 12: "sw",
    7: "nes", 11: "new", 13: "nsw", 14: "esw", 15: "nesw",
}


def seed_of(name):
    h = 2166136261
    for ch in name:
        h = ((h ^ ord(ch)) * 16777619) & 0xFFFFFFFF
    return h


def main():
    made = []

    for mask, name in sorted(MASK_NAMES.items()):
        path = os.path.join(THEME, "path", name + ".png")
        write_png(path, road(mask, seed_of("path/" + name)))
        made.append(path)

    for name in ("grass", "grass-2", "grass-3"):
        path = os.path.join(THEME, "buildable", name + ".png")
        write_png(path, grass(seed_of("buildable/" + name)))
        made.append(path)

    for name in ("stone", "stone-2"):
        path = os.path.join(THEME, "blocked", name + ".png")
        write_png(path, stone(seed_of("blocked/" + name)))
        made.append(path)
    path = os.path.join(THEME, "blocked", "bush.png")
    write_png(path, bush(seed_of("blocked/bush")))
    made.append(path)

    write_png(os.path.join(THEME, "spawn", "pad.png"), marker(SPAWN, seed_of("spawn")))
    write_png(os.path.join(THEME, "goal", "pad.png"), marker(GOAL, seed_of("goal")))
    made += [os.path.join(THEME, "spawn", "pad.png"), os.path.join(THEME, "goal", "pad.png")]

    print(f"wrote {len(made)} tiles under {THEME}")


if __name__ == "__main__":
    main()
