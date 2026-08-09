# Next Steps

**Written:** 2026-08-09 · **State:** `main`, tree clean · `dotnet build` 0 warnings 0 errors ·
**244 tests** · `replay` 30/30 · `maps` exits 0 · 12/12 maps valid and selectable, ten with terrain
that climbs and **five with rivers** · `arrow-station` is real art.

> **Five boards changed appearance and nobody has looked at them.** `river-bridges` shipped with its
> visual criterion explicitly unverified — see
> [the report](../05-verify/river-bridges-report.md) §What a human must look at. That is the first
> thing to do with a display in front of you.

**Start here, then read [`work-log.md`](work-log.md) before touching anything.** That file is shorter
than this one and it is where the traps live — the things that cost hours and would cost them again.

This handoff exists because the open threads span four workspaces and their ordering is not derivable
from any one folder. Everywhere else, the filename is the status.

**Nothing is blocked on a decision.** Two calls are the human's (§7) but neither stops work; everything
else is building and judgement, ordered below by what I would pick up first.

### The five-minute orientation

```bash
dotnet build && dotnet test                       # 0/0, 244
dotnet run --project Gridfall.Verify -- replay    # determinism; must be 30/30
dotnet run --project Gridfall.Verify -- maps      # geometry + MapValidator, exits 1 on error
./run-game.sh                                     # play it  (Windows: .\run-game.ps1)
./run-editor.sh spiral                            # look at a board; `,` and `.` sculpt the ground
```

`GODOT_BIN` overrides Godot discovery on any platform. Godot must be the **mono** 4.6.3 build — a
standard build silently ignores every C# script (ADR-0005).

---

## Open — in the order I would take it

Everything below is genuinely open. Closed threads are one line each in §Settled, with a pointer to
the record; the detail is not repeated here.

### 1. Make the husk mean something — a decision, priced, and it is yours

**This is what §1 turned into.** The policy learned about fussiness (§Settled); it can buy a cannon
now and on all twelve boards it still never does. Every balance figure came back byte-identical. The
rock-paper-scissors is in the roster and **not in the content**: the crossover is at average fussiness
4 weighted by appetite, every shipped wave table peaks at 1.53, and the arrow station stays 22.5%
better value even on the most armoured wave in the repo.

Three levers, all cheap, all with different blast radii:

| Lever | What it takes | What it costs you |
|---|---|---|
| **Cannon at 73 gold** (from 90), or serving 49 (from 40) | one number, one file | makes burst better value in the *late* game on every board that offers it; nothing has been measured that way |
| **90 husks in wave 12** (from 19) | one wave, twelve tables | one wave asks the question; wave 12 goes from 147 visitors to 218 |
| **Nothing** | — | defensible if the husk is flavour — but then `husk.json`'s `_asks` claims something the content does not deliver, and two roster targets stay failing |

Raising `fussiness` is **not** on the list: past 11 the arrow station is already floored at 1 per hit
and more armour only subtracts from the cannon. Full pricing:
[policy-fussiness balance](../../content-data/docs/reports/2026-08-09-policy-fussiness-balance.md).

Related and now measured for the first time: `balance-targets.md` asks that no station appear in more
than 70% of winning runs. It is at **100%**.

### 2. Inverted mode — requirements written, and the ADR gates everything

[`inverted-mode-requirements.md`](../01-requirements/inverted-mode-requirements.md). You spend a budget
sending visitors and score the ones that arrive; the game builds the stations. **Both directions ship.**

Three things are already true and were checked, not assumed:

- **The opponent already exists.** `PlayPolicy` is it — and it would pass Core's purity gate today.
  Moving it in is a relocation, not a rewrite, which frees the ADR to be decided on the right grounds.
- **The seam is one expression in one file.** `content.Waves[state.WaveIndex - 1]`, in
  `SpawnSystem.Run` and `SpawnSystem.WaveComplete`. Nothing else in Core indexes the wave table.
- **The difficulty ladder is already measured, and it says the mode is unwinnable.** Leak rate *is* the
  attacker's score: 0.0–1.6% across twelve boards, and exactly 0.0% on four of them.

