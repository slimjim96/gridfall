# ADR-0001 — Keep the Simulation in a Godot-Free `net8.0` Core

**Status:** accepted
**Date:** 2026-08-06 · **Raised by:** project setup

## Context

Gridfall needs a simulation that can be replayed identically, run 200 times headless for balance, and
tested without a display. Godot's runtime brings a scene tree, its own `float`-based math types, its own
RNG, and a frame loop we do not control. Every one of those is a source of variation across platforms
and engine versions.

The constraints that force the question: 200-run balance sims in CI-like conditions, tick-by-tick trace
diffing as the determinism test, and a 30 Hz fixed timestep the renderer must not influence.

## Options

### A. One Godot project; sim logic in `Node`-derived classes

Simplest to start. The sim can use `Vector2`, `_PhysicsProcess`, and Godot's `RandomNumberGenerator`
directly. Everything is inspectable in the editor, and there is no marshalling between layers.

The cost is that headless runs need Godot's runtime, tests need a Godot host, and the sim inherits
Godot's float semantics and node iteration order. Determinism becomes something you hope for rather than
something you assert.

### B. `Gridfall.Core` as a plain `net8.0` class library, Godot as a consumer

The sim is ordinary C# with no engine dependency. The Godot project references it and renders its state.
Balance sims and determinism traces run as a console app in milliseconds; unit tests are plain xUnit.

The cost is a real boundary to maintain: two coordinate conversions, an event stream, a command queue,
and the discipline never to reach across.

## Decision

Chose **B**.

Deciding factor: **the determinism harness must run without Godot.** A trace diff that requires the
engine to be installed and a display to be present is a test that will stop being run, and the moment
it stops being run, pillar 3 stops being true.

This is the same split that worked on Scrap Escape, where an engine-agnostic C# core was verified
byte-identical against its original implementation across all levels.

## Consequences

### Good
- Balance sims run 200 games in seconds, in any environment, with no display.
- Determinism is assertable, not aspirational — the harness is a plain console app.
- The renderer can be replaced (2D sprites, a different engine) without touching game logic.
- Unit tests are fast and need no engine host.

### Bad
- Two representations of position: `Fix32` cell-space in Core, `Vector3` world-space in the view.
- Conversion code at the boundary that must stay correct and is easy to get subtly wrong.
- No inspecting sim state in the Godot editor's remote tree; debugging tooling is ours to build.
- New contributors will try to reach across the boundary "just this once".

### Forecloses
- Using Godot physics for anything gameplay-affecting. Collision, overlap, and targeting are ours.
- Godot resources as the authoring format for gameplay data — JSON is the source, `.tres` is generated.
