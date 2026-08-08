#!/usr/bin/env python3
"""
Capture every map in the board editor and tile the frames into one contact sheet.

    python3 content-data/maps/capture-iso-atlas.py [out.png]

**This is the real visual check.** Its sibling `render-atlas.py` draws a top-down
schematic straight from JSON: no display, no Godot, one pixel block per cell. It
answers "what shape is this level"; it cannot answer "how does this level read at
the iso angle", because it never renders a tile, a wall height, or a shadow.

Needs a display and `godot-mono` — see `scripts/godot-env.sh`. When there is no
session, use `render-atlas.py` and say plainly that the result is a schematic.
Ten levels were once signed off from a schematic; three of them turned out to be
carrying validator warnings nobody had seen.

No PIL and no ImageMagick on this box, so PNG reading and writing are hand-rolled
below. Godot writes 8-bit non-interlaced RGB, which is the only case decode()
handles -- it raises rather than guess.
"""

import os
import struct
import subprocess
import sys
import tempfile
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))

# Ordered as the level set is discussed, with the two hand-built boards last so
# a reader meets the generated ten as a group.
MAPS = ["meander", "spiral", "chambers", "switchback", "comb",
        "ringfort", "braid", "stepwell", "atoll", "driftway",
        "crossroads", "gauntlet"]

COLS = 3
SCALE = 3          # 1280x720 -> 426x240 per cell
FRAMES = 30        # matches the other capture paths; the rig is locked anyway


# ---- png ------------------------------------------------------------------

def decode(path):
    """-> (w, h, [row0, row1, ...]) with each row a bytearray of RGB triples."""
    data = open(path, "rb").read()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"{path}: not a PNG")

    idat, pos = bytearray(), 8
    w = h = None
    while pos < len(data):
        length, kind = struct.unpack(">I", data[pos:pos + 4])[0], data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + length]
        if kind == b"IHDR":
            w, h, depth, colour, _, _, interlace = struct.unpack(">IIBBBBB", body)
            if (depth, colour, interlace) != (8, 2, 0):
                raise ValueError(f"{path}: expected 8-bit RGB non-interlaced, "
                                 f"got depth={depth} colour={colour} interlace={interlace}")
        elif kind == b"IDAT":
            idat += body
        elif kind == b"IEND":
            break
        pos += 12 + length

    raw = zlib.decompress(bytes(idat))
    stride = w * 3
    rows, prev, p = [], bytearray(stride), 0
    for _ in range(h):
        f = raw[p]
        line = bytearray(raw[p + 1:p + 1 + stride])
        p += 1 + stride
        # Paeth and friends, per the spec. bpp is 3 for RGB8.
        if f == 1:
            for i in range(3, stride):
                line[i] = (line[i] + line[i - 3]) & 0xFF
        elif f == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif f == 3:
            for i in range(stride):
                left = line[i - 3] if i >= 3 else 0
                line[i] = (line[i] + ((left + prev[i]) >> 1)) & 0xFF
        elif f == 4:
            for i in range(stride):
                a = line[i - 3] if i >= 3 else 0
                b = prev[i]
                c = prev[i - 3] if i >= 3 else 0
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                pred = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pred) & 0xFF
        elif f != 0:
            raise ValueError(f"{path}: bad filter {f}")
        rows.append(line)
        prev = line
    return w, h, rows


def encode(path, w, h, rows):
    def chunk(kind, body):
        return (struct.pack(">I", len(body)) + kind + body
                + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF))

    raw = b"".join(b"\x00" + bytes(r) for r in rows)
    png = (b"\x89PNG\r\n\x1a\n"
           + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
           + chunk(b"IDAT", zlib.compress(raw, 9))
           + chunk(b"IEND", b""))
    open(path, "wb").write(png)


def box_downscale(w, h, rows, n):
    """Average n*n blocks. Nearest-neighbour turns the one-pixel grid lines
    between cells into moire, which is exactly the detail being judged."""
    ow, oh = w // n, h // n
    out = []
    for oy in range(oh):
        line = bytearray(ow * 3)
        src = rows[oy * n:oy * n + n]
        for ox in range(ow):
            r = g = b = 0
            base = ox * n * 3
            for s in src:
                for k in range(n):
                    r += s[base + k * 3]
                    g += s[base + k * 3 + 1]
                    b += s[base + k * 3 + 2]
            count = n * n
            line[ox * 3:ox * 3 + 3] = bytes((r // count, g // count, b // count))
        out.append(line)
    return ow, oh, out


# ---- capture --------------------------------------------------------------

def capture(map_id, path):
    result = subprocess.run(
        [os.path.join(REPO, "run-editor.sh"), map_id,
         "--shot", path, "--shot-after", str(FRAMES)],
        cwd=REPO, capture_output=True, text=True, timeout=180)
    if not os.path.exists(path):
        tail = "\n".join(result.stdout.strip().splitlines()[-5:])
        raise RuntimeError(f"{map_id}: no frame written\n{tail}")


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        REPO, "presentation", "docs", "level-atlas-iso.png")

    tiles = []
    with tempfile.TemporaryDirectory() as tmp:
        for map_id in MAPS:
            shot = os.path.join(tmp, f"{map_id}.png")
            capture(map_id, shot)
            w, h, rows = decode(shot)
            tiles.append(box_downscale(w, h, rows, SCALE))
            print(f"  {map_id}")

    tw, th = tiles[0][0], tiles[0][1]
    cols = COLS
    rows_n = (len(tiles) + cols - 1) // cols
    sheet_w, sheet_h = tw * cols, th * rows_n
    sheet = [bytearray(sheet_w * 3) for _ in range(sheet_h)]

    for i, (_, _, tile) in enumerate(tiles):
        ox, oy = (i % cols) * tw, (i // cols) * th
        for y, line in enumerate(tile):
            sheet[oy + y][ox * 3:(ox + tw) * 3] = line

    encode(out, sheet_w, sheet_h, sheet)
    print(f"{len(tiles)} maps -> {os.path.relpath(out, REPO)}  ({sheet_w}x{sheet_h})")


if __name__ == "__main__":
    main()
