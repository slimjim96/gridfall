# Versus Mode — Requirements

**Slug:** `versus-mode` · **Status:** ready · **Owner:** design-lead · **Date:** 2026-08-09

## In One Sentence

Two players — and only ever two — play the same board against each other over a network, on desktop or
on a phone, by exchanging **command streams** rather than game state.

## Why this arrives now, and why it cannot wait for inverted mode

`inverted-mode-requirements.md` ends by handing `engine-systems` one question: *where does the opponent
live, and what part of it is simulation state?* It offers two answers — the AI inside Core as a system
in the tick order, or outside Core driving commands like `PlayPolicy` does today.

**If versus mode ships, that question is already answered.** The opponent is a remote human, and their
decisions arrive as commands on a wire. There is no version of that where the opponent's intent is
anything other than a command stream entering `CommandSystem`. The AI then becomes one implementation
of a command source and the human another, through one seam.

Deciding versus mode *after* the inverted-mode ADR risks building the "outside Core, serialised
alongside the trace" answer and then paying again. **This file exists to be read before that ADR, not
after it.**

## Why the netcode is close to free — checked, not assumed

Deterministic lockstep — send inputs, not state, and let both machines simulate — is normally
impossible to retrofit, because floating point does not agree across compilers and architectures.
ADR-0002 banned floats from Core and `SourcePurityTests` enforces the ban. That decision, made for
fairness under Pillar 3, is what makes this possible at all.

Four things already exist and were read, not assumed:

### 1. The wire protocol is already written, and it is called `Trace`

`Gridfall.Verify/Trace.cs` serialises `(tick, cmd, x, y, station, stationId)` plus checkpoint hashes,
and its own header says *"map + seed + commands is the entire input to a run."* That is the definition
of a lockstep protocol. The field names are already chosen and the loader already exists.

### 2. The volume is trivially small, which is what makes phones viable

The committed `crossroads-baseline` trace is **7 commands / 396 bytes for 3000 ticks** — 100 seconds of
play. A full twelve-wave run is larger; the policy fields 18–41 stations plus upgrades and repairs, so
**order 100–200 commands, single-digit kilobytes for an entire match** (extrapolated from the station
counts in the balance corpus, not measured — worth measuring before the ADR).

Not per second. **Per match.** A match costs less data than loading a web page, which is the difference
between "works on a train" and "needs wifi."

### 3. Desync detection and anti-cheat are the same function, and it is already called every tick

`Sim.Hash()` exists, is already checkpointed every 100 ticks in the trace format, and is already
verified 30/30 by the `replay` harness. Two clients exchanging checkpoint hashes get divergence
detection for nothing.

### 4. The server does not need a game engine

ADR-0001 keeps Godot out of Core, and `Gridfall.Verify` already proves Core runs headless on its own.
**An authoritative server is `Gridfall.Core` plus a socket** — no renderer, no headless Godot, no GPU.
It can re-simulate any match itself to arbitrate a hash mismatch. That is a direct dividend of the
Core/view boundary and it should be named as one.

`Snapshot()`/`Restore()` round-trip exactly and are already tested, which is reconnect, late-join and
spectate.

## The shape of the match is the open decision

Two candidates. **This is the decision this file exists to force**, and it is not primarily technical —
both are buildable, they differ in what has to be balanced and what the match feels like.

### A · Asymmetric — one attacks, one defends

Inverted mode with the AI seat given to a person. One board, one `Sim`, one command stream per seat.

- **Cheapest.** The mode already has requirements written and the seam already identified.
- **But it inherits a balance problem it cannot escape.** `inverted-mode-requirements.md` decided, on
  2026-08-09, that *each mode leans toward the human* — 70–85% of runs won by whichever chair the human
  sits in. **Two humans, and one of those leans has to die.** Attacker-versus-defender parity is a
  strictly harder tuning problem than anything the project has solved so far, and the twelve-board
  balance archive says nothing about it.
- Each player exercises half the content per match.

### B · Mirrored — both defend a copy of the same board, and spend to send at the other

Two `Sim` instances, one per player, same map and same seed. Offensive spending on your board sends
visitors to *theirs*. Both players do both jobs.

- **Symmetric, so it self-balances.** Neither seat needs to be tuned against the other; a mirror match
  is fair by construction. This removes the problem that sinks A.
- **Uses the shipped defence content unchanged.** Stations, visitors, waves, the twelve boards, all of
  it, as-is.
- **It retires two long-standing open problems rather than inheriting them.** next-steps §5 has five
  degenerate boards at 0.0% leak and `comb` as a single-wave gate. In a mirrored match nobody loses to
  the board — you lose to the person, and the board is shared terrain. A board too safe to be a solo
  level can still be a fair race.
- **Costs two sims and a cross-board channel.** `Sim` is a plain object with no statics, so two
  instances is not the hard part; the channel is a genuinely new rule and the only new rule in this
  option.
