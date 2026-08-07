# Board Editor — How To Use It

Step by step. The [spec](board-editor-spec.md) says what it *is* and why; this says what to press.

---

## 1. Launch it

### From a terminal (recommended)

```bash
# a blank 20x12 map
godot-mono --path ~/projects/claude/gridfall/godot --scene res://Dev/BoardEditor.tscn

# or open an existing map to edit
godot-mono --path ~/projects/claude/gridfall/godot --scene res://Dev/BoardEditor.tscn -- --map crossroads
```

The `--` matters. Everything before it is for Godot; everything after is for the game. Without it,
Godot tries to interpret `--map` itself and ignores it.

`godot-mono`, never `godot` or `godot-4` — those are 4.7 on this machine and the project is pinned to
4.6.3 (ADR-0005). A **non-mono** build is worse: it loads the project, silently ignores every C#
script, and shows you an empty window that looks like a broken game.

### From inside the Godot editor

1. Open the project: `godot-mono --path ~/projects/claude/gridfall/godot --editor`
2. In the **FileSystem** dock (bottom left), open `Dev/` and double-click `BoardEditor.tscn`
3. Press **F6** (*Run Current Scene*), or the ▶ with a clapperboard icon, top right

> Launched this way there is **no `--map` argument**, so you always get a blank map. To edit an
> existing one, use the terminal command above.

### What you should see

A board, a status line top-left reading `board editor`, a brush line under it, and a validation panel
below that. If the window is black, the scene did not load — check the terminal for a C# exception.

---

## 2. Make a map

The editor starts you with a legal blank map: a walled 20×12 rectangle, a spawn on the west edge, a
goal on the east, and a clear lane between them. It is valid from the first frame on purpose, so you
are never starting from an error.

**Pick a brush** with the number keys, then **left-drag** to paint:

| Key | Brush | What it means |
|---|---|---|
| `1` | **Buildable** | Creeps walk it *until* you build there. This is where mazing happens. |
| `2` | **Path-only** | Creeps walk it, you can never build on it. Use it to force a corridor. |
| `3` | **Blocked** | Permanent scenery. Never walkable, never buildable. |
| `4` | **Spawn** | Where creeps enter. You can have more than one. |
| `5` | **Goal** | Where they are heading. Placing a new one *moves* the old one. |

**Right-drag** erases back to buildable. `[` and `]` switch between a 1×1 and 3×3 brush — the 3×3 is
ignored for spawn and goal, since placing nine goals would be nonsense.

**Wheel** zooms. `Ctrl+Z` / `Ctrl+Shift+Z` undo and redo, 50 steps deep, one step per *stroke* rather
than per cell.

### A sensible first map

1. Press `3` and paint a vertical wall down the middle, leaving one gap
2. Watch the route overlay bend through the gap as you close it
3. Press `1` and paint buildable pockets either side of the gap
4. Press `F6` and see what mazing potential you created

---

## 3. Read the validation panel

The panel updates on **mouse-up**, not while you drag. Three severities:

| | Blocks save? | Where it comes from |
|---|---|---|
| **ERROR** (red) | **Yes** | The game's own loader. If it is an error here, the game refuses to load the map. |
| **warn** (amber) | No | The balance targets in `content-data/docs/balance-targets.md`. |
| plain | No | Metrics — size, buildable %, path length, spawn count. |

Errors also **outline the offending cell in red on the board**, so you can see where the problem is
rather than only reading about it.

The editor has no opinions of its own. Every error is the game's verdict shown earlier, which is why
the editor can never save a map the game would reject.

**Warnings never block anything.** An unusual map is often a deliberate one.

### The errors you will actually hit

| Message | What happened |
|---|---|
| `map has no goal` | You painted over the goal. Press `5` and place a new one. |
| `spawn cannot reach the goal` | You walled a spawn off. The offending spawn is outlined. |
| `map has N goals` | Should be impossible — placing a goal moves the old one. Report it if you see it. |

