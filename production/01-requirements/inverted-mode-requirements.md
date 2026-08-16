# Inverted Mode — Requirements

**Slug:** `inverted-mode` · **Status:** ready · **Owner:** design-lead · **Date:** 2026-08-09

## In One Sentence

You spend a budget sending visitors down the board and score the ones that arrive, while the game
builds the stations trying to stop them — and **the game ships both directions**, not one instead of
the other.

## Why this is cheaper than it sounds, and where the cost actually is

Three things are already built, and one of them is the opponent.

### 1. The AI that builds the towers already exists and is already deterministic

`PlayPolicy` in `Gridfall.Verify` is a scripted player: it places by route coverage, buys by effective
serving-per-gold against the visitors it has met, upgrades when the board saturates, repairs what is
about to die, and refuses placements the game would refuse. It is seeded, integer-only, and has driven
every balance figure in the repo.

**That is the opponent.** Not a sketch of one — the finished thing, with a written operational
definition of what "competent" means and a 150-run-per-board measurement history.

### 2. The attacker's score already has a name, a number, and twelve measurements

The score in inverted mode is *visitors that reached the goal*. The balance sim has reported that on
every run since it existed. It is called **leak rate**.

### 3. So the difficulty ladder for inverted mode is already measured — and it says the mode is
currently unwinnable

Read the balance corpus from the other side. 150 runs per board, seed 1, current content:

| Board | Attacker's score (leak %) | Defence broken (runs lost) | Stations standing | As an inverted-mode level |
|---|---|---|---|---|
| `spiral` | **1.6%** | 26.7% | 21.7 | the most winnable board in the repo |
| `comb` | 1.2% | **42.0%** | 17.8 | attacker's best odds; a single-wave gate |
| `crossroads` | 0.9% | 20.0% | 41.3 | the AI fields twice the defence of anywhere else |
| `braid` | 0.8% | 0.0% | — | |
| `stepwell` | 0.6% | 0.0% | — | |
| `chambers` | 0.3% | 0.0% | — | |
| `atoll` | 0.2% | 0.0% | — | |
| `switchback` | 0.1% | 0.0% | — | |
| `driftway`, `meander`, `ringfort`, `gauntlet` | **0.0%** | 0.0% | — | **the attacker never scores at all** |

**An attacker running the shipped wave tables delivers about one visitor in a hundred, and on four
boards delivers nothing whatsoever.** The five "degenerate" boards that have been an embarrassment
since 2026-08-08 are, read backwards, boards where the defence is simply unbeatable.

A human composing waves against a budget will do better than a fixed table. But the table is the
**floor**, and the floor is zero on a third of the set.

## The one thing the simulation has to change — checked, not assumed

Today `SpawnSystem` reads `content.Waves[state.WaveIndex - 1]`. In inverted mode the wave comes from
the player instead.

**Verified 2026-08-09:** that expression appears in exactly **two** places, `SpawnSystem.Run` and
`SpawnSystem.WaveComplete`, and nothing else in Core indexes the wave table during a run. The seam is
one `WaveDef` source, in one file.

**Everything else is unchanged.** Pathing, targeting, serving, fussiness, projectiles, the economy,
station combat, repair, salvage, patience — the AI plays the game exactly as it is, under the rules
that are already there and already tested. The win condition is the *same state variable read with the
opposite sign*: patience reaching zero is a loss today and the attacker's victory in inverted mode.

### And the opponent would pass Core's purity gate today

`SourcePurityTests` is the audit that keeps Core deterministic: no float or double, no `System.Random`,
no clock, no `Guid`, no parallelism. Run against `PlayPolicy` and `VisitorCensus` as they stand:

| Banned construct | Hits |
|---|---|
| `float` / `double` | **one**, `VisitorCensus.ServingPerTickPerGold` — a reports-only convenience, not on the decision path. The policy ranks with the integer `ValuePerGold` |
| `System.Random` / `new Random` | none — it uses `SimRandom`, seeded |
| `DateTime` / `Stopwatch` | none |
| `Guid`, `Parallel.` | none |

