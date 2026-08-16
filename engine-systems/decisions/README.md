# Architecture Decision Records

One file per decision. Numbered sequentially, never renumbered, never deleted — superseded.

Write one when reversing the choice later would be expensive. See
`../../workflows/cross-cutting/architecture-decision-record.md`.

| # | Decision | Status | Date |
|---|---|---|---|
| [0001](ADR-0001-core-view-boundary.md) | Keep the simulation in a Godot-free `net8.0` core | accepted | 2026-08-06 |
| [0002](ADR-0002-fixed-point-arithmetic.md) | Use Q16.16 fixed-point for all simulation math | accepted | 2026-08-06 |
| [0003](ADR-0003-flow-field-pathfinding.md) | Use a flow field rather than per-unit A* | accepted | 2026-08-06 |
| [0004](ADR-0004-view-asset-abstraction.md) | Put one view interface behind both sprite and mesh assets | accepted | 2026-08-06 |
| [0005](ADR-0005-pin-godot-4-6-3-mono.md) | Pin Godot to 4.6.3 mono | accepted | 2026-08-06 |
| [0006](ADR-0006-enemy-attacks-in-phase-five.md) | Resolve enemy attacks in phase 5, not a new phase | accepted | 2026-08-07 |
| [0007](ADR-0007-repair-bounds-validated-at-load.md) | Validate the repair cost bound at content load | accepted | 2026-08-07 |
| [0008](ADR-0008-active-wave-as-commanded-state.md) | Make the active wave hashed state, written by a command | **proposed** | 2026-08-15 |

ADR-0003 was promoted out of the worked example when `core-foundation` implemented it. The example copy
is kept as teaching material and points here.