---

## 4. Check the mazing — `F6`

`F6` estimates how much a player could lengthen the route by building. It prints something like:

```
maze estimate: 2.4x (greedy lower bound, target <= 3x)
```

**It is a lower bound, not the answer.** Finding the true worst case is a search problem, so the editor
greedily blocks whichever cell lengthens the path most, repeatedly, and reports what it managed.

- Over 3× → the map **fails** the target. That is certain.
- Under 3× → **proves nothing**. The real worst case may be higher.

It takes a second or two on a large map, which is why it is a keypress and not automatic.

---

## 5. Playtest — `F5`

`F5` runs your map **right now**, unsaved, with a built-in test wave. `Esc` returns you to the editor
with the draft exactly as you left it.

Playtesting an unsaved map is the whole point — do not save first just to try something.

It uses the real simulation, the real loader, and the real renderer. If a map crashes the loader in
playtest, it would have crashed the game.

Refused if the map has errors, because the game could not load it either.

---

## 6. Save — `Ctrl+S`

Writes to `content-data/maps/<id>.json`.

Save **runs the validator first and refuses on any error**, showing you the validator's own message.
On success the status line confirms it and the `*` next to the map name disappears.

The written file is the same rows-of-strings format a human would hand-write, so it diffs readably in
git.

> **The map id is currently whatever it was loaded as, or `untitled` for a new map.** There is no
> rename UI yet — to name a new map, save it and then rename the file, or start from a copy. Tracked
> as `editor-resize-ui` / naming follow-ups.

---

## 7. Use your map in the game

Two things are needed before a new map is playable:

1. **A wave table.** `content-data/waves/<mapid>.json`. Copy `crossroads.json` and edit it — there is
   no wave editor, deliberately.
2. **Point the game at it.** `godot/GameplayScene.cs` has `private const string MapId = "crossroads";`.
   Change it, or use the editor's `F5` playtest, which needs neither.

Then check the geometry from the terminal:

```bash
dotnet run --project Gridfall.Verify -- maps
```

---

## Every key, in one place

| Key | Does |
|---|---|
| `1` `2` `3` `4` `5` | Brush: buildable / path-only / blocked / spawn / goal |
| Left drag | Paint · Right drag | Erase to buildable |
| `[` `]` | Brush size 1×1 / 3×3 |
| Wheel | Zoom |
| `Ctrl+Z` / `Ctrl+Shift+Z` | Undo / redo (50) |
| `Ctrl+S` | Save · `Ctrl+N` New |
| `F1` | Toggle this key list on screen |
| `F2` | Toggle the route overlay |
| `F3` | Toggle the validation panel |
| `F5` | Playtest · `Esc` back |
| `F6` | Maze estimate |
| `Esc` | Quit (from the editor) |

---

## Troubleshooting

| Symptom | Cause |
|---|---|
| `Couldn't load file 'project.binary'` | You pointed `--path` at the repo root. It must be `.../gridfall/godot`. |
| Window opens, board is blank/black | Scene failed to load. Read the terminal for a C# exception. |
| Nothing happens, no window, no error | No display. Over SSH you need X forwarding; on this VM, connect over **RDP** first — the display belongs to that session. |
| `--map` seems ignored | Missing the `--` separator, or you launched from inside the Godot editor. |
| Scripts do nothing at all | You used a **non-mono** Godot. Use `godot-mono`. |
| `mkdir: cannot create directory '/run/user/0'` | You used `sudo`. Don't — it also risks root-owned files in the project. |

## Not built yet

Named honestly so you do not go looking:

- **No resize UI.** `MapDraft.Resize` exists and is tested; nothing is wired to it.
- **No open dialog.** Use `--map <id>` at launch.
- **No rename.** Save, then rename the file.
- **No wave editing.** Out of scope by decision — wave tables stay hand-authored JSON.