It reads only `Content`, `Map`, `Path`, `State` and `TickCount` — every one of them a thing a Core
system already has. Its one sort is `score` descending then `cell` ascending, a total order, so an
unstable sort cannot make it non-deterministic.

**So "move the opponent into Core" is a relocation, not a rewrite.** That does not decide the ADR — it
means the ADR is free to be decided on the right grounds (how big Core should get, and what a trace has
to carry) rather than on how much work each option is.

## Pillar Check

| Pillar | | Note |
|---|---|---|
| 1 · The maze is the game | **Supports, and this is the interesting part** | The maze is still the game — you are *solving* it instead of *building* it, against an opponent who lengthens it while you watch. But see the agency risk below: this only holds if the player has a spatial decision, and today they have none |
| 2 · Legible at a glance | **Neutral to fights** | Everything the board says is currently phrased for the defender. "That station covers this corner" has to read as a threat |
| 3 · Deterministic, therefore fair | **Fights — and this is the ADR** | The opponent's decisions become simulation input. Its RNG stream and its build cooldown must be hashed and snapshotted, or a replay resumed mid-run diverges. Today `PlayPolicy` is outside the sim and none of its state is in the hash |
| 4 · Every loss is explainable | **Fights** | "Why did my brute die at that corner" needs the same inspectability the defender gets, pointed the other way |
| 5 · Small numbers, big decisions | **Fights, today** | The player's toolkit is **five visitor archetypes**, and two of the five traits are inert or narrow: `fussiness` never changes a purchase at shipped composition, and `attackDrain` exists only on `sapper` |

## TD Checklist

| Question | Answer |
|---|---|
| **Player fantasy** | Reading a defence and finding the hole in it. Timing, composition and pressure rather than placement |
| **Pathing** | Unchanged mechanically — but it is now the *opponent* who re-mazes, and the player's route changes under them without warning. That is the mode's core tension and its core legibility problem |
| **Economy** | Two economies running against each other for the first time. The AI earns from kills exactly as today; the player's budget is new and is the main tuning surface |
| **Wave pressure** | Becomes a player decision instead of a data table. This is `tier2-soft-lock-options.md` option B arriving from an unexpected direction |
| **Failure state** | Sending into a defence that answers it — burst into armour-less swarms, rate into a board built to punish it. Also: spending everything early and having nothing when a gap finally opens |

## The three risks, in order

### 1. The opponent and the measuring instrument are the same code

`PlayPolicy`'s own documentation says it is *"a competent BEGINNER… a floor on difficulty, not a
verdict."* That is exactly right for a measuring instrument and **too weak for an opponent**.

The moment anyone strengthens it, every balance figure in the repo describes a different game — eleven
balance passes, twelve boards, and the entire report archive are all measured against the beginner.

**So they must be separable before either is touched.** One interface, named difficulty levels, and the
balance harness pinned to the beginner one *by name in the report header*. This is not a refactor to do
later; doing it later means re-measuring everything.

### 2. The player has no spatial decision, because every board has one spawn

All twelve maps have exactly **one** spawn. `MapTargets.MaxLanes` permits more and nothing uses it.

With one spawn, the attacker chooses *what* and *when* and never *where* — and against an opponent who
re-mazes, "when" is largely reactive. That is thin. Multi-spawn boards, where a defence cannot cover
every lane at once, is where the spatial decision comes from, and **no board in the repo has one.**

### 3. The player's toolkit is five archetypes and two of its traits are dead

`station-pool` is already growing the *station* roster to ten. Inverted mode needs the same for
visitors, and for the same reason: five archetypes differentiated mostly by hit points is a stat line,
not a toolkit. This is the **third** independent thread to arrive at "the visitor roster needs a real
spread" — the fussiness measurement and the `station-pool` axis argument are the other two.

