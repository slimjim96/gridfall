#!/usr/bin/env python3
"""
Generate the two fixture assets that exercise SpriteUnitView and MeshUnitView.

    python3 presentation/units/make-fixture-assets.py

## These are fixtures, not art

They exist so the two view implementations are *verified* rather than merely
compiled, and so the format bake-off in `prompts/tower-frost-spire.md` has a
working target to drop real output into. They are ugly on purpose — nobody
should mistake them for a design, and the moment Ludo.ai returns something the
matching folder is overwritten and this script stops mattering.

    arrow-tower/   sprite strips + unit.json   -> SpriteUnitView
    cannon/        model.glb                   -> MeshUnitView

One of each, deliberately: a single capture then shows both formats on one board
with creeps walking behind both, which is the only way to check the property
that actually matters (occlusion) for both paths at once.

## Proportions are not invented here

Each fixture matches the placeholder it stands in for, so swapping formats is
visibly a format change and not a size change:

    arrow-tower   Shapes.TallPrism(0.30, 1.45)      0.30 wide, 1.45 tall
    cannon        Shapes.SquatCylinder(0.36, 0.62)  0.72 wide, 0.62 tall

The sprite additionally carries the camera's foreshortening, because a sprite is
drawn as the thing *appears*: on-screen height is `1.45 * cos(30 degrees)`, so the
frame proportion is 4.19:1, not the object's 4.83:1. Getting that backwards is
the single easiest way to make a sprite that looks wrong beside a mesh, and it is
why the prompt file states the number explicitly.

Hard alpha only -- every pixel fully opaque or fully transparent. A soft edge
forces alpha blending, blending disables depth write, and the sprite silently
stops occluding anything.

Written with zlib and struct alone: no image library, no glTF library, nothing to
install.
"""

import json
import math
import os
import struct
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))

FRAME = 128                      # px per sprite frame
FILL = 0.80                      # unit fills 80% of frame height, per the guide
PITCH = math.radians(30.0)       # IsoGrid.CameraPitch

ARROW_COLOUR = (217, 143, 69)    # Palette.TowerArrow  d98f45
CANNON_COLOUR = (196, 106, 58)   # Palette.TowerCannon c46a3a

TRANSPARENT = (0, 0, 0, 0)


# ---- png ------------------------------------------------------------------

def write_png(path, px):
    """RGBA8. Alpha is only ever 0 or 255 -- see the module docstring."""
    h, w = len(px), len(px[0])
    raw = b"".join(b"\x00" + bytes(v for p in row for v in p) for row in px)

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body))

    blob = (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9))
            + chunk(b"IEND", b""))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(blob)


def solid(colour, alpha=255):
    return (colour[0], colour[1], colour[2], alpha)


def shade(colour, factor):
    return tuple(min(255, max(0, round(c * factor))) for c in colour)


