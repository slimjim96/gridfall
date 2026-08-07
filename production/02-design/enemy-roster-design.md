# Enemy Roster — Design

**Slug:** `enemy-roster` · **Status:** done

## The problem, stated precisely

Waves 5–11 leak nothing, and three passes have failed to fix that from the economy side. The reason is
not quantity:

> **A defence of 55 arrow towers cannot be threatened by any number of enemies that all die to arrow
> towers.**

Every extra creep is also extra bounty, which becomes another arrow tower. The loop closes on itself.

## Why "add two more archetypes" is the wrong instinct

The balance target asks for 4–7 archetypes and we have 2, so the obvious move is to author two more.
But `runner` and `brute` differ only in HP and speed, and a third and fourth stat-variant would change
nothing — the same towers would still answer all of them.

Pillar 5 rejects "the ninth tower that is the third tower with more DPS". **The same rule applies to
enemies.** An archetype earns its slot by asking a question the others do not.

So this slice is one mechanic plus a roster built around it, not four stat lines.

## The mechanic: armour

Flat damage reduction per hit. `effective = max(1, damage - armour)`.

Flat, not percentage, because flat is what makes it a *choice*:

| Tower | Damage | vs armour 8 | Effective |
|---|---|---|---|
| Arrow | 12 | 12 − 8 | **4** — a third as good |
| Cannon | 40 | 40 − 8 | **32** — barely notices |

A percentage reduction would scale both towers equally and change no decisions. Flat reduction
punishes many-small-hits and rewards few-big-hits, which is precisely the axis the current roster
cannot express.

**Never below 1.** An enemy immune to a tower is a soft-lock waiting to happen, and "my towers do
literally nothing" is not a readable failure (pillar 4).

## The roster — what each one asks

| Archetype | HP | Speed | Armour | The question it asks |
|---|---|---|---|---|
| **runner** | 60 | 0.06 | 0 | *Can you cover the route?* The baseline. |
| **brute** | 220 | 0.03 | 0 | *Do you have enough total damage?* A slow wall of HP. |
| **husk** | 120 | 0.04 | **8** | *Do you have burst?* Punishes a board of cheap fast towers. The answer is cannons. |
| **mite** | 18 | 0.10 | 0 | *Do you have enough coverage?* Fast and numerous — single-target towers cannot keep up, and it is gone before a slow tower cycles. |

Husk and mite pull in opposite directions: husk wants few big hits, mite wants many small ones. A board
optimised for either is punished by the other, and **that tension is the point** — it is the first
thing in the game that makes a 55-tower monoculture a bad answer.

## Player-Visible States

Silhouette first, per pillar 2 and the placeholder standard. No two share a shape:

| Archetype | Placeholder silhouette |
|---|---|
| runner | low sphere |
| brute | broad box |
| **husk** | **squat hexagonal drum** — heavy and plated |
| **mite** | **small stacked cones** — reads as a swarm unit |

Armour is not separately signposted yet. It is carried by the husk's identity, and a dedicated cue is
follow-up work once someone has played against it.

## Tuning Knobs

| Knob | Raising it… | Expected direction |
|---|---|---|
| `armour` | Widens the gap between burst and rapid towers | Careful — past a tower's damage it stops being a choice and starts being immunity |
| `mite` speed | Shortens time-in-range | Bounded by the 0.6–1.8× spread target |
| `mite` count per wave | Rewards coverage over concentration | Up, if single-target towers stay dominant |

## Interaction Rules

- **Armour vs the damage buffer:** applied per damage record as it is drained in phase 7, not to the
  total. Two 12-damage hits against armour 8 deal 4 + 4, never 24 − 8 = 16. Per-hit is what makes
  rapid-fire towers weak, which is the whole design.
- **Armour vs HP scaling:** independent. `hpGrowth` multiplies HP; armour is flat and does not scale.
  That is deliberate — an armour value that grew with waves would become immunity.
- **Armour vs the minimum:** every hit deals at least 1, so no tower is ever useless, only inefficient.

## Rejection Cases

None — this adds no new player input.

## Acceptance Criteria

1. An enemy with armour takes `max(1, damage − armour)` from each hit.
2. Armour applies **per hit**, not to a tick's total.
3. A hit never deals less than 1 damage, whatever the armour.
4. Armour defaults to 0, so existing content is unaffected.
5. Four archetypes exist and none shares a silhouette with another.
6. Determinism holds: identical inputs, identical hashes.
7. The husk is meaningfully harder for arrow towers than for cannons — provable from the numbers.
8. Late-game waves stop leaking zero.