## The asymmetry is deliberate: whichever chair the human sits in gets the advantage

**Decided 2026-08-09.** The same content cannot serve both directions, because it is not supposed to:
*each mode leans toward the human.*

This sounds like it needs two content sets. It does not — `balance-targets.md` **already quantifies the
lean**, and has since it was written:

> Runs lost, first half of the table: **0–5%**. Second half: **15–30%**.

That is not a neutral target. It says the defending human should win roughly three runs in four, and
should lose enough late to make it matter. Inverted mode needs the **same numbers pointed the other
way** — the attacking human wins 70–85% of runs, and the losses arrive late.

So there is one band, stated twice, and the tuning problem is: what makes the attacker's side reach it?

### The knob is mode-local by construction

| Knob | Touches | Available in |
|---|---|---|
| **The defence's total budget** | nothing shared — no station, visitor, wave or map | inverted only |
| The attacker's budget curve | a new `cost` on `VisitorDef`; additive, ignored by normal mode | inverted only |
| The wave table | the attacker's *script* in normal mode, its *price list* in inverted | both, two readings |
| Station, visitor, map data | everything | **both — do not tune these per mode** |

**The defence's budget is the right primary dial**, and the reason is that it cannot leak. Turning it
changes nothing normal mode can observe, so the twelve committed balance figures stay valid while
inverted mode is tuned from scratch. Tuning a station's cost or a visitor's appetite for inverted mode
would move both directions at once and put the whole balance archive back in play.

Both shapes are already in the harness — `Verify balance --cap N` (total) and `--perWave N` (rate).
Uncapped is the default and reproduces the shipped figures byte for byte.

### What the curve actually looks like — measured

Full sweep: [inverted-mode difficulty](../../content-data/docs/reports/2026-08-09-inverted-mode-difficulty.md).
Three results shape the requirements below:

- **The dial reaches the band.** `crossroads` puts the attacker at 73% wins at 20,000g total, or 89% at
  550 g/wave. The mode is tunable.
- **One number cannot serve four boards.** `spiral` jumps 29% → 100% in a single step and `comb`
  43% → 100%; neither has an in-band setting at all, and `crossroads` and `meander` need different
  numbers from each other. **The defence budget is per-board content, not a global difficulty slider** —
  which is the direct answer to "each scenario should lean toward the player."
- **Board quality is mode-independent.** `comb` has exactly **1 of 12** waves able to end a run at
  *every* setting of *both* dials, as it does in normal mode; `crossroads` has 6–10 either way. A board
  that is a gate is a gate in both directions.

That last one changes what the level set is worth. Fixing it is not a normal-mode chore that inverted
mode will need repeating — it is **one piece of work serving both**, and the boards that fail one
mode's quality bar are exactly the ones that fail the other's.

## Constraints

1. **One simulation, both directions.** No second `Sim`, no fork of the tick order, no mode flag inside
   a system. The mode changes where the wave comes from and how the end state is scored; if it starts
   changing rules, it is a different game and needs saying so.
2. **The opponent is simulation input and must be hashed**, or replay and the determinism harness stop
   meaning anything. ADR required before any code.
3. **The opponent's beginner difficulty is frozen** as the balance harness's policy, and the balance
   report names which difficulty it ran.
4. Content serves both directions: a wave table is the attacker's *script* in normal mode and the
   attacker's *price list* in inverted mode. One file, two readings.
5. No new visitor trait invented here. Name the gap; `content-data` and `engine-systems` fill it.
6. **The defence's budget is per-board data**, authored beside the map like its station roster — not a
   global difficulty slider. Measurement says no global value can put even two of four boards in band.

## Acceptance Criteria

1. Both modes are selectable, and a board can be played in either.
2. In inverted mode the player composes and launches a wave from a budget, and cannot exceed it.
3. Visitors reaching the goal are scored and shown; the score is the same quantity `Verify balance`
   calls leak rate, and the two agree on the same run.
