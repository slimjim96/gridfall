# Glossary

Stable reference. Load it when a term in a spec is doing work you can't pin down.

## Station defense domain

| Term | Meaning in Gridfall |
|---|---|
| **Visitor** | A single visitor unit walking the grid. Sim-side it is an index into entity arrays, not an object. |
| **Wave** | One scripted group of visitors, defined by a wave table entry. Waves are numbered from 1. |
| **Leak** | A visitor reaching the goal. Leaks cost lives. **Leak rate** is leaks ÷ visitors spawned. |
| **Lane** | A spawn→goal corridor. A map may have several; they can merge. |
| **Maze / mazing** | Placing stations to lengthen the path rather than to block it. Gridfall supports it — see the never-fully-blockable rule. |
| **Dirty grid** | The walkable grid changed this tick, so pathing must recompute. The only trigger for a recompute. |
| **Flow field** | One pass over the whole grid producing a per-cell "step this way" direction. Replaces per-visitor A*. See ADR-0003. |
| **Acquisition** | Choosing a target for the tick — a station picking a visitor, or a sapper picking a station. Deterministic: fixed priority rule, ties broken by entity id. |
| **Structure health** | A station's `hp`. Stations are destructible; at zero the station is removed and its cell frees. Large relative to per-hit damage because it is measured against cumulative attack throughput, not one hit. |
| **Sapper** | The archetype that attacks stations while walking. Shorthand for any visitor with `attackDamage > 0`; `0` is the default, so every other visitor ignores stations. |
| **Attrition** | Defence lost to destruction rather than spent. The reason *stations built* and *stations standing* are now different numbers. |
| **Gold curve** | Gold held over time across a run. A balance target, not a knob. |
| **Time-to-clear** | Ticks from wave start to the last visitor of that wave dying or leaking. |
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
| **`SimEvent`** | An ordered, tick-stamped fact the view can react to ("visitor died", "build rejected"). |
| **Command** | Player intent queued into the sim, applied at phase 1 of the next tick. |
| **`SimStateView`** | The read-only façade the renderer gets. No setter, no arrays out — the Core/View boundary as a compile-time fact. |
| **`MutableState`** | The writable state, `internal` to Core, visible only to the test suite and the harness. Never to the view. |
| **`TraceRoute`** | Walks the flow field from a cell to the goal into a caller-provided span. Drives the route overlay. |
| **`appetiteGrowth`** | Per-wave HP multiplier, compounded at load. Without it later waves cannot be harder. |
| **`MapDraft` / `MapValidator`** | The mutable map being edited, and the single verdict on whether a map is legal — shared by the editor and the loader. |
| **Play policy** | The scripted "competent beginner" that drives the balance sim. Its numbers are a floor on difficulty, not a verdict. |
| **Phase** | One of the nine ordered steps inside a tick. Knowing yours is most of knowing you're correct. |
| **Slot vs. id** | An entity's id is stable for life; its slot is where it currently sits in the arrays and changes on death. Iterate by id. |

## Assets and tools

| Term | Meaning |
|---|---|
| **Placeholder** | Procedural C# geometry standing in for real art. Hour budget, distinct silhouette, deleted on replacement. |
| **Style anchor** | The verbatim block at the top of every Ludo.ai prompt. Paraphrasing it is how a set drifts. |
| **Prompt set** | One file per asset: sprite form, mesh form, every animation clip, iteration log. The durable artifact. |
| **`IUnitView`** | The interface placeholders, sprites, and meshes all sit behind (ADR-0004). |
| **Playtest (editor)** | `F5` in the board editor: run the unsaved map with a test wave, `Esc` to return. |

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
