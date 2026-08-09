#!/usr/bin/env python3
"""
Generate ten example levels, one per registered terrain theme.

    python3 content-data/maps/make-example-levels.py

Each is a different *structural* motif, not a recolour — a serpentine plays
differently from a spiral even before the theme changes. The theme assignment is
one-per-map so the set doubles as a visual catalogue of every ramp and tileset.

## The bands pull against each other, and that decides the grid size

`MapTargets` wants buildable at 35-55% of the grid and the unmazed path at 18-30.
The map report also proposes at most 2.0 buildable cells per route cell.

Those cannot all hold on a large grid. On 20x9 (180 cells), 40% buildable is 72
cells against a route capped at 30 — density 2.4, and `crossroads` sits at 4.0.
The only way to satisfy all three is a **small grid with a long winding route**,
which is why these are ~13x12 with routes near the top of the band rather than
the wide open boards the validator would also accept.

Every layout is checked here before it is written: one goal, reachable spawns,
and the three bands. A motif that cannot satisfy them is reported, not shipped.
"""

import json
import os
from collections import deque

HERE = os.path.dirname(os.path.abspath(__file__))

# One theme each, so the set is also a catalogue of every ramp.
LEVELS = [
    ("meander",   "forest"),
    ("spiral",    "desert"),
    ("chambers",  "mountain"),
    ("switchback","slate"),
    ("comb",      "ocean"),
    ("ringfort",  "ash"),
    ("braid",     "marsh"),
    ("stepwell",  "underwater"),
    ("atoll",     "tundra"),
    ("driftway",  "space"),
]

# Which towers a board offers, for boards that restrict. Absent means every
# tower -- and that is NOT the same as listing them all, because a board with no
# entry here keeps whatever the tower set grows to, while a listed board keeps
# exactly what it names. See MapDef.TowerIds.
#
# Lives in the generator so a roster survives regeneration. Setting it only in
# the JSON would work until the next `make-example-levels.py`, which is the kind
# of loss nobody notices until a board quietly offers a tower again.
ROSTERS = {
    # The gentlest board in the set, and the one a player meets first: one tool,
    # so the first thing learned is placement rather than shopping. It was also
    # the most degenerate board measured -- 0.0% of runs lost at sd 0.0 -- so
    # there is nothing here for a second tower to make more interesting.
    "meander": ["arrow-tower"],
}


def blank(w, h):
    """Walled rectangle, buildable interior."""
    return [["#" if x in (0, w - 1) or y in (0, h - 1) else "b"
             for x in range(w)] for y in range(h)]


def wall(g, x0, y0, x1, y1):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if 0 <= y < len(g) and 0 <= x < len(g[0]):
                g[y][x] = "#"