def arrow_frame(brightness):
    """One frame: a square-shouldered column with a visible head."""
    px = [[TRANSPARENT for _ in range(FRAME)] for _ in range(FRAME)]

    unit_h = round(FRAME * FILL)
    # 4.19:1 on screen -- the object's 4.83:1 foreshortened by cos(pitch).
    unit_w = max(4, round(unit_h * (0.30 / (1.45 * math.cos(PITCH)))))

    top = (FRAME - unit_h) // 2
    left = (FRAME - unit_w) // 2

    body = shade(ARROW_COLOUR, brightness)
    dark = shade(ARROW_COLOUR, brightness * 0.72)
    head_w, head_h = unit_w * 2, max(6, unit_h // 8)

    for y in range(top, top + unit_h):
        for x in range(left, left + unit_w):
            # Right third darker, so the column reads as having a lit side.
            px[y][x] = solid(dark if x >= left + unit_w * 2 // 3 else body)

    # The head: wider than the shaft, which is the arrow tower's whole silhouette.
    hl = (FRAME - head_w) // 2
    for y in range(top, top + head_h):
        for x in range(hl, hl + head_w):
            px[y][x] = solid(dark if x >= hl + head_w * 2 // 3 else body)

    return px


def strip(frames):
    """Lay frames out horizontally. Frame count is implied by strip width/height."""
    h = len(frames[0])
    return [[px for frame in frames for px in frame[y]] for y in range(h)]


def build_sprite():
    root = os.path.join(HERE, "arrow-tower")

    # idle: a slow 4-frame brightness breath, looping.
    write_png(os.path.join(root, "idle.png"),
              strip([arrow_frame(b) for b in (1.00, 1.06, 1.12, 1.06)]))

    # fire: a 3-frame flare, one-shot. Frame counts are what the guide calls for
    # at 30fps; these are short because the fixture only has to prove the path.
    write_png(os.path.join(root, "fire.png"),
              strip([arrow_frame(b) for b in (1.35, 1.20, 1.00)]))

    # The one number a PNG cannot carry: how big the frame is in world terms.
    # unit height on screen / FILL.
    frame_cells = round(1.45 * math.cos(PITCH) / FILL, 3)
    with open(os.path.join(root, "unit.json"), "w", encoding="utf-8") as f:
        json.dump({"frameCells": frame_cells}, f, indent=2)
        f.write("\n")

    return root, frame_cells


# ---- glb ------------------------------------------------------------------

def drum(bottom_r, top_r, height, sides=8):
    """A squat tapered prism, flat shaded, origin at the base centre."""
    positions, normals, indices = [], [], []

    def ring(radius, y):
        return [(math.cos(i / sides * math.tau) * radius, y,
                 math.sin(i / sides * math.tau) * radius) for i in range(sides)]

    low, high = ring(bottom_r, 0.0), ring(top_r, height)

    def quad(a, b, c, d, n):
        base = len(positions)
        positions.extend([a, b, c, d])
        normals.extend([n] * 4)
        # Reversed relative to the obvious order. glTF front faces are
        # counter-clockwise seen from OUTSIDE, and low[i] -> low[j] -> high[j]
        # winds clockwise from out there. The first version got this backwards
        # and Godot culled every side, so the drum rendered as an open funnel
        # with a black interior wall -- see check_winding, which now catches it.
        indices.extend([base, base + 2, base + 1, base, base + 3, base + 2])

    for i in range(sides):
        j = (i + 1) % sides
        # Face normal from the midpoint, which is exact enough for a flat prism.
        mx = (low[i][0] + low[j][0]) / 2
        mz = (low[i][2] + low[j][2]) / 2
        length = math.hypot(mx, mz) or 1.0
        quad(low[i], low[j], high[j], high[i], (mx / length, 0.0, mz / length))

    # Caps, as fans from vertex 0 of each ring. The ring runs clockwise seen from
    # +Y, so the TOP cap is the one that needs reversing, not the bottom.
    for ring_verts, normal, flip in ((high, (0.0, 1.0, 0.0), True),
                                     (low, (0.0, -1.0, 0.0), False)):
        base = len(positions)
        positions.extend(ring_verts)
        normals.extend([normal] * sides)
        for i in range(1, sides - 1):
            tri = (base, base + i, base + i + 1)
            indices.extend(reversed(tri) if flip else tri)

    check_winding(positions, normals, indices)
    return positions, normals, indices


def check_winding(positions, normals, indices):
    """
    Every triangle's winding must agree with its own vertex normal.

    Inverted winding is invisible in a viewer that draws double-sided and
    catastrophic in an engine that does not: Godot culls the back faces and you
    get a hollow shell lit from the inside. That is a five-line check and it
    would have saved a render-and-squint cycle, so it runs every time.
    """
    for t in range(0, len(indices), 3):
        i0, i1, i2 = indices[t], indices[t + 1], indices[t + 2]
        p0, p1, p2 = positions[i0], positions[i1], positions[i2]

        u = [p1[k] - p0[k] for k in range(3)]
        v = [p2[k] - p0[k] for k in range(3)]
        geometric = (u[1] * v[2] - u[2] * v[1],
                     u[2] * v[0] - u[0] * v[2],
                     u[0] * v[1] - u[1] * v[0])

        agreement = sum(geometric[k] * normals[i0][k] for k in range(3))
        if agreement <= 0:
            raise SystemExit(
                f"triangle {t // 3} winds against its normal "
                f"(dot={agreement:.4f}) -- it would be culled and render hollow")


def build_mesh():
    root = os.path.join(HERE, "cannon")
    positions, normals, indices = drum(0.36, 0.30, 0.62)

    # A one-shot recoil: squash down and back. Not root motion -- scale only, so
    # the model never leaves the cell the simulation says it is in.
    times = [0.0, 0.1, 0.3]
    scales = [(1.0, 1.0, 1.0), (1.12, 0.82, 1.12), (1.0, 1.0, 1.0)]

    blob = bytearray()
    views, accessors = [], []

    def add(data, target, accessor):
        offset = len(blob)
        blob.extend(data)
        while len(blob) % 4:
            blob.append(0)
        view = {"buffer": 0, "byteOffset": offset, "byteLength": len(data)}
        if target is not None:
            view["target"] = target
        views.append(view)
        accessor["bufferView"] = len(views) - 1
        accessors.append(accessor)
        return len(accessors) - 1

    flat = [c for v in positions for c in v]
    a_pos = add(struct.pack(f"<{len(flat)}f", *flat), 34962, {
        "componentType": 5126, "count": len(positions), "type": "VEC3",
        "min": [min(v[i] for v in positions) for i in range(3)],
        "max": [max(v[i] for v in positions) for i in range(3)]})

    flat = [c for v in normals for c in v]
    a_nrm = add(struct.pack(f"<{len(flat)}f", *flat), 34962,
                {"componentType": 5126, "count": len(normals), "type": "VEC3"})

    a_idx = add(struct.pack(f"<{len(indices)}H", *indices), 34963,
                {"componentType": 5123, "count": len(indices), "type": "SCALAR"})

    a_time = add(struct.pack(f"<{len(times)}f", *times), None,
                 {"componentType": 5126, "count": len(times), "type": "SCALAR",
                  "min": [min(times)], "max": [max(times)]})

    flat = [c for v in scales for c in v]
    a_scale = add(struct.pack(f"<{len(flat)}f", *flat), None,
                  {"componentType": 5126, "count": len(scales), "type": "VEC3"})

    gltf = {
        "asset": {"version": "2.0", "generator": "gridfall make-fixture-assets.py"},
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": [{"mesh": 0, "name": "cannon"}],
        "meshes": [{"name": "cannon", "primitives": [{
            "attributes": {"POSITION": a_pos, "NORMAL": a_nrm},
            "indices": a_idx, "material": 0}]}],
        "materials": [{
            "name": "cannon",
            "pbrMetallicRoughness": {
                "baseColorFactor": [c / 255 for c in CANNON_COLOUR] + [1.0],
                "metallicFactor": 0.0, "roughnessFactor": 1.0}}],
        # Named exactly as IUnitView expects. A clip called anything else can
        # never be triggered -- ludo-prompt-guide.md "The standard clip set".
        "animations": [{
            "name": "fire",
            "samplers": [{"input": a_time, "output": a_scale, "interpolation": "LINEAR"}],
            "channels": [{"sampler": 0, "target": {"node": 0, "path": "scale"}}]}],
        "bufferViews": views,
        "accessors": accessors,
        "buffers": [{"byteLength": len(blob)}],
    }

    js = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    js += b" " * (-len(js) % 4)
    bin_chunk = bytes(blob) + b"\x00" * (-len(blob) % 4)

    glb = (struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bin_chunk))
           + struct.pack("<II", len(js), 0x4E4F534A) + js
           + struct.pack("<II", len(bin_chunk), 0x004E4942) + bin_chunk)

    os.makedirs(root, exist_ok=True)
    with open(os.path.join(root, "model.glb"), "wb") as f:
        f.write(glb)

    return root, len(positions), len(indices) // 3


def main():
    sprite_root, frame_cells = build_sprite()
    print(f"sprite  {os.path.relpath(sprite_root, HERE)}/  idle(4) fire(3)  frameCells={frame_cells}")

    mesh_root, verts, tris = build_mesh()
    print(f"mesh    {os.path.relpath(mesh_root, HERE)}/model.glb  {verts} verts, {tris} tris, clip 'fire'")


if __name__ == "__main__":
    main()
