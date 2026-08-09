# Play Policy — Fussiness-Aware Buying — Note

**Status:** review · **Date:** 2026-08-09 · **Slug:** `policy-fussiness`
**Touches:** `Gridfall.Verify/PlayPolicy.cs`, `Gridfall.Verify/VisitorCensus.cs`,
`Gridfall.Verify/Program.cs`, `Gridfall.Core/Content/Defs.cs`, `Gridfall.Core/Systems/DamageSystem.cs`

## Friction It Removes

`PlayPolicy` ranked stations on **base** serving-per-gold. Two consequences, both measurable:

- **The cannon was bought 0 times in every balance run ever recorded.** Across twelve maps ×
  150 runs × ~200 builds, the station mix was `arrow-station 100%, cannon 0%`. Half the roster was
  never exercised, and no line of any report said so.
- **Every balance number in the repo therefore described a one-station game** — including the ones
  used to tune wave tables, `hpGrowth`, and `waveClearGold`.

Fussiness is subtracted **per hit**, so "best value" is not a property of a station. It is a property
of a station *against a mix of visitors*, and the ordering inverts inside the shipped roster:

| serving per second per gold | arrow (12 / 0.6 s / 50 g) | cannon (40 / 1.5 s / 90 g) |
|---|---|---|
| fussiness 0 | **0.400** | 0.296 |
| fussiness 4 | 0.267 | 0.267 |
| fussiness 8 (`husk`) | 0.133 | **0.237** |

## What It Reuses

| Game code | Instead of |
|---|---|
| `VisitorDef.ServingTaken(amount)` — **new, and now the one authority** | a second `max(1, amount - fussiness)` in the harness |
| `MapDef.Offers(content, i)` — the roster check `CommandSystem` enforces | a harness-side idea of what a board sells |
| `ContentSet.Waves` (only indices `< WaveIndex`) | a hand-kept table of what has spawned |
| the real `Sim`, driven by real `BuildCommand`s | a closed-form model of what a station is worth |

`DamageSystem` now calls `ServingTaken` too, so the simulation and the harness cannot drift about what
a hit does. `replay` passed unchanged, which is the proof that refactor was behaviour-identical.

## Two Changes, and Either Alone Does Nothing

1. **Value is census-relative.** The policy weights `max(1, serving - fussiness)` by the appetite of
   every visitor in every wave that has **already started**.
2. **The policy will not substitute down.** It used to buy the best station it could afford *this
   tick*. On any roster that means the cheapest station is bought the instant its price is reached and
   gold never approaches the price of anything else — a **structural** block, independent of fussiness.
   With a 50 g station on the board, the 90 g one is unreachable no matter what the census says.

Change 1 alone leaves the policy still buying arrows (measured: 2 arrows, 0 cannons on a pure-husk
board). Change 2 alone has nothing to prefer. Both together flip it.

The wait introduced by (2) is self-limiting: holding makes `TryBuild` fail, which is what pulls the
next wave, which is what pays for the station. Cost: up to `price - 1` gold can sit across one wave.

## The Honesty Boundary — Memory, Not Preview

The census counts only waves with index `< SimState.WaveIndex`, i.e. waves already **started**.

This is the whole defensibility of the class. **The game shows no wave preview** — `WaveCountdown`
prints `wave N incoming` and nothing about its composition — so a policy weighting against the *next*
wave would be reading `content-data/waves/*.json`, which no player can do. Weighting against waves
already met is memory, which every player has. Mid-wave the running wave *is* counted, because the
player can see it on the board.

## Scope Added / Explicitly Not Added

**Added:** `VisitorCensus`; a `station mix` line in `Verify balance`; a per-wave `srv/gold` column in
`Verify curve` (it printed one constant computed from base stats, which was the same blind spot).

**Not added:**

- **Counter-buying.** The policy buys against the *average* of what it has met. A real player buys
  against the thing that is *leaking*, which needs leak attribution the harness does not have. The
  average is what a beginner does, and beginner is the stated bar.
- **Fussiness in the upgrade choice.** `TryUpgrade` still ranks by coverage. Upgrades multiply
  serving, so fussiness matters there too, but coverage is the beginner-legible signal.
- **Saving up in general.** The policy holds for the station it has *chosen*, never for a better one
  it has not.

## Release Exclusion

Not applicable — nothing under `godot/Dev/` was touched. `Gridfall.Verify` is a headless CLI and has
never been part of a game export; `Gridfall.Tests` now project-references it, which is a test-time
reference only.

## What a Human Must Try

Nothing UI-facing changed, so there is no unverifiable claim here. What is worth reading:

1. `dotnet run --project Gridfall.Verify -- balance --map crossroads --runs 150` — the new
   `station mix` line. It reads `arrow-station 100%, cannon 0%` on every shipped board, and that is
   the finding, not a bug.
2. `dotnet run --project Gridfall.Verify -- curve --map crossroads` — `srv/gold` falls 0.01333 →
   0.01163 across the table. That 13% is the entire measurable footprint of fussiness on the shipped
   content.
3. The decision it hands back: see the balance report's *What this leaves for a person*.
