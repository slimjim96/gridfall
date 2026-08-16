# Versus Mode — Requirements

**Slug:** `versus-mode` · **Status:** ready · **Owner:** design-lead · **Date:** 2026-08-09
**Revised:** 2026-08-15 — match shape decided (mirrored), channel decided (composed waves); pre-design
checks 1 and 3 answered. **No design questions remain open.**

## In One Sentence

Two players — and only ever two — **each defend their own copy of the same board** while spending a
budget to **compose waves they send at the other**, over a network, on desktop or on a phone, by
exchanging **command streams** rather than game state.

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

**Measured 2026-08-13**, all twelve boards, seed 1, full twelve-wave runs under `PlayPolicy`.
`BuildsPlaced` increments on `_sim.Enqueue` (`PlayPolicy.cs:371`), so these are commands *issued* —
the thing that crosses a wire — not commands the sim accepted:

| | builds | upgrades | repairs | total |
|---|---|---|---|---|
| `chambers` — most | 249 | 43 | 14 | **306** |
| median board | ~200 | ~45 | ~12 | **~255** |
| `gauntlet` — least | 15 | 30 | 0 | **45** |

Plus at most 12 `startWave`. So **45–320 commands per board.** At the trace format's measured 57 bytes
per command that is ~18 KB of naive JSON; a byte-packed command (tick, kind, cell, def index) is
~9 bytes and puts a board near **3 KB**.

**Double it for a match.** The 2026-08-15 decision makes a match two boards, so the figures above are
per-player: **90–640 commands, ~36 KB naive JSON or ~6 KB packed**.

**The channel adds almost nothing on top.** Composed waves cross it, and `SimState.MaxWaveEntries` caps
a wave at **16** entries; at (def index, count) per entry that is ~64 bytes packed, so twelve waves from
both players is **under 2 KB for the entire match**. The richer answer to "what crosses the channel"
turns out to be the cheap one on the wire — the composition is a handful of small integers, and it is
the *screen* that composes them that is expensive, not the sending.

So the match stays comfortably inside acceptance criterion 7's 100 KB, with the headroom now spent on
handshake and checkpoint-hash exchange rather than on gameplay.

Not per second. **Per match.** A match costs less data than loading a web page, which is the difference
between "works on a train" and "needs wifi."

**Two things the measurement turned up that the extrapolation would have missed.**

`crossroads` issues 205 build commands and ends with 38 stations standing having lost 10 — so roughly
three quarters of its build commands are refused by the sim. A human does not spam placements the board
will reject, so **the policy's figure is a conservative upper bound for a human**, which is the right
direction for a bandwidth budget but the wrong direction for estimating anything else from it.

`gauntlet` issues 45 commands against `chambers`'s 306 — a **7× spread across the shipped set**. Any
per-match budget has to be quoted as a range, and `gauntlet` at 15 builds is doing something different
enough from the other eleven to be worth a look on its own terms.

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

## The shape of the match — DECIDED 2026-08-15: mirrored

**Mirrored. Both players defend a copy of the same board, and offensive spending sends visitors at the
other player's board.** Option B below, chosen by the human on 2026-08-15. The alternative and the
reasoning are kept because the *reason* constrains what comes next: the mode is symmetric, so nothing
in it may be tuned per seat.

Two candidates were on the table. They were never primarily a technical choice — both are buildable,
and they differ in what has to be balanced and what a match feels like.

### A · Asymmetric — one attacks, one defends · **rejected**

Inverted mode with the AI seat given to a person. One board, one `Sim`, one command stream per seat.

- **Cheapest.** The mode already has requirements written and the seam already identified.
- **But it inherits a balance problem it cannot escape.** `inverted-mode-requirements.md` decided, on
  2026-08-09, that *each mode leans toward the human* — 70–85% of runs won by whichever chair the human
  sits in. **Two humans, and one of those leans has to die.** Attacker-versus-defender parity is a
  strictly harder tuning problem than anything the project has solved so far, and the twelve-board
  balance archive says nothing about it.
- Each player exercises half the content per match.

### B · Mirrored — both defend a copy of the same board, and spend to send at the other · **chosen**

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

**Why B won.** A is cheaper this month and more expensive every month after, because it buys a balance
problem the project has no instrument for. B's new rule is bounded and its content bill is already paid.

### What the decision now commits the project to

Three consequences follow immediately, and each is a thing someone must do rather than a thing to note:

1. **The cross-board channel is the only new simulation rule in the whole mode**, and it is therefore
   the thing the ADR has to get right. What crosses is *visitors*, and the open question is whether the
   sending player composes a wave (as in inverted mode) or buys pressure by some cheaper handle. It is
   simulation input on the receiving board, so it is hashed, and it must be expressible as a command.
2. **Nothing may be tuned per seat, ever.** Symmetry is the entire reason this option was chosen; a
   number that differs between the two boards silently reintroduces the parity problem that sank A.
   This is written as constraint 8 below.
3. **Two `Sim` instances per match becomes a supported configuration.** `Sim` is a plain object with no
   statics — verified — but nothing has ever run two at once, and "no statics" is a property that has
   to stay true rather than one that stays true by itself. It needs a test.

**What it does *not* change:** the pacing, the protocol, the server shape, the bandwidth figures, and
every check already run. Those were all decided or measured independently of this, which is why they
were worth doing first.

## What crosses the channel — DECIDED 2026-08-15: composed waves

**You spend a budget, compose a wave, and send it at the opponent's board.** Not a pressure slider, not
an abstract handle — the same act `inverted-mode-requirements.md` already specifies in full.

### This is the cheapest possible answer, and that is not a coincidence

The alternative was a cheaper pressure handle, easier to render on a phone. Composed waves cost more in
Pillar 2 and buy three things a slider cannot:

**1. One mechanic now serves three modes.** Normal mode reads a wave table. Inverted mode has the human
compose the wave. Mirrored versus has *both* humans compose, at each other. That is one composition
mechanic, one budget, one price list, one piece of UI — built once, for a mode that ships before this
one.

**2. It lands on a seam that is already found and already measured.** `inverted-mode` verified that
`content.Waves[state.WaveIndex - 1]` appears in exactly **two** places, `SpawnSystem.Run` and
`SpawnSystem.WaveComplete`, and that nothing else in Core indexes the wave table during a run. A
composed wave enters through that same seam. **One seam, three modes** — and versus adds nothing to
what inverted mode already has to build there.

**3. The content bill was already paid twice over.** A wave table is the attacker's *script* in normal
mode and their *price list* in inverted mode; in versus it is the price list again, unchanged. One
file, three readings, no new content.

### Budget enforcement is a Core rule, not a server responsibility

This is the part worth getting right, and it falls out of what already exists.

A hostile client could compose a wave that exceeds its budget. The instinct is to have the server
validate it — but that is the wrong place, and unnecessary. **The receiving `Sim` rejects an
over-budget wave exactly as `CommandSystem` already rejects an unaffordable build**, deterministically,
on both machines, with an event saying why. `CommandSystem.BuildCost` is the precedent and the shape to
copy.

Two consequences follow, both good:

- **A cheating client desyncs itself into a rejection**, not into an advantage. Both machines agree the
  wave was illegal because both machines ran the same rule.
- **The server stays a relay-plus-commit-gate** rather than growing a rules engine. It still holds
  commands until both players commit (risk 2), and it can still re-simulate to arbitrate — but it never
  needs to *know* what a wave costs.

### What it costs, stated plainly

**Pillar 2, and this is now the mode's largest open problem.** A composition screen has to coexist with
two boards on a phone. That is a harder presentation brief than anything in the project, and it is the
one place where the cheap answer would genuinely have been better.

**Pillar 5 is the reason it is worth it.** "Small numbers, big decisions" is exactly what a composed
wave is and exactly what a pressure slider is not.

**It makes the visitor roster load-bearing.** `inverted-mode` risk 3 already says the player's toolkit
is five archetypes with two traits inert or narrow — `fussiness` never changes a purchase at shipped
composition, and `attackDrain` exists only on `sapper`. That was the **third** independent thread
demanding a real visitor spread. Versus makes it the **fourth**, and the first where it is the flagship
mode's core verb rather than a secondary one. **Composing from five archetypes is not composing.**

### The budget is fixed, not earned — inherited, not re-decided

`inverted-mode` already recommends **fixed per wave, growing on a curve**, because an earned budget
makes two compounding economies race, which is the exact pathology six normal-mode balance passes
fought. Mirrored versus has *two* players with budgets, so that reasoning applies twice over and the
TD checklist's "compounding-economy race" concern is answered by inheriting the decision rather than
re-opening it.