And three risks, in order: the opponent and the measuring instrument are **the same code** (strengthen
it and the whole balance archive describes a different game — separate them *before* touching either);
every board has **one spawn**, so the attacker has no spatial decision; and the player's toolkit is
five archetypes.

**Do nothing before the ADR**: where the opponent lives and what part of it is simulation state.

### 3. Ten stations — requirements are written, and wave 0 is yours

[`station-pool-requirements.md`](../01-requirements/station-pool-requirements.md) specifies the roster
by **role**, in six shippable waves, with the counterpart visitor trait each role needs to be a
decision rather than a stat line.

Two things to know before picking it up:

- **Wave 0 is §1 above and it is not optional.** "Slower but stronger" is only a choice when something
  punishes many-small-hits. Until the fussiness share moves, waves 2–5 cannot be *measured* — they can
  be built, but every balance figure would describe a game where the cheapest station still wins.
- **Wave 1 is free and unblocked.** `anchor` and `longshot` need no engine work at all; `sapper` and
  winding routes already ask for them. Two JSON files and a roster edit.

Also newly blocking, and it was already written down: **`themed-unit-palettes`.**
`board-themes-direction.md` says decide it *before* `station-pool` ships. Ten stations in a palette
that already owns most of the warm spectrum is exactly the collision it predicted — and water is now
competing for the cool end too.

### 4. Make elevation mean something — the follow-on that was always planned

Boards climb (§Settled). A station on a rise reaching further is not a new rule — it is the shipped
*height means range* rule composing. But it turns elevation into **simulation input**: its own ADR,
`Fix32` heights rather than float, targeting in 3D, and a trace re-record. The current field was built
so this is additive.

One thing to know first: the route is carved flat with a level shelf either side, for readability. The
moment height affects range, that shelf becomes a **balance** decision rather than a cosmetic one.

### 5. Content judgement on the level set

Five of ten boards are degenerate (0.0% lost, sd ≤ 0.4) and `comb` sits at 42%. §Settled explains why
knob-tuning cannot fix that and why no map metric predicts it. What is left is a person deciding
whether these ten are the shipped set, a smaller tuned set, or generator output kept as examples.

### 6. Two presentation calls nobody has made

- **Ten of twelve maps emit no `PathOnly`.** The route exists only in the flow field, drawn only by the
  *editor's* overlay — which the game does not have. Should the generator emit roads, or is an unmarked
  route intended?
- **Six of ten palettes are blue-grey.** `atoll` (tundra) and `switchback` (slate) read as one board at
  thumbnail size. The three newest are no worse than the seven that predate them; the set is just
  crowded at the desaturated end.

### 7. Tier 2's soft-lock question — priced, still yours

Three options costed against the engine in
[`tier2-soft-lock-options.md`](../../game-design/docs/tier2-soft-lock-options.md). **A** is one line —
`ServingSystem` already floors at 1. **B** turns wave tables into generator inputs. **C** needs a slow
mechanic that does not exist and swaps the soft-lock for a worse one.

The decision is not technical: **should a child be able to finish a level without doing the
arithmetic?** A says yes-but-slower, B says the question never arises, C says no.

Related, and also yours: **the theme is open and was reopened for better candidates on 2026-08-09** —
[`theme-direction.md`](../../game-design/docs/theme-direction.md). Three of the five old candidates are
now cut, and not on taste: the board direction committed to boards being *places*, elevation shipped,
rivers shipped, and The Wash is a room, Please Hold is an office, Bin Night is a street. Four new
candidates are written to that filter. Only `Appetite` and `Serving` carry a theme in the code;
everything else in the vocabulary is already theme-free, and the rename cost is measured, not guessed.

**Until it closes, do not run a Ludo batch on stations or visitors.** Terrain is safe — a mountain is a
mountain under every candidate, and so is a river.

### 8. Verify cross-platform on real hardware

Locale, line-ending, enumeration-order and float hazards are all closed and each has a test
(`tech-standards.md` §Cross-platform) — but those tests have only ever *run* on Linux. The whole check
on a Mac or Windows box is two commands:

```bash
dotnet test
dotnet run --project Gridfall.Verify -- replay
```

