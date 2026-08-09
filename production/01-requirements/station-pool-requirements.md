# Station Pool — Requirements

**Slug:** `station-pool` · **Status:** ready · **Owner:** design-lead · **Date:** 2026-08-09

## In One Sentence

The roster grows from two stations to ten, spread across four axes rather than one, and an advanced
board offers all ten while an early board offers three.

## Why now

Asked for directly: *"up to 10 options in the last advanced boards … some catapult like or other things
slower but stronger faster but weaker."*

The instinct is right and the game currently cannot express it. **Slower-but-stronger is not a
different station in Gridfall today — it is a worse one.** Measured 2026-08-09 across all twelve maps:
the cheap fast station is the correct buy on every board, in every wave, in every run, and the burst
station was never bought once. See
[policy fussiness](../../content-data/docs/reports/2026-08-09-policy-fussiness-balance.md).

## The load-bearing constraint

> **A speed/power roster needs a resistance spread in the *visitor* roster, or nine of the ten stations
> are dominated.**

`fussiness` is the only mechanic that makes few-big-hits beat many-small-hits, and it is inert: the
crossover is at average fussiness **4** weighted by appetite, and the most armoured wave in any shipped
table averages **1.53**. Ten stations along that one axis today would be **ten stations where the
cheapest wins**, which is precisely the pillar-5 failure ("the ninth station that is the third station
with more DPS").

**So this slice is not "add eight stations."** It is "add four axes, and give each one a visitor trait
that asks for it." Two of the four already have their counterpart shipped.

## Pillar Check

| Pillar | | Note |
|---|---|---|
| 1 · The maze is the game | **Supports** | The `anchor` role exists only to shape the route. Ten footprints to place is ten more ways to shape one |
| 2 · Legible at a glance | **Fights — and this is the real cost** | Ten silhouettes that stay distinct at wave density, in a palette where the roster already owns most of the warm spectrum (`board-themes-direction.md` §The constraint). Resolve `themed-unit-palettes` **before** the tenth station, not after |
| 3 · Deterministic, therefore fair | Neutral | Four new rules, all integer, all in the tick order. Nothing reaches for a clock |
| 4 · Every loss is explainable | **Fights, if done carelessly** | Auras and chains are the classic silent-damage offenders. Any support effect must be drawn, not merely computed |
| 5 · Small numbers, big decisions | **Supports only under the constraint above** | Ten stations on four axes is "combinations matter". Ten on one axis is "forty that differ by a stat line" with the serial numbers filed off |

## TD Checklist

| Question | Answer |
|---|---|
| **Player fantasy** | Having a toolbox, and picking the right tool because the board told you which. Not "unlocking better guns" |
| **Pathing** | Every station blocks, so ten footprints is ten route shapes. `anchor` is a maze tool with a damage stat of zero |
| **Economy** | Ten prices is a real spending curve for the first time. Also the first place a wrong buy can be *punished* rather than merely suboptimal |
| **Wave pressure** | Waves gain a second job: each must ask for a role. A wave that asks for nothing is a wave any station answers |
| **Failure state** | Building the wrong ten. Bringing rate into an armoured wave, or burst into a swarm, and watching it not work |

## The ten, by role

Named by **role**, never by prop — the theme is open and being revisited, and a roster half-named after
ferries and half after catapults is a theme decision taken by accident in ten files
([`theme-direction.md`](../../game-design/docs/theme-direction.md)).

| # | Role | The axis it sits on | What asks for it | New rule needed? |
|---|---|---|---|---|
| 1 | **rapid** *(ships as `arrow-station`)* | rate | Unarmoured, numerous | — |
| 2 | **burst** *(ships as `cannon`)* | rate | `fussiness` — **needs its share raised** | — |
| 3 | **lobber** | area | Visitors arriving in tight groups | **splash** |
| 4 | **sweeper** | area | Swarms of low-appetite visitors (`mite`) | **splash** (shared with 3) |
| 5 | **slower** | control | Fast visitors, and long routes | **speed modifier** |
| 6 | **anchor** | maze | `sapper` — it attacks the nearest station, so a cheap tanky one is bait | — |
| 7 | **longshot** | reach | Winding routes where one station covers several legs | — |
| 8 | **chain** | area | Dense single-file lines — the shape mazing *creates* | **chain** |
| 9 | **support** | force multiplier | A saturated board with nowhere left to build | **aura** |
| 10 | **toll** | economy | Nothing — it is the choice *against* defence | **income** |

Roles 1, 2, 6 and 7 need **no engine work at all**. Roles 6 and 7 are the cheapest real additions in
the game: `sapper` and winding routes are already shipped and already ask for them.

## Constraints

1. **A station must name what asks for it.** "Slower but stronger" is an axis, not a justification. If
   no visitor trait or route shape makes a station correct, it is a stat variant and is rejected.
2. **Ship in axis pairs, never ten at once.** Each pair is one balance problem; ten at once is one
   unattributable one. Eleven balance passes were needed to balance one map with two stations.
3. **No station's stat line may dominate another's at every fussiness value.** Checkable: the crossover
   must exist inside the shipped range.
4. **Availability is data on the map**, using the per-board roster that already exists and that
   `CommandSystem` already enforces. No new mechanism.
5. **An advanced board offers up to 10; an early board offers 3.** Availability is a difficulty
   progression, not a theme deck — which *changes* `board-themes-direction.md`, see below.
6. Every new rule is integer, deterministic, hashed, snapshotted, and in the documented tick order.
7. Any effect a station has on another unit is **drawn**. Pillar 4 does not survive a silent aura.

## Acceptance Criteria

1. Ten station defs load, and each names its role and the trait that asks for it.
2. For every pair of stations there exists a fussiness value in `[0, 11]` at which each is the better
   buy — **no station is dominated at every point in the shipped range**.
3. `Verify balance` reports a `station mix` in which **no single station exceeds 70% of builds** on an
   advanced board — the target `balance-targets.md` has carried since it was written and which reads
   100% today.
4. At least **4 of the 10** appear in a winning run on an advanced board, meeting the roster-share
   target that has never been met.
5. An advanced board offers 10 and an early board offers 3, enforced by `CommandSystem`, refused
   visibly when violated.
6. Each new rule has a test that fails if the rule is removed, and the determinism trace is re-recorded
   once, deliberately, with the divergence explained.
7. No two of the ten share a silhouette at peak wave density — checked in-engine, by a human, at the
   density the design calls for and not at wave 3.
8. `PlayPolicy` buys more than one station type on an advanced board without further changes to the
   harness. If it does not, criterion 2 is not really met.

## What this changes upstream

**`board-themes-direction.md` §The decision that shapes everything downstream** chose *shared pool of
~8, each theme offers a different 5*. This requirement makes availability a **difficulty curve** (3
early → 10 late) rather than a theme deck. Those are compatible — a late desert board and a late ocean
board can still offer different tens — but the accepted wording says "theme becomes a deck choice" and
that is no longer the primary axis. **That file needs a decision recorded, not a silent edit.**

Also unresolved and now blocking: **`themed-unit-palettes`.** The direction file already says decide it
*before* `station-pool` ships. Ten stations in a palette that already owns most of the warm spectrum is
exactly the collision it predicted.

## Sequencing

| Wave | Roles | New rules | Why this order |
|---|---|---|---|
| 0 | — | — | **Raise `fussiness` share first.** Until burst is ever correct, nothing downstream can be measured |
| 1 | `anchor`, `longshot` | none | Free. Both counterparts are already shipped; proves the availability curve with zero engine risk |
| 2 | `lobber`, `sweeper` | splash | One rule, two stations, one balance problem |
| 3 | `slower` | speed modifier | Touches movement — the most determinism-sensitive system in the game |
| 4 | `chain`, `support` | chain, aura | The two with pillar-4 risk. Drawn before they are shipped |
| 5 | `toll` | income | Last, because it changes the economy every earlier measurement was taken against |

**Wave 0 is not optional and is not part of this slice.** It is a content decision already priced and
sitting in [`next-steps.md`](../docs/next-steps.md) §1, and it belongs to the human. Wave 1 can proceed
in parallel; waves 2–5 cannot be *measured* until it lands.

## Handoff

To `engine-systems` for waves 2–5 (four rules, four architecture notes, at least one ADR — splash and
chain both need a targeting-set decision). Wave 1 goes straight to `content-data`: it is two JSON files
and a roster edit.