- Also the harder thing to show on a phone screen — see the presentation risk below.

**Recommendation: B.** A is cheaper this month and more expensive every month after, because it buys a
balance problem the project has no instrument for. B's new rule is bounded and its content bill is
already paid. But this is a design call about what the game *is*, and it is stated here as open on
purpose.

## Pacing: simultaneous-commit, not real-time

This is a recommendation strong enough to be a constraint, and the reason is in the existing loop.

Gridfall's input is already **bursty and then absent**: dense during the prep window, and during a wave
the player watches. `Sim.FinalizeTick` even starts the next wave by itself when `PrepTicksRemaining`
hits zero. The game is already shaped like a turn.

So: both players plan during the prep window, both commit, and **the wave resolves with no further
input from either side.** Network latency then has nothing to be late *for* — there is no moment where
one player's reaction time is racing the other's through a socket.

Two consequences worth having:

- **Async play falls out for free.** The server stores a command list and never game state, so a match
  can span hours — compose your wave on the bus, your opponent answers at lunch. That is a
  Chess.com-shaped product, and it is the strongest argument for the two-player cap: async 1v1 needs
  nobody online at the same moment, and it is how a phone game actually gets played.
- **It is the only structure that can hide information.** See risk 2.

Real-time versus (both players acting continuously against a shared clock) is not ruled out forever,
but it is a different requirements file and it needs everything below solved first.

## Pillar Check

| Pillar | | Note |
|---|---|---|
| 1 · The maze is the game | **Supports** | Under B, the maze is the game twice over: you build yours while reading theirs. Under A, unchanged from inverted mode |
| 2 · Legible at a glance | **Fights, and this is the mobile problem** | Two boards on a phone is the hardest legibility problem the project has taken on. Under A it is neutral. Today the game is mouse-only — there is no touch input anywhere in `godot/` |
| 3 · Deterministic, therefore fair | **Supports — this is the pillar cashing in** | The determinism regime built for fairness is exactly what makes lockstep possible. But the guarantee is now load-bearing against a *hostile* client, not just a careless one, and it has only ever been verified on one architecture. See risk 1 |
| 4 · Every loss is explainable | **Fights** | "Why did I lose" now includes a decision another person made, possibly hours ago, possibly on a board you were not watching. Under B the answer partly lives on someone else's screen |
| 5 · Small numbers, big decisions | **Supports** | Two players is a constraint, not a limitation. It is what keeps the protocol, the server and the matchmaking small enough to be worth building |

## TD Checklist

| Question | Answer |
|---|---|
| **Player fantasy** | Under B: building the better answer to the same problem, and watching your solution beat theirs. Under A: reading a defence and finding the hole, against someone who is reading you back |
| **Pathing** | Mechanically unchanged. Under B, two independent flow fields — `PathSystem` is per-`Sim` and already carries no shared state |
| **Economy** | The real Core surgery. `Gold` and `Patience` are single global `int`s on `SimState`. Under A they must become per-seat; under B each `Sim` keeps its own and the new quantity is what crosses between boards |
| **Wave pressure** | Becomes an opposing player's decision. Under B it is *both* players' decision simultaneously, which is a compounding-economy race — the exact pathology `inverted-mode` chose a fixed budget to avoid, and the same answer probably applies |
| **Failure state** | Losing to a person. Also: the opponent disconnecting, the opponent stalling an async match for a week, and a hash mismatch that cannot be attributed |

## The risks, in order

### 1. Cross-platform determinism is a claim, and versus mode makes it the foundation

next-steps §8 is explicit: the locale, line-ending, enumeration-order and float hazards each have a
test, **and those tests have only ever run on Linux/x86.**

Desktop-versus-mobile means **x86 against ARM**. `Fix32` is integer arithmetic so it should hold — but
"should" is not a hash, and under lockstep a single divergent tick ends the match for both players with
no way to say whose machine was wrong.

**This moves §8 from housekeeping to a hard prerequisite.** It is still two commands
(`dotnet test`, `Verify replay`); it just has to run on ARM before anyone commits to this design.

### 2. Lockstep means both clients know everything

Every client simulates the whole game, so **nothing can be hidden client-side.** If a player should not
see the opponent's build until the wave resolves, the *server* must withhold those commands until both
sides commit, and release them together.

Simultaneous-commit makes this natural — the server already holds both command sets at the commit
boundary. But it means the server is not optional and not a pure relay, and it must be designed in
rather than bolted on. A design that lets clients exchange commands peer-to-peer can never have hidden
information.

### 3. Mobile is entirely unproven — there is no export preset in the repo

`godot/` has **no `export_presets.cfg` at all.** The project has never been exported to anything.
Godot 4.6.3 mono to Android and iOS has real constraints, and they are independent of everything else
in this file — this risk can kill "mobile" on its own while leaving desktop versus perfectly healthy.

