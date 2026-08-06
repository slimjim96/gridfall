# Design Pillars

Five pillars. Every requirement is checked against them. A feature that fights a pillar is rejected, or
the pillar changes — and changing a pillar is a decision with a date on it, not a drift.

## 1. The maze is the game

Towers are not turrets bolted to a fixed path. Placing a tower **changes where creeps walk**. The
player's real decision is the shape of the route, and damage is the second-order effect.

*Implies:* paths recompute, mazing is supported, and a build that would fully block a lane is refused
rather than allowed and punished.

## 2. Legible at a glance

A player looking at the board for two seconds can tell what is about to go wrong. Silhouette carries
identity, color carries state, motion carries urgency — in that order.

*Implies:* no two creep archetypes share a silhouette; every player-visible state has a visible
representation; readability is checked at peak density, not at wave 3.

## 3. Deterministic, therefore fair

The same run plays out the same way. No hidden randomness decides whether you lose. When randomness
exists, it is seeded, visible in its effect, and the same for everyone.

*Implies:* the whole `Fix32`/`SimRandom`/state-hash regime in tech-standards. This pillar is the reason
the engineering standard is as strict as it is.

## 4. Every loss is explainable

When a creep leaks, the player can point at the reason. Not "the difficulty ramped" — *that* creep, on
*that* route, past *that* gap.

*Implies:* failure states are designed, not emergent; damage and pathing are inspectable; the HUD
answers "why did that get through" without a wiki.

## 5. Small numbers, big decisions

Few towers, few enemies, deep interactions. Gridfall would rather have eight towers whose combinations
matter than forty that differ by a stat line.

*Implies:* a new tower must justify itself against the two it most resembles. "It's a faster version of
X" is a rejection, not a pitch.

---

## Using these in requirements analysis

For each pillar, the requirements file answers: **supports / neutral / fights**. Neutral is common and
fine. "Fights" is not fatal — it is a conversation, held now rather than after the build.

| Pillar | Common way features fight it |
|---|---|
| The maze is the game | Fixed lanes, unblockable paths, towers that only shoot |
| Legible at a glance | Effects that stack invisibly; state shown only in a tooltip |
| Deterministic, therefore fair | Anything that reaches for a clock, a float, or an unseeded random |
| Every loss is explainable | Off-screen damage, silent auras, delayed consequences |
| Small numbers, big decisions | The ninth tower that is the third tower with more DPS |
