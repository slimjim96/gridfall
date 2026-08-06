# Glossary

Stable reference. Load it when a term in a spec is doing work you can't pin down.

## Tower defense domain

| Term | Meaning in Gridfall |
|---|---|
| **Creep** | A single enemy unit walking the grid. Sim-side it is an index into entity arrays, not an object. |
| **Wave** | One scripted group of creeps, defined by a wave table entry. Waves are numbered from 1. |
| **Leak** | A creep reaching the goal. Leaks cost lives. **Leak rate** is leaks ÷ creeps spawned. |
| **Lane** | A spawn→goal corridor. A map may have several; they can merge. |
| **Maze / mazing** | Placing towers to lengthen the path rather than to block it. Gridfall supports it — see the never-fully-blockable rule. |
| **Dirty grid** | The walkable grid changed this tick, so pathing must recompute. The only trigger for a recompute. |
| **Flow field** | One pass over the whole grid producing a per-cell "step this way" direction. Replaces per-creep A*. See ADR-0003. |
| **Acquisition** | A tower choosing its target for the tick. Deterministic: fixed priority rule, ties broken by entity id. |
| **Gold curve** | Gold held over time across a run. A balance target, not a knob. |
| **Time-to-clear** | Ticks from wave start to the last creep of that wave dying or leaking. |
| **Pressure** | How hard a wave pushes the player's current defense. Design language, measured by leak rate in the sim. |
| **Slice** | One unit of work moving through the production pipeline. The thing a slug names. |

## Architecture

| Term | Meaning |
|---|---|
| **Core** | `Gridfall.Core` — the deterministic simulation. No Godot, no floats, no clock. |
| **View** | The Godot layer. Reads state and events, mutates nothing. |
| **Tick** | One fixed simulation step, 33 ms. Nine ordered phases; see tech-standards. |
| **State hash** | A hash over all sim state at the end of a tick. Determinism is defined as these matching. |
| **Trace** | A recorded command stream plus the per-tick hashes it produced. The determinism harness replays these. |
| **`Fix32`** | Q16.16 fixed-point. All sim arithmetic. |
| **`SimRandom`** | The seeded PRNG. The only randomness Core may use, advanced only inside the tick loop. |
| **`SimEvent`** | An ordered, tick-stamped fact the view can react to ("creep died", "build rejected"). |
| **Command** | Player intent queued into the sim, applied at phase 1 of the next tick. |

## Method vocabulary

Inherited from the Mirror Method (`../../mirror-workflow-guide/`), which this project's layout follows.

| Term | Meaning |
|---|---|
| **Map** (Layer 1) | `CLAUDE.md`. Always loaded. Where things live. |
| **Router** (Layer 2) | `CONTEXT.md`. Read once per task. Which area owns this? |
| **Scope** (Layer 3) | A workspace's `CONTEXT.md`. What to load, what to skip, how work happens here. |
| **Workflow** | A runnable procedure in `workflows/`. The Scope says *where*; the workflow says *how*. |
| **Handoff** | A finished artifact crossing a workspace boundary, with a note. Not shared context. |
| **Injection point** | One of the four places a model call is actually warranted: route classification, branch resolution, leaf generation, gap detection. |
| **Branch resolution** | The stage-05 judgment call: did this pass, and if not, which stage does it loop back to? |
| **Gap detection** | Noticing the inputs don't determine the output, and asking one question instead of guessing. |
