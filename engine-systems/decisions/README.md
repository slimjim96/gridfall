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

ADR-0003 was promoted out of the worked example when `core-foundation` implemented it. The example copy
is kept as teaching material and points here.
