# Route Overlay — v1

**Slug:** `route-overlay` · **Status:** done

## What Shipped

The board draws the route creeps take, and the route they **would** take if you built on the cell under
the cursor. `r` toggles it.

Pillar 1 says the maze is the game; until now you placed a tower and inferred the consequence by
watching creeps afterwards. Now you see it before committing, which is the difference between a puzzle
and a guess.

The preview calls `PathSystem.WouldRemainConnected` — the same call the simulation makes in phase 1, on
the same scratch buffers — so what you see while hovering and what the sim does on release cannot
disagree. They never contend: the check is phase 1, a hover query is between frames.

New in Core: `TraceRoute` (walks the field into a caller-provided span, allocation-free, step-capped at
the cell count so a malformed field costs a bounded walk rather than a hang), `PreviewFlowAt`,
`PreviewDistanceAt`, `RouteLength`.

## Player-Facing Change

You can see what a tower will do to the route before you buy it.

## New Tuning Knobs

| Knob | Owner | Default set? |
|---|---|---|
| `RouteLive` / `RoutePreview` palette slots | presentation | Yes — tuned against a captured frame |

## Follow-Ups Not Done

| Item | Workspace | Slug |
|---|---|---|
| The sealing-hover refusal is unreachable on `crossroads` — no cell there can seal a lane | content-data | `pinch-map` |
| Whether always-on live routes are the right default is a taste call | presentation | — |

## Known Not Verified

- **The sealing-hover refusal.** Wired and tested at Core level, but no shipped map has a sealable cell,
  so the red path cannot be reached in the real game today.
- **Readability at wave-18 density.** Still assessed at four creeps.

## What Closed on the Way Past

`PathSystem.SetBlocked`, `MarkDirty`, `ForceRebuild`, `RecomputeIfDirty`, and `RestoreFrom` were all
**public** — the renderer could have dirtied or rebuilt the flow field and desynchronised itself. All
five are now `internal`. `SimStateView` closed half the Core/View boundary; this closed the rest.