Constraint 8 binds it further: **the budget curve is identical for both players.** It is content, not a
per-seat dial.

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
| 1 · The maze is the game | **Supports, twice over** | You build your maze while reading theirs, and the pressure you send is judged against the maze they built. Both players are doing the pillar's activity at once |
| 2 · Legible at a glance | **Fights, and mirrored makes it worse** | Two boards on a phone is the hardest legibility problem the project has taken on, and choosing mirrored commits to it rather than leaving it optional. Today the game is mouse-only — there is no touch input anywhere in `godot/`. **This is the pillar the decision costs the most** |
| 3 · Deterministic, therefore fair | **Supports — this is the pillar cashing in** | The determinism regime built for fairness is exactly what makes lockstep possible. But the guarantee is now load-bearing against a *hostile* client, not just a careless one, and two `Sim`s per match doubles what a divergence can come from. See risk 1 |
| 4 · Every loss is explainable | **Fights** | "Why did I lose" now includes a decision another person made, possibly hours ago, on a board you were not watching. The answer partly lives on someone else's screen, and mirrored guarantees that rather than risking it |
| 5 · Small numbers, big decisions | **Supports** | Two players is a constraint, not a limitation. It is what keeps the protocol, the server and the matchmaking small enough to be worth building |

## TD Checklist

| Question | Answer |
|---|---|
| **Player fantasy** | Building the better answer to the same problem, and watching your solution outlast theirs. Both players face one board and one opponent, and the same board |
| **Pathing** | Mechanically unchanged, and this is where mirrored is cheap. Two independent flow fields — `PathSystem` is constructed per-`Sim` and carries no shared state |
| **Economy** | Lighter than asymmetric would have been. `Gold` and `Patience` stay single `int`s because **each `Sim` keeps its own** — no per-seat split inside `SimState`. The new quantity is whatever crosses between boards, and that is the design question, not the surgery |
| **Wave pressure** | **Entirely a player decision now** — both players compose waves at each other from a budget. That is two economies running simultaneously, which is why the budget is **fixed per wave on a curve** rather than earned, inherited from `inverted-mode` rather than re-decided. `tier2-soft-lock-options.md` option B arriving from a third direction |
| **Failure state** | Losing to a person. Also: the opponent disconnecting, the opponent stalling an async match for a week, and a hash mismatch that cannot be attributed to a board |

## The risks, in order

### 1. Cross-platform determinism is a claim, and versus mode makes it the foundation

next-steps §8 is explicit: the locale, line-ending, enumeration-order and float hazards each have a
test, **and those tests have only ever run on Linux/x86.**

Desktop-versus-mobile means **x86 against ARM**. `Fix32` is integer arithmetic so it should hold — but
"should" is not a hash, and under lockstep a single divergent tick ends the match for both players with
no way to say whose machine was wrong.

**This moves §8 from housekeeping to a hard prerequisite.** It is still two commands
(`dotnet test`, `Verify replay`); it just has to run on ARM before anyone commits to this design.

**Downgraded but not closed, 2026-08-13.** Both commands now pass on arm64 under emulation — 244/244
and 30/30 against x86-recorded hashes. The integer regime holds across instruction sets. What is left
of this risk is real silicon, and it arrives with risk 3 rather than before it.

### 2. Lockstep means both clients know everything

Every client simulates the whole game, so **nothing can be hidden client-side.** If a player should not
see the opponent's build until the wave resolves, the *server* must withhold those commands until both
sides commit, and release them together.

Simultaneous-commit makes this natural — the server already holds both command sets at the commit
boundary. But it means the server is not optional and not a pure relay, and it must be designed in
rather than bolted on. A design that lets clients exchange commands peer-to-peer can never have hidden
information.

### 3. Mobile is unproven, and the first attempt found a framework conflict

`godot/` has no committed `export_presets.cfg` — the file is **gitignored** (`.gitignore:9`), so a
preset is a local artefact each machine makes for itself, not something the repo can carry. The project
has never been exported to anything.

Compounding it: input is `InputEventMouseButton` and `InputEventMouseMotion` only, in `GameplayScene`
and `CameraRig`. **There is no touch handling anywhere.** A phone build is a new input layer, not a
recompile.

#### Attempted 2026-08-14, headless, with an Android preset. It failed — informatively.

Godot's own words first: **`Exporting to Android when using C#/.NET is experimental.`** That is 4.6.3
describing itself, and it belongs in any decision that bets on this path.

Then the structural one:

```
C# project targets 'net8.0' but the export template only supports 'net9.0'.
```