def lane(g, x0, y0, x1, y1):
    """Carve path-only corridor. Forces the route without allowing building on it."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if 0 <= y < len(g) and 0 <= x < len(g[0]):
                g[y][x] = "."


# ---- motifs ---------------------------------------------------------------
# Each returns (grid, spawn, goal). Kept small and explicit rather than
# parameterised: a layout you can read is worth more than one you can tweak.

def meander(w=13, h=11):
    g = blank(w, h)
    # One bend, not three. Each full traverse of a 13-wide board adds ~13 to the
    # route, and the band tops out at 30 -- two bends already overshoot.
    wall(g, 1, 5, w - 4, 5)
    return g, (0, 1), (w - 1, h - 2)


def spiral(w=13, h=13):
    g = blank(w, h)
    # A spiral has to be WALKED, not just drawn.
    #
    # The first version painted four thin walls and left the goal on the east
    # edge, so the route was the Manhattan minimum -- `vs floor` 1.0x, a monotone
    # staircase that never turned. Nothing about it spiralled, and at the iso
    # angle it read as a C.
    #
    # The goal is the middle now and a two-thick ring encloses it, open on one
    # side only. The route has to run the length of the board, down the far edge
    # and back in -- 1.9x the floor, and the shape is legible from the walls
    # themselves rather than from the flow field. Two cells thick because a
    # one-cell wall is a scratch once it has height; solid masses are what read.
    ring0, ring1 = 3, w - 4
    wall(g, ring0, ring0, ring1, ring0 + 1)          # top
    wall(g, ring0, ring1 - 1, ring1, ring1)          # bottom
    wall(g, ring0, ring0, ring0 + 1, ring1)          # left
    wall(g, ring1 - 1, ring0, ring1, ring1)          # right
    # The one way in, on the far side from the spawn.
    mid = h // 2
    for x in (ring1 - 1, ring1):
        g[mid][x] = "b"
    return g, (0, 1), (w // 2, h // 2)


def chambers(w=14, h=12):
    g = blank(w, h)
    # Two-cell dividers with the pinches at opposite ends, so the route has to
    # come back on itself to find the next one -- 1.4x the floor rather than the
    # monotone walk the one-cell version allowed.
    #
    # Thickness is the readability half: a single-cell divider is a scratch at the
    # iso angle and the whole board read as one open field.
    for x, gap in ((4, h - 4), (9, 3)):
        wall(g, x, 1, x + 1, h - 2)
        g[gap][x] = "b"
        g[gap][x + 1] = "b"
    return g, (0, 2), (w - 1, h - 3)


def switchback(w=12, h=11):
    g = blank(w, h)
    for i, y in enumerate(range(4, h - 3, 4)):
        if i % 2 == 0: wall(g, 1, y, w - 3, y)
        else:          wall(g, 2, y, w - 2, y)
    return g, (0, 1), (w - 1, h - 2)


def comb(w=15, h=11):
    g = blank(w, h)
    # Teeth must not meet: top stops two rows short of where the bottom starts,
    # or the comb is a wall and nothing reaches the goal.
    for x in range(3, w - 2, 4):
        wall(g, x, 1, x, h - 6)
    for x in range(5, w - 2, 4):
        wall(g, x, 5, x, h - 2)
    return g, (0, h // 2), (w - 1, h // 2)


def ringfort(w=13, h=13):
    g = blank(w, h)
    wall(g, 4, 4, w - 5, h - 5)
    lane(g, 2, 2, w - 3, 2); lane(g, 2, h - 3, w - 3, h - 3)
    lane(g, 2, 2, 2, h - 3); lane(g, w - 3, 2, w - 3, h - 3)
    return g, (0, 1), (w - 1, h - 2)


def braid(w=15, h=12):
    g = blank(w, h)
    # One thick island down the middle. The lanes passing north and south of it
    # ARE the braid, and they rejoin at both ends -- spawn and goal sit on the
    # centre line so neither lane is the obvious one.
    #
    # The previous version was a thin bar plus two stubs, which at the iso angle
    # read as a single route through an open field: no island, so no two ways
    # round it, so nothing to braid.
    wall(g, 3, 4, w - 4, 7)
    return g, (0, h // 2 - 1), (w - 1, h // 2)


def stepwell(w=13, h=13):
    g = blank(w, h)
    # Terraces two rows thick, each stepping further in than the last, so the
    # silhouette is a staircase. The one-row version left four detached strips
    # floating in the middle of the board with no read at all.
    for i in range(1, 5):
        wall(g, 2 * i, 2 * i, w - 2, 2 * i + 1)
    return g, (0, 1), (w - 1, h - 2)


def atoll(w=14, h=12):
    g = blank(w, h)
    for cx, cy in ((3, 2), (8, 2), (3, 8), (8, 8), (5, 5), (10, 5)):
        wall(g, cx, cy, cx + 1, cy + 1)
    wall(g, 5, 1, 6, 1); wall(g, 5, h - 2, 6, h - 2)
    wall(g, 11, 2, 11, 3); wall(g, 2, 5, 2, 6)
    # Corner to corner. An open island field has parallel routes of equal length,
    # so blocking one cell at a time can never bend it -- the length has to come
    # from the endpoints instead. Floor is 13 + 9 = 22, inside the band by itself.
    return g, (0, 1), (w - 1, h - 2)


def driftway(w=14, h=12):
    g = blank(w, h)
    for i in range(1, h - 1):
        x = 2 + (i * 2) % (w - 5)
        wall(g, x, i, x + 1, i)
    return g, (0, 1), (w - 1, h - 2)


MOTIFS = dict(meander=meander, spiral=spiral, chambers=chambers,
              switchback=switchback, comb=comb, ringfort=ringfort,
              braid=braid, stepwell=stepwell, atoll=atoll, driftway=driftway)


# ---- validation -----------------------------------------------------------

def stats(g, spawn, goal):
    h, w = len(g), len(g[0])
    walkable = lambda x, y: 0 <= x < w and 0 <= y < h and g[y][x] != "#"

    dist = {(goal[0], goal[1]): 0}
    q = deque([goal])
    while q:
        x, y = q.popleft()
        for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            n = (x + dx, y + dy)
            if walkable(*n) and n not in dist:
                dist[n] = dist[(x, y)] + 1
                q.append(n)

    total = w * h
    buildable = sum(r.count("b") for r in g)
    route = len(dist)
    return {
        "w": w, "h": h,
        "reachable": spawn in dist,
        "path": dist.get(spawn, -1),
        "buildable_pct": round(100 * buildable / total),
        "density": round(buildable / dist[spawn], 1) if dist.get(spawn) else 0,
        "useful": 0,
        "floor": abs(spawn[0] - goal[0]) + abs(spawn[1] - goal[1]),
    }


def stranded(g, goal):
    """Buildable cells the creeps can never reach, so no tower on them can ever
    fire. MapValidator warns about these; this script did not check for them at
    all, which is how spiral, stepwell and driftway shipped with 5, 6 and 2 of
    them. The game's validator is the authority -- a generator that passes its
    own weaker checks and then loses to MapValidator in the editor is the same
    mistake the board editor exists to avoid."""
    h, w = len(g), len(g[0])
    walkable = lambda x, y: 0 <= x < w and 0 <= y < h and g[y][x] != "#"

    seen = {goal}
    q = deque([goal])
    while q:
        x, y = q.popleft()
        for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            n = (x + dx, y + dy)
            if walkable(*n) and n not in seen:
                seen.add(n)
                q.append(n)

    return [(x, y) for y in range(h) for x in range(w)
            if g[y][x] == "b" and (x, y) not in seen]


def seal_strays(g, goal):
    """Turn walled-off buildable cells into the scenery they already are.

    Repair, not suppression: the cell is unreachable either way, so the only
    question is whether the board admits it. Painting it blocked costs a little
    buildable share and buys a board that says what it means."""
    strays = stranded(g, goal)
    for x, y in strays:
        g[y][x] = "#"
    return len(strays)


def route_of(g, spawn, goal):
    """The actual shortest route, walked from the spawn down the distance field."""
    h, w = len(g), len(g[0])
    walkable = lambda x, y: 0 <= x < w and 0 <= y < h and g[y][x] != "#"
    dist = {goal: 0}
    q = deque([goal])
    while q:
        x, y = q.popleft()
        for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            n = (x + dx, y + dy)
            if walkable(*n) and n not in dist:
                dist[n] = dist[(x, y)] + 1
                q.append(n)

    if spawn not in dist:
        return []
    cur, out = spawn, [spawn]
    while cur != goal:
        cur = min((n for n in ((cur[0] + dx, cur[1] + dy) for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)))
                   if walkable(*n) and n in dist), key=lambda n: dist[n])
        out.append(cur)
    return out


def useful_pct(g, spawn, goal, rng=2.0):
    """
    Share of buildable cells within tower range of the route.

    A map can pass every band and still be unwinnable if its buildable area sits
    away from the path -- `spiral` was 52% buildable, all bands green, and lost
    100% of 150 runs because 89 of its cells were a courtyard the creeps never
    approached. Below ~50% here is dead space, not a level.
    """
    route = route_of(g, spawn, goal)
    if not route:
        return 0
    build = [(x, y) for y in range(len(g)) for x in range(len(g[0])) if g[y][x] == "b"]
    if not build:
        return 0
    good = sum(1 for bx, by in build
               if any((bx - rx) ** 2 + (by - ry) ** 2 <= rng * rng for rx, ry in route))
    return 100 * good // len(build)


def thin(g, spawn, goal, target=52, floor_pct=37):
    """
    Add scenery until the buildable share is inside the band, preferring cells
    that lengthen the route.

    Two bands pull opposite ways on a small grid: a longer route needs more
    scenery, and more scenery eats the 35% buildable floor. Blocking blindly
    satisfies one and breaks the other -- chasing a path of 24 dropped six of ten
    maps to 26-34% buildable.

    So it runs in two passes. The first only keeps a block that **bends the
    route**, which buys route length at the lowest possible cost in buildable.
    The second fills in anywhere, and only if the share is still over target.
    Neither may cross `floor_pct`.

    Scenery, not corridor: turning route cells into path-only would also drop the
    percentage, and would do it by removing the freedom to re-route -- `gauntlet`'s
    exact failure, one solution and zero variance.
    """
    h, w = len(g), len(g[0])

    def pct():
        return sum(r.count("b") for r in g) * 100 // (w * h)

    # Pass 0: scenery over cells the route can never be defended from.
    #
    # This is the pass that makes a level viable rather than merely legal. Dead
    # buildable is worse than no buildable -- it reads as somewhere to build and
    # is not -- so it is the first thing converted, and it costs nothing the
    # player could have used.
    for y in range(1, h - 1):
        for x in range(1, w - 1):
            if g[y][x] != "b" or pct() <= floor_pct or useful_pct(g, spawn, goal) >= 60:
                continue
            route = route_of(g, spawn, goal)
            if any((x - rx) ** 2 + (y - ry) ** 2 <= 4.0 for rx, ry in route):
                continue        # within range of the route: genuinely useful
            g[y][x] = "#"
            if not stats(g, spawn, goal)["reachable"]:
                g[y][x] = "b"

    for lengthening_only in (True, False):
        for y in range(1, h - 1):
            for x in range(1, w - 1):
                if g[y][x] != "b":
                    continue
                if not lengthening_only and pct() <= target:
                    return
                if pct() <= floor_pct:
                    return

                before = stats(g, spawn, goal)
                g[y][x] = "#"
                after = stats(g, spawn, goal)

                # Blocking can only lengthen a route, never shorten it, so the
                # guard needs an upper bound only.
                bad = not after["reachable"] or after["path"] > 30
                if bad or (lengthening_only and after["path"] <= before["path"]):
                    g[y][x] = "b"


def build(name, theme):
    g, spawn, goal = MOTIFS[name]()

    # Stamp the markers BEFORE thinning. Both sit on the border wall until they
    # are stamped, so thinning saw an unreachable spawn and reverted every single
    # conversion -- silently, since reverting is its normal behaviour.
    g[spawn[1]][spawn[0]] = "S"
    g[goal[1]][goal[0]] = "G"
    thin(g, spawn, goal)
    # After thinning, not before: thinning is what walls cells off in the first
    # place, by blocking the last route into a pocket.
    s_sealed = seal_strays(g, goal)
    s = stats(g, spawn, goal)
    s["useful"] = useful_pct(g, spawn, goal)
    s["sealed"] = s_sealed

    problems = []
    if not s["reachable"]:                        problems.append("spawn cannot reach goal")
    if not 18 <= s["path"] <= 30:                 problems.append(f"path {s['path']} outside 18-30")
    if not 35 <= s["buildable_pct"] <= 55:        problems.append(f"buildable {s['buildable_pct']}% outside 35-55")
    if s["floor"] > 30:                           problems.append(f"spawn-goal {s['floor']} over 30")
    if s["useful"] < 50:                          problems.append(f"only {s['useful']}% of buildable is near the route")
    # Backstop. seal_strays should have emptied this; if it has not, sealing
    # opened a new pocket and the map must not be written.
    left_over = stranded(g, goal)
    if left_over:                                 problems.append(f"{len(left_over)} buildable cells still walled off")

    doc = {
        "id": name, "theme": theme, "version": 1,
        "width": s["w"], "height": s["h"],
        "cells": ["".join(r) for r in g],
        "spawns": [{"x": spawn[0], "y": spawn[1]}],
        "goal": {"x": goal[0], "y": goal[1]},
        "startingGold": 300, "startingLives": 20,
        "meta": {"author": "make-example-levels", "motif": name},
    }
    # Omitted when the board offers everything: an absent field and a full list
    # are different statements once a third tower exists.
    if name in ROSTERS:
        doc["towers"] = ROSTERS[name]
    return doc, s, problems


def main():
    print(f"{'level':11} {'theme':11} {'size':7} {'path':5} {'build%':7} {'useful':7} {'density':8} verdict")
    ok = 0
    pending = []
    for name, theme in LEVELS:
        doc, s, problems = build(name, theme)
        verdict = "ok" if not problems else "; ".join(problems)
        # Say when the map was repaired on the way out. A silent repair is how
        # the generator and MapValidator drifted apart in the first place.
        if s["sealed"]: verdict += f"  (sealed {s['sealed']} walled-off cell{'s' * (s['sealed'] != 1)})"
        if not problems:
            ok += 1
            pending.append((name, doc))
        print(f"{name:11} {theme:11} {s['w']}x{s['h']:<4} {s['path']:<5} "
              f"{s['buildable_pct']:<7} {str(s['useful'])+'%':<7} {s['density']:<8} {verdict}")
    # All or nothing. A partial write leaves the previous run's files on disk
    # beside this one's, and the map report then describes a set that was never
    # generated together.
    if ok != len(LEVELS):
        print(f"\n{ok}/{len(LEVELS)} valid -- nothing written")
        return
    for name, doc in pending:
        with open(os.path.join(HERE, name + ".json"), "w") as f:
            json.dump(doc, f, indent=2); f.write("\n")
    print(f"\n{ok}/{len(LEVELS)} written")


if __name__ == "__main__":
    main()