If the trace hashes match there, the claim stops being a claim.

---

## Settled — do not re-derive these

| Thread | Outcome | Record |
|---|---|---|
| The reframe and rename | Accepted and shipped; behaviour untouched, `replay` passed unchanged | [`fulfilment-direction.md`](../../game-design/docs/fulfilment-direction.md) |
| Are the levels legible? | All twelve looked at in-engine; four motifs redrawn | [`example-levels.md`](../../content-data/docs/example-levels.md) |
| Wave-table length | Band restated for the twelve waves that exist; **not** grown to 20 | [`balance-targets.md`](../../content-data/docs/balance-targets.md) |
| `route-variance-metric` | **Closed.** Seven predictors ruled out and the reason none can work: outcomes are stable to seed and chaotic in inputs | [balance report](../../content-data/docs/reports/2026-08-08-example-levels-balance.md) |
| Can `comb` be tuned by composition? | No. Fifteen configurations, non-monotone, 42% is structural | same report |
| Elevation | Shipped, view-only; a hilly board hashes identically to the same board flat | `docs/iso-grid.md` §Elevation |
| Does the play policy understand fussiness? | **Shipped.** It ranks by effective serving and will hold gold for the station it wants. Two blocks, not one — the second was "never substitute down" | [policy-fussiness](../06-release/policy-fussiness-v1.md) |
| Does teaching it move the balance figures? | **No.** All twelve maps byte-identical. The content never asks the question — see §1 | [balance report](../../content-data/docs/reports/2026-08-09-policy-fussiness-balance.md) |
| Station price | **Left as is**, by decision (2026-08-09). The cannon stays at 90; §1 is about the content asking for it, not about the price | this file, §1 |
| Rivers and bridges | **Shipped, view-only and *enforced*** — water is a load error on any cell that is not already blocked. Five boards, zero numbers moved. The *look* is unverified | [river-bridges](../06-release/river-bridges-v1.md) |
| Do rivers affect pathing? | **No, by decision.** Water as a real cell kind with bridges as chokepoints is a different game and a different slice — ADR, validator change, trace re-record | same |

What each of those *taught* is in [`work-log.md`](work-log.md) — read that before starting anything,
it is shorter than this file and it is where the traps are.

## Where the state actually lives

| Thing | File |
|---|---|
| The ten levels: metrics, motifs, elevation styles, and what a human must still eyeball | `content-data/docs/example-levels.md` |
| All twelve measured, the knob sweeps, the runway result | `content-data/docs/reports/2026-08-08-example-levels-balance.md` |
| Bands, cover/useful figures, balance history | `content-data/docs/balance-targets.md` |
| What the balance harness's player actually does, and what it deliberately does not | `Gridfall.Verify/PlayPolicy.cs` header, `tooling/specs/policy-fussiness-tool-note.md` |
| The accepted reframe (why the code says Station/Visitor) | `game-design/docs/fulfilment-direction.md` |
| The three soft-lock options, costed | `game-design/docs/tier2-soft-lock-options.md` |
| Regenerate maps / schematic atlas / iso atlas | `content-data/maps/make-example-levels.py`, `render-atlas.py`, `capture-iso-atlas.py` |
| Per-map balance reports, newest first | `content-data/docs/reports/` |
| What happened last session and what it taught | `production/docs/work-log.md` |
| What the game is *about* (open, on purpose) | `game-design/docs/theme-direction.md` |
| Cross-platform guarantees and what enforces them | `docs/tech-standards.md` §Cross-platform |
| How a grid coordinate becomes a pixel, and elevation | `docs/iso-grid.md` |
| The engine itself, eleven chapters | `docs/engine-guide/README.md` |

Regenerating is all-or-nothing and validates before writing:

```bash
python3 content-data/maps/make-example-levels.py   # maps + elevation, validated before writing
python3 content-data/maps/render-atlas.py          # schematic contact sheet, headless
python3 content-data/maps/capture-iso-atlas.py     # real in-engine frames, needs a display
```

It is **idempotent** — regenerating a clean tree leaves it clean. If it does not, something is
non-deterministic and that matters more than whatever you were doing.