**This is not fatal, and the reason matters.** `godot/Gridfall.Godot.csproj` pins `net8.0` with a
comment reading *"Godot 4.6's SDK targets net8.0. This is the constraint that pins Gridfall.Core to
net8.0 as well (ADR-0001)."* Two things about that, both checked:

- **ADR-0001 does not decide the framework.** Its decision is the Core/view boundary; `net8.0` appears
  in its title and option B as description, not as the thing being chosen. Nothing in it is disturbed
  by moving the *view*.
- **Core does not have to move at all.** Only the Godot project needs `net9.0`, and a `net9.0` project
  may reference a `net8.0` library. Core stays `net8.0` and stays Godot-free — which is the whole of
  what ADR-0001 actually protects.

Retargeting **only** the Godot project to `net9.0` was tested end to end:

| | Result |
|---|---|
| That export error | **gone** |
| `dotnet build -c Release` | 0 warnings, 0 errors |
| Godot 4.6.3 mono loading the assembly | **runs** — headless boot printed the C# `tiles:`/`units:` lines |

So the `net8.0` pin on the view is the SDK's *default*, not a hard constraint, and the runtime does not
enforce it. **The edit was reverted**: a framework change that a csproj comment attributes to an ADR is
an `engine-systems` decision, not a side effect of an export experiment.

#### What the attempt could not reach

Everything after that is absent toolchain, not a project problem: Godot's Android export templates, a
JDK, and the Android SDK's `platform-tools` and `build-tools`. Installing it is roughly **5 GB**
(templates ~1.2 GB, JDK ~400 MB, Android SDK ~1.5 GB, `dotnet workload install android` ~1.5–2 GB)
against **6.3 GB free on a disk at 89%**, so it was stopped rather than run to the edge of the disk.

**The residual risk is smaller than it was.** What remains unknown is whether the toolchain produces a
running APK — not whether the project can be made to satisfy it.

### 4. Commands have no owner — and mirrored may mean they never need one

`CommandQueue.Entry` carries `Kind`, `Cell`, `StationDefIndex`, `StationId` — and no seat. Adding one
is a small change to a hashed structure, which **re-records every trace**.

**Largely dissolved by the 2026-08-15 decision, and this is a real dividend.** Under mirrored, each
player owns a `Sim` — their commands apply to their own board, and a cross-board send applies to the
opponent's. **Ownership is a property of routing, not of the command**, so the queue may need no seat
field at all and the trace archive may never be re-recorded for this.

Asymmetric would have needed the field, because there both seats act on one `Sim`. Confirm this holds
before designing the channel; if the channel turns out to need an originator recorded in hashed state,
the field comes back and the "do it early" advice returns with it.

### 5. The board set has one spawn, and mirrored softens but does not fix it

All twelve maps have exactly one spawn — verified by reading the map files, and already flagged as
inverted mode's second risk. Mirrored reduces the sting, because each player's spatial decision is on
their own board and does not depend on the spawn count of the board they are attacking. But what
crosses the channel still arrives *somewhere*, and with one spawn it always arrives in the same place.
**This is one more thread arriving at multi-spawn boards**, alongside inverted mode and `station-pool`.

## Constraints

1. **Two players. Never three.** The cap is a design constraint that buys a small protocol, a small
   server and no matchmaking tiers — not a limitation to be relaxed later.
2. **Inputs cross the wire, never state.** If any game state is ever sent, determinism has stopped
   being the fairness guarantee and this whole design is the wrong one.
3. **One set of rules, two instances of it.** Inherited from `inverted-mode` constraint 1 and it binds
   harder here: no mode flag inside a system, no fork of the tick order. Mirrored runs *two `Sim`s of
   the same game*, not one `Sim` that knows it is in versus mode.
4. **Single-player behaviour is unchanged** — same play, same balance figures. **Revised 2026-08-15:**
   this used to say "same hashes, same traces" and that is now known to be unachievable.
   [ADR-0008](../../engine-systems/decisions/ADR-0008-active-wave-as-commanded-state.md) makes the
   active wave a field in `SimState`, and hashes are over state, so every trace re-records **once**.
   Behaviour and balance figures are untouched; the hashes shift because there is more state, not
   because anything plays differently. Do the re-record with that change, not after it.
5. **The server runs Core and nothing else.** No Godot dependency, headless or otherwise. If the server
   ever needs the engine, ADR-0001 has been violated somewhere upstream.
