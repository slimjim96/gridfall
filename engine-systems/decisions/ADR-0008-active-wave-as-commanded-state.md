# ADR-0008 — Make the Active Wave Hashed State, Written by a Command

**Status:** proposed
**Date:** 2026-08-15 · **Raised by:** `inverted-mode` · `versus-mode`

## Context

Two slices need a wave to come from somewhere other than the content table, and they need it
differently.

`inverted-mode` has the human compose a wave from a budget while an AI defends. `versus-mode` — decided
mirrored with composed waves on 2026-08-15 — has *two* humans compose waves at each other across two
`Sim` instances, over a network, with one of them possibly running a modified client.

Today `SpawnSystem` reads `content.Waves[state.WaveIndex - 1]`. That expression appears in exactly
**two** places, `SpawnSystem.Run` and `SpawnSystem.WaveComplete`, and nothing else in Core indexes the
wave table during a run — verified, not assumed.

The constraints that force the choice:

- **`Restore()` then N ticks must equal N ticks, hash for hash.** There is a test for it, and it is the
  fastest way to find a field that was snapshotted but not hashed, or neither. Anything the spawner
  reads mid-wave is inside that contract.
- **A trace is map + seed + commands.** That is what makes replay exact and what makes the network
  protocol nearly free — `versus-mode` measured a whole match at 90–640 commands. Anything the spawner
  reads that is *not* one of those three has to be carried alongside the trace, by every consumer of a
  trace, forever.
- **The remote client is hostile.** An over-budget wave must be refused, and refused identically on both
  machines, or lockstep has a hole in it.
- **`versus-mode` runs two `Sim`s in one process.** Anything ambient — a static, a shared source object
  — stops being safe the moment there are two.

## Options

### A. The active wave is a field in `SimState`, written by a `SendWave` command

`CommandSystem` (phase 1) validates a composed wave against its budget and writes it into `SimState`.
`SpawnSystem` (phase 3) reads it from state rather than from content. Normal mode fills the same field
from the table when a wave starts, so there is one read path and the table becomes *an input to the
field* rather than a second way to spawn.

The wave is hashed and snapshotted because it is in `SimState`, which is where the contract already
applies. It reaches the sim as a command, so a trace already carries it.

Cost: `SimState` grows, and everything hashed that grows re-records the trace archive.

### B. Inject an `IWaveSource` into `Sim`

`Sim` takes a wave source at construction; `SpawnSystem` asks it for the active wave.
`TableWaveSource`, `LocalPlayerWaveSource`, `RemoteWaveSource`, `PolicyWaveSource`.

The cleanest-looking abstraction, and the one most people would reach for. It keeps `SimState` exactly
as it is, so no trace re-records.

Cost: the source holds state the spawner reads, and that state is **outside `SimState`** — therefore
outside `Hash()` and outside `SimSnapshot`. Restore-then-N-ticks stops being sound unless every
implementation serialises itself and `SimSnapshot` grows a slot for it. A trace stops being map + seed +
commands. This is precisely the "outside Core, serialised alongside the trace" shape that
`inverted-mode-requirements.md` flagged as making every trace consumer carry the opponent's state too.

### C. Move the opponent into Core as a system in the tick order

The AI that composes waves becomes a Core system with its RNG and cooldowns in `SimState`, hashed like
everything else. This is the option `inverted-mode` named as the alternative to B.

Cost, and it is fatal rather than expensive: **it does not answer the question for `versus-mode` at
all.** There the opponent is a remote human — there is no AI to place anywhere. C solves inverted mode
and leaves versus needing A or B regardless, which means building the seam twice. It also grows Core by
an entire AI to avoid growing it by one array.

## Decision

Chose **A**.

Deciding factor: **the snapshot contract.** `Restore()` followed by N ticks must produce the same hashes
as N ticks without the round trip. The spawner reads the active wave every tick of a wave, so the active
wave is simulation state by definition — and option B's central move is to store simulation state
outside the object that gets snapshotted and hashed. B does not fail because it is inelegant; it fails
because it puts a mid-wave read outside the only mechanism that proves mid-wave reads are restorable.

There is a second consequence that only became visible after choosing, and it is the larger one:

