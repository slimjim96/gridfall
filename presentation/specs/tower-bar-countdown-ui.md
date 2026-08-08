# Tower Bar & Wave Countdown — UI Spec

**Built:** 2026-08-08 · **Status:** placeholder art, real behaviour ·
**Baselines:** `presentation/docs/board-baseline.png`, `countdown-baseline.png`

Two HUD widgets. Both read simulation state and neither can write it — they are
`Control`s under the existing HUD `CanvasLayer`, and they queue no commands.

---

## The tower bar

One slot per tower the **board** offers, in roster order, at bottom centre.

```
        ┌─────────────────────────┐
        │  ▓▓▓▓▓      ▓▓▓▓▓       │   selected slot: 2px bright border
        │  ▓▓▓▓▓      ▓▓▓▓▓       │   chip colour = Palette.ForTower(id)
        │   50         90         │   price now, "+" while the premium applies
        │ Arrow Tower  Cannon     │
        │     1          2        │   number key = slot, not tower id
        └─────────────────────────┘
```

**Slots are portrait, 46×60, chip 32×46.** Units stand on a cell and are typically far taller than
they are wide — the shipped arrow tower is 262×662, aspect 0.40 — so a square slot spends its width on
nothing and renders 12px of tower in a 32px box. Cropping does not fix that: on an asset fitted by
`fit-sprite.sh` the content already spans the full frame height by construction, so there is no
vertical margin left to reclaim. A taller box is the only thing that makes a tall silhouette bigger.

The chip is the unit's **own first idle frame, cropped to its silhouette**, when it has sprite art, and
its palette colour when it does not — so `arrow-tower` shows the shipped tower and `cannon` shows a brown square. A flat colour
was right while every tower was a coloured solid and the swatch matched by construction; once real art
lands, an orange square beside a board of blue towers is not a missing picture, it is a wrong one.
Mesh units keep the colour chip: thumbnailing a `.glb` needs a render pass, and that is not worth a
viewport per slot yet.

| Cue | Meaning |
|---|---|
| Bright 2px border | Selected |
| Chip at 32% alpha | Not affordable right now |
| Price in amber with `+` | Mid-wave premium is in effect |
| Name in `Ink` vs `Dim` | Selected vs not |

**The roster is a rule, not a hint.** The bar asks `MapDef.Offers(content, index)`
— the same method `CommandSystem` refuses builds with. A slot on screen and a
build the sim accepts are therefore the same set by construction, which is the
only property of this widget worth protecting: a toolbar that filtered the list
itself would eventually offer a tower that gets refused, and "you can't build
that here" is the refusal a player can do least about.

**Number keys address slots, not towers.** `SelectSlot(n)` indexes the roster, so
a one-tower board has no dead `2` key, and a board that does not offer the arrow
tower still starts at `1`. Out-of-range presses do nothing rather than clamping.

**Prices come from the sim per frame.** `SelectedTowerCost` is passed in as a
delegate; the premium formula lives in `CommandSystem.BuildCost` and the view
holds no second copy of it.

### Authoring a roster

Optional `towers` array in the map JSON, in toolbar order:

```json
{ "id": "meander", "towers": ["arrow-tower"] }
```

**Absent means every tower, and that is not the same as listing them all** — an
absent field keeps whatever the tower set grows to; a listed board keeps exactly
what it names when a third tower is added. An empty array is rejected at load: a
board offering nothing is a typo every time, and it would present as an empty
toolbar with no explanation.

Rosters for generated levels live in `ROSTERS` in `make-example-levels.py`, so
they survive regeneration. `meander` ships arrow-only.

---

## The wave countdown

A ring that empties over the prep window, centred, with the seconds inside.

```
              ╭─────────╮
             ╱  ◜◜◜◜◜    ╲        arc sweeps clockwise from 12 o'clock
            │      6      │       scrim disc behind, 72% opaque
             ╲           ╱
              ╰─────────╯
            wave 1 incoming
           space to start now
```

| Property | Value | Why |
|---|---|---|
| Radius / thickness | 54 / 7 px | |
| Fade out | 300 ms | A hard cut at the moment the board gets busy reads as a dropped frame |
| Seconds | `CeilToInt` | A window with time left must never read "0" |
| Scrim | `0.03,0.05,0.07` at 72% | The ring sits over the busiest part of the frame, on twelve different palettes |
| Denominator | `WaveDef.PrepTicks` | Read from the wave def, not a remembered high-water mark — calling a wave early zeroes the counter, and a remembered maximum would start the next window part-drained |

**Pure view.** `PrepTicksRemaining` already existed in `SimState`, was already
hashed, and was already exposed on `SimStateView`. This reads and draws; nothing
here can change the simulation.

It also settles a note in `balance-targets.md`, which records `prepTicks` as *"a
FEEL knob the sim cannot measure at any value"*, left at a placeholder 300 to be
tuned by playing. It could not be tuned by playing because nothing on screen
showed it.

---

## Handing over to generated art

Both widgets are deliberately plain geometry with fixed anchors, so a generated
asset drops in without moving anything around it:

- **Bar** — the 46px slot frame and the chip inside it are separate nodes. A
  generated tower icon replaces the `ColorRect` chip; the frame, price, name and
  key stay.
- **Ring** — `_Draw` owns the scrim, track and arc. A generated dial replaces the
  whole `_Draw` body; the three labels are positioned relative to `Radius` and
  follow it.

Keep the selected/affordable/premium cues in whatever replaces them. They are the
information; the geometry is not.

---

## What a human still has to judge

Frames are captured and committed. These are the things a still frame cannot
answer:

1. Is 10s (`prepTicks: 300`) the right window, now that you can see it run down?
2. Does the ring over the centre of the board annoy you by wave 5, or stop
   registering?
3. Is "space to start now" discoverable, or does it need to be louder the first
   time?
4. On a one-tower board, does a single slot read as *"this board is simple"* or
   as *"something failed to load"*?

## Trap worth keeping

A `Control` parented to a `CanvasLayer` **does not inherit a rect** — it stays
0×0 at the origin, so anchored children lay out inside a zero box and render as
nothing at all. That is indistinguishable from the widget never having been
added, and it cost a capture to diagnose. Both widgets take their size from
`GetViewportRect()` every frame instead, which also survives a resize.