6. **Hidden information is enforced server-side or not claimed.** See risk 2.
7. **The cross-board channel is the only new simulation rule the mode may introduce.** Everything else
   is existing rules running twice. If a second new rule appears, that is the signal the design has
   drifted into being a different game.
8. **Nothing is tuned per seat.** No station, visitor, wave, map, budget or timer may differ between
   the two boards. Symmetry is the reason mirrored was chosen over asymmetric; a per-seat number
   reintroduces the parity-balance problem that rejected A, silently and late.
9. **Budget legality is a Core rule, enforced by command rejection.** An over-budget wave is refused by
   the receiving `Sim` exactly as `CommandSystem` refuses an unaffordable build — deterministically, on
   both machines, with an event saying why. The server must never need to know what a wave costs.
10. **The composition mechanic is shared with `inverted-mode`, not forked from it.** One budget, one
    price list, one composition UI. If versus needs its own variant, that is a design change and needs
    saying so out loud.

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
7. Total network payload for a complete twelve-wave match is **under 100 KB**, measured. *(The command
   payload is now known — 45–320 commands per board, so 90–640 and ~36 KB naive JSON for a mirrored
   match. The criterion covers the rest: the cross-board channel, handshake, checkpoint-hash exchange
   and commit acknowledgements, none of which are designed yet.)*
8. Neither player can see a command the other has committed but not yet resolved — verified by
   inspecting what the client is sent, not by what it draws.
9. Single-player runs produce hashes and balance figures identical to the committed archive.
10. A match is playable to completion on a phone, by touch, **with both boards legible** — the criterion
    the mirrored decision made mandatory rather than optional.
11. **Two `Sim` instances advanced in the same process do not influence each other**: interleaving their
    ticks produces the same hashes as running each to completion alone. This is the test that keeps
    "`Sim` has no statics" true rather than merely currently-true.
12. **A match is symmetric under seat swap.** Replaying a recorded match with the two players' command
    streams exchanged produces the mirrored result. This is constraint 8 made checkable.
13. **A player composes a wave from a budget and cannot exceed it** — and an over-budget wave submitted
    by a modified client is **refused by both simulations identically**, with an event naming the
    reason. Verified by submitting one deliberately, not by trusting the sender.
14. **A wave composed in inverted mode and the same wave composed in versus produce identical spawns**
    on the same board and seed. One mechanic, proven to be one mechanic.

## Open sub-decisions, with a recommendation each

| Question | Recommendation | Why |
|---|---|---|
| ~~Mirrored or asymmetric?~~ | **DECIDED 2026-08-15: mirrored** | Asymmetric buys a parity-balance problem with no instrument to measure it; mirrored's content bill is already paid. See the decision section above |
| ~~What crosses the channel?~~ | **DECIDED 2026-08-15: composed waves** | The same act inverted mode already specifies — spend a budget, compose a wave, send it. One mechanic serves three modes. See the section below |
| Real-time or simultaneous-commit? | **Simultaneous-commit** | Latency stops existing as a concept, and async play — the thing that makes a phone game get played — falls out for free |
| Authoritative server, or relay? | **Authoritative**, re-simulating | It costs almost nothing (Core + a socket) and it is the only way to arbitrate a mismatch or hide information |
| Same seed for both boards? | **Yes** — and constraint 8 now makes it near-mandatory | Identical boards make the match about the players. A per-player seed reintroduces "I got the worse board," which Pillar 3 exists to prevent |
| Ranked, rating, matchmaking? | **Out of scope, explicitly** | Named here so it is a later decision rather than a silent assumption |
| Does versus ship before inverted mode? | **No — but it is decided first** | Inverted mode is the single-player proving ground for the opponent-as-command-source seam |

## What must be true before design proceeds

Three checks, none of them a design task, all of them cheap. Any one failing changes this file.
**Two are now done; the one that is left is the one most likely to hurt.**

1. **`Verify replay` passes on ARM**, and `dotnet test` with it (next-steps §8).
   **Passes under emulation, 2026-08-13.** arm64 container, .NET 10 SDK, QEMU: build 0/0,
   **244/244 tests**, and `replay` **30/30 checkpoints against hashes recorded on x86_64**. The
   `Fix32`/`SimRandom` regime reproduces bit-for-bit on a different instruction set — which was the
   single assumption the whole design rests on, and it was previously untested.
   **Not yet closed.** QEMU exercises the real ARM64 JIT but advertises a different CPU feature set
   than an M-series or a Snapdragon, so the codegen paths a real device takes are not all covered.
   This rules out the crude failures; criterion 3 still wants silicon.