**A dissolves the question this ADR was raised to answer.** "Where does the opponent live?" stops being
architectural. The opponent — `PlayPolicy` in `Gridfall.Verify`, a human at a keyboard, a socket
carrying a stranger's command — can live anywhere at all, because the only thing any of them can do is
produce a `SendWave` command, and the command is the only thing that touches the simulation. Core does
not grow an AI, and it does not need to know one exists.

### The player composes the entries; the content supplies the envelope

`WaveDef` carries two different kinds of thing, and only one of them may be commanded.

| Field | Comes from | Why |
|---|---|---|
| `Entries` | **the player** | This is the composition. It is the decision the mode exists for |
| `AppetiteScale`, `PrepTicks`, `ClearGold`, `MidWaveBuildPercent`, `VariancePercent` | **content, at that wave index** | Tuning. A commanded `AppetiteScale` is a player setting their own difficulty multiplier |

This split is what keeps `versus-mode` constraint 8 ("nothing tuned per seat") enforceable: both boards
draw the same envelope from the same content at the same wave index, and the only thing that differs is
what each player chose to spend their identical budget on.

### Budget legality is a rejection in phase 1

An over-budget wave is refused by `CommandSystem` exactly as an unaffordable build is refused, with an
event naming the reason. `CommandSystem.BuildCost` is the precedent and the shape to copy.

Because both machines run the rule, a modified client that sends an illegal wave gets it refused on
**its own** machine too. It desyncs itself into a refusal rather than into an advantage, and the server
never needs to know what a wave costs.

## Consequences

### Good
- One read path. Normal mode, inverted mode and versus all spawn through the same code; the difference
  is only who wrote the field.
- The wave is hashed and snapshotted for free, because it is in the object that already is.
- A trace stays map + seed + commands, so `Verify replay`, the network protocol and the balance harness
  keep working on the same three inputs — and the measured 90–640 commands per match already includes
  wave commands at ~64 bytes each.
- Cheating is a Core-level rejection, not a server responsibility.
- Two `Sim`s stay independent: the wave lives in each instance's own `SimState`, so nothing is shared
  and nothing is ambient.
- `PlayPolicy` does not move, and does not need to. It gains one more command it can issue.

### Bad
- **`SimState` grows, so every recorded trace re-records — once.** Behaviour is unchanged and balance
  figures are unchanged, but hashes shift, because hashes are over state and there is now more state.
  This project has hit exactly this before: `Sim`'s constructor carries a comment about `ForceRebuild`
  bumping a hashed `Version` and shifting every hash in every trace, which the harness caught. Cheap
  today — one file, 8 KB — and steadily less cheap.
- **`versus-mode` constraint 4 and `inverted-mode` criterion 9 both say "same traces", and that is now
  wrong.** They should read: same behaviour, same balance figures, traces re-recorded once with the
  reason recorded. Flagged rather than silently reinterpreted.
- `SimState` gains a fixed-size wave-entry array (`MaxWaveEntries` is 16) that is dead weight in normal
  mode, where the table would have served.
- Phase 1 now decides something phase 3 depends on within the same tick. That ordering is already true
  of builds, but it is one more thing the tick order is load-bearing for.

### Forecloses
- **A wave that changes shape mid-flight.** The field is written when the wave is sent; a mechanic that
  rewrites an in-progress wave would need a second command and a rule about what happens to entries
  already spawned. Not wanted by either slice, and the door is not locked — but it is not open either.
- **Per-seat tuning**, deliberately. The envelope comes from content at a wave index, so there is no
  place to put a number that differs between two boards without changing this ADR.

### Does not foreclose
- Where the AI lives. That is now a `Gridfall.Verify` / Core packaging question with no determinism
  consequence, which is the point.
- Real-time versus later. The command still arrives at a tick; simultaneous-commit is a pacing choice
  above this layer, not a property of it.

## Notes

Supersedes nothing. Interacts with **ADR-0001** (the wave source stays inside Core, so the boundary is
untouched) and **ADR-0006** (no new phase; phase 1 writes, phase 3 reads, as builds already do).

The re-record should happen **with this change and not after it**. Two slices are queued behind this
seam, and every trace recorded before it pays the same cost later.
