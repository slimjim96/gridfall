#!/usr/bin/env python3
"""
Render every map as a top-down atlas, in its own theme's colours.

    python3 content-data/maps/render-atlas.py [out.png]

**Needs no display and no Godot.** The in-engine capture path is the real visual
check, but it requires a running X session — and on this VM the display belongs
to an RDP session that is not always up. This gives a usable picture of a level
set from a headless shell, which is the difference between reviewing ten maps in
the morning and waiting for a session.

It is a *schematic*, not a screenshot: top-down, one pixel block per cell, no
isometric projection, no tiles, no lighting. It answers "what shape is this level
and what palette is it in", which is what a contact sheet is for. It deliberately
does not try to look like the game.

Colours are parsed out of `godot/View/TerrainTheme.cs`, the same single source
`make-placeholder-tiles.py` reads, so an atlas can never show a palette the game
does not have.
"""

import json
import os
import re
import struct
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
REGISTRY = os.path.join(REPO, "godot", "View", "TerrainTheme.cs")

CELL = 9          # pixels per map cell
PAD = 10          # gutter between panels
COLS = 5          # panels per row
BACKDROP = (13, 17, 23)     # the scene's empty colour, 0d1117

# Functional markers, identical on every board -- Palette.cs.
SPAWN = (122, 90, 160)
GOAL = (70, 160, 122)

THEME_BLOCK = re.compile(r'\["(?P<id>[a-z-]+)"\]\s*=\s*new TerrainTheme\s*\{(?P<body>.*?)\}', re.S)


def from_hex(s):
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


def read_themes():
    source = open(REGISTRY, encoding="utf-8").read()
    out = {}
    for m in THEME_BLOCK.finditer(source):
        c = {}
        for slot in ("Blocked", "PathOnly", "Buildable"):
            hit = re.search(slot + r'\s*=\s*Color\.FromHtml\("([0-9a-fA-F]{6})"\)', m.group("body"))
            c[slot] = from_hex(hit.group(1))
        out[m.group("id")] = c
    return out


def write_png(path, px):
    h, w = len(px), len(px[0])
    raw = b"".join(b"\x00" + bytes(v for p in row for v in p) for row in px)

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body))

    open(path, "wb").write(
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(raw, 9))
        + chunk(b"IEND", b""))


def colour_of(ch, ramp):
    if ch == "#": return ramp["Blocked"]
    if ch == ".": return ramp["PathOnly"]
    if ch == "b": return ramp["Buildable"]
    if ch == "S": return SPAWN
    if ch == "G": return GOAL
    return ramp["Buildable"]


def main():
    themes = read_themes()
    maps = []
    for f in sorted(os.listdir(HERE)):
        if not f.endswith(".json"):
            continue
        d = json.load(open(os.path.join(HERE, f)))
        maps.append(d)

    if not maps:
        raise SystemExit("no maps found")

    # Uniform panel size, so the sheet is a grid and the eye can compare shapes.
    pw = max(m["width"] for m in maps) * CELL
    ph = max(m["height"] for m in maps) * CELL
    rows = (len(maps) + COLS - 1) // COLS

    W = COLS * pw + (COLS + 1) * PAD
    H = rows * ph + (rows + 1) * PAD
    sheet = [[BACKDROP for _ in range(W)] for _ in range(H)]

    order = []
    for i, m in enumerate(maps):
        ramp = themes.get(m.get("theme", "slate"), themes["slate"])
        ox = PAD + (i % COLS) * (pw + PAD)
        oy = PAD + (i // COLS) * (ph + PAD)
        order.append(f"{m['id']} ({m.get('theme','slate')})")

        for y, row in enumerate(m["cells"]):
            for x, ch in enumerate(row):
                c = colour_of(ch, ramp)
                for dy in range(CELL):
                    for dx in range(CELL):
                        # 1px inset so the grid reads as cells, matching the
                        # hairline gap WorldRenderer leaves in the real board.
                        px = c if (dx and dy) else tuple(int(v * 0.7) for v in c)
                        sheet[oy + y * CELL + dy][ox + x * CELL + dx] = px

    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(REPO, "presentation", "docs", "level-atlas.png")
    write_png(out, sheet)

    print(f"{len(maps)} maps -> {os.path.relpath(out, REPO)}  ({W}x{H})")
    for i in range(0, len(order), COLS):
        print("  " + "   ".join(f"{i+j+1}. {n}" for j, n in enumerate(order[i:i + COLS])))


if __name__ == "__main__":
    main()