2. **A Godot 4.6.3 mono export runs on Android**, at all, with C# alive. There is no export preset yet.
   **Attempted 2026-08-14; still open, and still the critical path** — it gates the hardware half of
   check 1, since the phone is the ARM device that matters. The attempt found and cleared a framework
   conflict (`net8.0` project against a `net9.0` export template; retargeting the view alone fixes it,
   Core untouched) and then stopped at ~5 GB of missing toolchain against 6.3 GB of free disk. Godot
   calls .NET Android export **experimental** in its own error text. Full account in risk 3.
3. **The command volume of a full twelve-wave run is measured**, not extrapolated from station counts.
   **Done, 2026-08-13** — 45–320 commands per match, twelve boards. See §2 above; the extrapolation
   it replaced was low on count and right on order of magnitude.

## Handoff

**Answered 2026-08-15 by
[ADR-0008 — Make the Active Wave Hashed State, Written by a Command](../../engine-systems/decisions/ADR-0008-active-wave-as-commanded-state.md)** (status: proposed).
The composed wave becomes a field in `SimState`, written by a `SendWave` command that `CommandSystem`
validates against the budget in phase 1 and `SpawnSystem` reads in phase 3. Normal mode fills the same
field from the table, so there is one read path for all three modes.

The ADR's own finding is that this **dissolves** the question below rather than answering it: an
opponent can live anywhere — `Gridfall.Verify`, a keyboard, a socket — because the only thing any of
them can do is produce a command. Core does not grow an AI and does not need to know one exists. The
original framing is kept below because the ADR's options section argues against it directly.

To `engine-systems` — and it merges with the handoff already sitting at the end of
`inverted-mode-requirements.md` rather than queueing behind it.

**The ADR is one question, not two:** where does an opponent's intent enter the simulation? Versus mode
constrains the answer that inverted mode left open, because a remote human's intent can only arrive as
a command. If that seam is built once, `PlayPolicy` and a network peer are both command sources and
neither is special.

**The mirrored decision sharpens it further, and mostly in `engine-systems`' favour.** With one `Sim`
per player, an opponent's intent does not enter *your* simulation as an opponent at all — it enters as
pressure on your board through the cross-board channel.

**And as of 2026-08-15 that channel is specified: composed waves, through the seam inverted mode
already found.** `content.Waves[state.WaveIndex - 1]` in `SpawnSystem.Run` and
`SpawnSystem.WaveComplete` — two places, verified, nothing else in Core indexes the wave table during a
run. Versus adds **no new seam** beyond what inverted mode must build there anyway; it adds a second
consumer of it. The ADR should therefore be written so the wave source is pluggable *once*, with the
table, the local human and the remote human as three cases of one thing.

Two smaller questions come with it, both now answerable rather than speculative:

- **Does `CommandQueue.Entry` need a seat field?** Probably not — see risk 4. Ownership looks like a
  routing property under mirrored, which would spare the trace archive entirely.
- **Do two `Sim`s in one process stay independent?** They should; `Sim` has no statics. Acceptance
  criterion 11 exists to make that a test rather than an assumption.
- **Where does budget legality live?** Constraint 9 says Core, by command rejection, on the model of
  `CommandSystem.BuildCost`. That keeps the server a relay-plus-commit-gate instead of a second rules
  engine, and it makes a cheating client desync itself into a refusal rather than an advantage.

**One thing this hands *back* to `game-design`, and it is not small.** Composed waves make the visitor
roster the flagship mode's core verb, and it is five archetypes with two traits inert or narrow.
`inverted-mode` risk 3 was the third independent thread to reach that conclusion; this is the fourth.
**Composing from five archetypes is not composing** — the roster work is now a dependency of versus,
not an improvement to it.

**A second, much smaller decision also belongs to `engine-systems`, and it is now unblocked:** may
`godot/Gridfall.Godot.csproj` target `net9.0`? Android requires it, it is measured as working
(builds clean, and Godot 4.6.3 loads and runs the assembly), and Core is untouched either way. The
csproj comment claiming ADR-0001 pins this is **wrong on the facts** — ADR-0001 decides the Core/view
boundary, not a framework — so this is a comment to correct and a one-line change to accept, not an
ADR to amend. It is worth doing on its own schedule: it costs nothing, and leaving the comment as-is
means the next person re-derives all of the above.

Nothing here should be built before the three checks above return.