Compounding it: input is `InputEventMouseButton` and `InputEventMouseMotion` only, in `GameplayScene`
and `CameraRig`. **There is no touch handling anywhere.** A phone build is a new input layer, not a
recompile.

**Both of these are answerable in about a day, and both should be answered before any design work.**

### 4. Commands have no owner, and the trace archive pays for adding one

`CommandQueue.Entry` carries `Kind`, `Cell`, `StationDefIndex`, `StationId` — and no seat. Adding one
is a small change to a hashed structure, which **re-records every trace**. That is cheap today (one
file, 8 KB) and gets steadily less cheap. If it is happening, it should happen early.

### 5. The board set has one spawn, and versus does not fix that

All twelve maps have exactly one spawn — verified by reading the map files, and already flagged as
inverted mode's second risk. Under A the attacker still chooses only *what* and *when*, never *where*.
Under B it matters less, because the spatial decision is on your own board. **This is one more thread
arriving at multi-spawn boards**, alongside inverted mode and `station-pool`.

## Constraints

1. **Two players. Never three.** The cap is a design constraint that buys a small protocol, a small
   server and no matchmaking tiers — not a limitation to be relaxed later.
2. **Inputs cross the wire, never state.** If any game state is ever sent, determinism has stopped
   being the fairness guarantee and this whole design is the wrong one.
3. **One simulation, unchanged rules.** Inherited from `inverted-mode` constraint 1 and it binds harder
   here: no mode flag inside a system, no fork of the tick order. Versus changes where commands come
   from and how the match is scored.
4. **Single-player is byte-identical to today** — same hashes, same balance figures, same traces. This
   is the same criterion inverted mode set, and the trace re-record in risk 4 is its one licensed
   exception.
5. **The server runs Core and nothing else.** No Godot dependency, headless or otherwise. If the server
   ever needs the engine, ADR-0001 has been violated somewhere upstream.
6. **Hidden information is enforced server-side or not claimed.** See risk 2.
7. **No new simulation rule for A.** Option B's cross-board channel is the only new rule either option
   may introduce, and only if B is chosen.

## Acceptance Criteria

1. Two clients, given the same map and seed, play a full match and **agree on every checkpoint hash**.
2. That match's command stream, replayed offline through `Verify replay`, reproduces both players'
   final states exactly.
3. **The same holds with the two clients on different architectures** — x86 and ARM — and on different
   operating systems.
4. A deliberately corrupted client is detected at the first checkpoint after divergence, and the match
   ends rather than continuing in two different realities.
5. A player who disconnects mid-match can rejoin from a snapshot and finish it.
6. An async match survives a gap of at least 24 hours between commits, with the server holding only the
   command stream.
7. Total network payload for a complete twelve-wave match is **under 100 KB**, measured.
8. Neither player can see a command the other has committed but not yet resolved — verified by
   inspecting what the client is sent, not by what it draws.
9. Single-player runs produce hashes and balance figures identical to the committed archive.
10. A match is playable to completion on a phone, by touch, with both boards legible. *(Option B only;
    under A, on the one board.)*

## Open sub-decisions, with a recommendation each

| Question | Recommendation | Why |
|---|---|---|
| Mirrored or asymmetric? | **Mirrored (B)** | Asymmetric buys a parity-balance problem with no instrument to measure it; mirrored's content bill is already paid — but this is the design call, stated open |
| Real-time or simultaneous-commit? | **Simultaneous-commit** | Latency stops existing as a concept, and async play — the thing that makes a phone game get played — falls out for free |
| Authoritative server, or relay? | **Authoritative**, re-simulating | It costs almost nothing (Core + a socket) and it is the only way to arbitrate a mismatch or hide information |
| Same seed for both players under B? | **Yes** | Identical boards make the match about the players. A per-player seed reintroduces "I got the worse board," which Pillar 3 exists to prevent |
| Ranked, rating, matchmaking? | **Out of scope, explicitly** | Named here so it is a later decision rather than a silent assumption |
| Does versus ship before inverted mode? | **No — but it is decided first** | Inverted mode is the single-player proving ground for the opponent-as-command-source seam |

## What must be true before design proceeds

Three checks, none of them a design task, all of them cheap. Any one failing changes this file:

1. **`Verify replay` passes on ARM**, and `dotnet test` with it (next-steps §8).
2. **A Godot 4.6.3 mono export runs on Android**, at all, with C# alive. There is no export preset yet.
3. **The command volume of a full twelve-wave run is measured**, not extrapolated from station counts.

## Handoff

To `engine-systems` — and it merges with the handoff already sitting at the end of
`inverted-mode-requirements.md` rather than queueing behind it.

**The ADR is one question, not two:** where does an opponent's intent enter the simulation? Versus mode
constrains the answer that inverted mode left open, because a remote human's intent can only arrive as
a command. If that seam is built once, `PlayPolicy` and a network peer are both command sources and
neither is special.

Nothing here should be built before the three checks above return.