4. The opponent builds, upgrades and repairs without the player acting, under the same
   `CommandSystem` rules — an illegal build is refused for the AI exactly as for a human.
5. **Identical inputs give identical hashes with the AI driving**, and a trace recorded in inverted mode
   replays. The opponent's RNG state and cooldown appear in the state hash.
6. The balance harness still runs the beginner policy and reproduces the twelve committed figures
   **unchanged**, with the difficulty named in its header.
7. A player can tell why a visitor died: which station, on which cell, at which point of the route.
8. On at least one board with more than one spawn, a defence that covers one lane visibly fails to
   cover another.
9. Normal mode **plays** identically to today — same behaviour, same balance figures. **Revised
   2026-08-15:** this said "byte-identical … same hashes, same traces", which
   [ADR-0008](../../engine-systems/decisions/ADR-0008-active-wave-as-commanded-state.md) makes
   unachievable: the active wave becomes a field in `SimState`, and hashes are over state, so every
   trace re-records **once**. Nothing plays differently — there is simply more state to hash. The
   re-record happens with that change, not after it.

## Open sub-decisions, with a recommendation each

None of these blocks the architecture work; all of them change the feel.

| Question | Recommendation | Why |
|---|---|---|
| Score, or win/lose? | **Both** — deliver-to-score, with draining patience to zero as the clean win | The state variable already exists and already ends runs; a score gives the four unwinnable boards something to be |
| Fixed budget, or earned? | **Fixed per wave, growing on a curve** | An earned budget makes two compounding economies race, which is the exact pathology six balance passes fought in normal mode |
| Does the player see the AI's gold? | **Yes** | Pillar 4. An opponent whose next move is unguessable is not a puzzle, it is weather |
| Can the player re-send mid-wave? | **Yes, from the same budget** | It is the only way "when" becomes a real decision on a one-spawn board |

## A vocabulary note that will bite

**`VisitorLeaked` reads as a failure, and in this mode it is the player's score.** `theme-direction.md`
already observes the codebase's vocabulary is mostly neutral — this is the exception it missed, and it
is an event name, a balance metric and a HUD string. The loop as written in that file already uses the
neutral word: *a need that reaches the end **unresolved**.* Renaming is a one-pass job with a measured
precedent (67 files, `replay` unchanged) and it is far cheaper now than after a second mode is built on
top of the current word.

## Handoff

To `engine-systems`, and the ADR comes first: **where the opponent lives and what part of it is
simulation state.** Two real alternatives —

- **Inside Core**, as a system in the tick order with its state in the hash. Replay works everywhere;
  Core grows an AI, which is a large thing to let past the boundary.
- **Outside Core**, driving commands like `PlayPolicy` does today, with its state serialised alongside
  the trace. Keeps Core small; every consumer of a trace now needs the AI's state too.

Nothing else should be built until that is decided, and criterion 5 is the one that decides it.

### Decided 2026-08-15 — and neither alternative won

[ADR-0008 — Make the Active Wave Hashed State, Written by a
Command](../../engine-systems/decisions/ADR-0008-active-wave-as-commanded-state.md) (status: proposed)
takes a third option that removes the question. The **wave** becomes a field in `SimState`, written by
a `SendWave` command; `SpawnSystem` reads it from state instead of from `content.Waves[...]`, and normal
mode fills the same field from the table.

Once the only thing that reaches the simulation is a command, **the opponent's location stops being an
architectural question.** `PlayPolicy` can stay exactly where it is in `Gridfall.Verify`; a human at a
keyboard and a socket carrying a remote player are the same case. Core does not grow an AI (rejecting
the first alternative) and no trace consumer carries the AI's state (rejecting the second), because
there is no AI state in the loop at all — only commands, which a trace already records.

The ADR was raised jointly with `versus-mode`, whose composed-waves decision needs the identical seam.
Read it before building anything here; criterion 9 above was revised by it.
