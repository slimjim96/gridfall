# The Gridfall Engine — Developer Guide

The manual for working inside `Gridfall.Core`. Written for a developer who has to change something and
needs to know what will break.

> **Status: this documents the engine as specified, not as written.** No code exists yet. Until it
> does, this guide *is* the engine spec — the thing implementations are checked against. Once the code
> exists, this becomes the manual, and any divergence between the two is a bug in one of them. Say
> which one when you find it.

## Read in this order, first time

| # | Chapter | You'll know |
|---|---|---|
| [01](01-orientation.md) | Orientation | What the projects are, how to run the thing, the mental model |
| [02](02-tick-loop.md) | The Tick Loop | The nine phases, what belongs in each, what may not happen where |
| [03](03-fix32.md) | Fix32 Arithmetic | How to do math here without breaking determinism |
| [04](04-state-and-entities.md) | State & Entities | How entities are stored, what the state hash covers |
| [05](05-commands-and-events.md) | Commands & Events | The only two ways across the Core/View boundary |
| [06](06-pathing.md) | Pathing | The flow field, the dirty flag, the block check |
| [07](07-content-loading.md) | Content Loading | JSON defs → runtime data, and the map format |
| [08](08-determinism-playbook.md) | Determinism Playbook | What to do when a trace diverges |

## Reach for these when you're doing the thing

| Recipe | When |
|---|---|
| [09 · Add a system](09-recipe-new-system.md) | New simulation behavior that needs its own tick phase slot |
| [10 · Add a tower](10-recipe-new-tower.md) | End to end: data, behavior, view, placeholder, prompts |

## The three sentences that matter most

1. **`Gridfall.Core` has no Godot in it, no floats, and no clock.** Everything else follows from that.
2. **The tick order is nine fixed phases**, and knowing which phase your code runs in is the difference
   between working code and a determinism bug that shows up two weeks later on someone else's machine.
3. **The view reads state and queues commands.** It never writes. Not once, not for convenience.

## Related, but not here

- `../tech-standards.md` — the rules, stated as rules. This guide is the *how*; that is the *what*.
- `../iso-grid.md` — the projection contract. Presentation's concern, but Chapter 07 touches it.
- `../../engine-systems/decisions/` — the ADRs. Each chapter cites the ADR that decided it.
