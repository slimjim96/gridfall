# Inverted Mode — Difficulty Dial

**Date:** 2026-08-09 · **Maps:** `crossroads`, `comb`, `spiral`, `meander` · **Runs:** 100 per point, seed 1
· **Verdict:** the dial works, one global number cannot serve four boards, and **board quality turns out
to be mode-independent**

The design decision this measures: **the game ships both directions, and each mode leans toward the
human.** That means inverted mode cannot run on the shared content untouched — measured, an attacker
delivers 0.0–1.6% of its visitors and wins essentially never.

Two dials were added to the balance harness. Both are **mode-local**: they change no station, visitor,
wave or map, so normal mode cannot move when they are turned.

```bash
Verify balance --map crossroads --cap 20000       # total gold the defence may ever commit
Verify balance --map crossroads --perWave 600     # gold per wave, refilled each wave
```

Uncapped is the default and reproduces the twelve committed figures byte for byte — checked.

---

## 1. The curve, and where the band sits

Target, mirrored from `balance-targets.md`: **the attacking human wins 70–85% of runs.**

### Total budget (`--cap`)

| Board | 30,000 | 20,000 | 15,000 | 12,000 | 9,000 |
|---|---|---|---|---|---|
| `crossroads` | 23% | **73% ✓** | 94% | 99% | 100% |
| `meander` | 0% | 0% | **75% ✓** | 100% | 100% |
| `spiral` | 29% | 29% | 29% | 100% | 100% |
| `comb` | 43% | 43% | 100% | 100% | 100% |

*(attacker's win rate. Natural uncapped spend on these boards is 15,000–20,000g.)*

### Per-wave allowance (`--perWave`)

| Board | 1,600 | 1,200 | 900 | 700 | 550 | 420 |
|---|---|---|---|---|---|---|
| `crossroads` | 26% | 28% | 44% | 64% | 89% | 99% |
| `meander` | 0% | 28% | **75% ✓** | 100% | 100% | 100% |
| `spiral` | 66% | 53% | 100% | 100% | 100% | 100% |
| `comb` | 55% | 100% | 100% | 100% | 100% | 100% |

`crossroads` lands in band between 700 and 550 — call it **~620 g/wave**, and it is the only board
where the curve is smooth enough to interpolate.

## 2. One number cannot serve four boards

`spiral` goes **29% → 100%** across a single step. `comb` goes **43% → 100%**. There is no value of
either dial that puts those two in band, and no global setting that puts even the other two in band
together — `crossroads` needs 20,000 where `meander` needs 15,000.

**So the defence budget is per-board content, not a global difficulty setting.** That is the direct
answer to "for each scenario it should lean towards the player's advantage": each scenario needs its own
number, and the number is not derivable from the others.

## 3. The finding that was not being looked for: board quality is mode-independent

`balance-targets.md` asks for **≥ 3 waves that can end a run** — the target that separates a difficulty
curve from a single gate, and the one `comb` has always failed. Measured across every configuration of
both dials:

| Board | Waves that can kill, normal mode | Waves that can kill, across the inverted sweep |
|---|---|---|
| `crossroads` | 5 of 12 | **6–10 of 12** |
| `spiral` | 1 of 12 | **1–2 of 12** |
| `comb` | 1 of 12 | **1 of 12, at every single setting** |
| `meander` | 0 of 12 | **0–2 of 12** |

**A board that is a gate is a gate in both directions, and no amount of dial-turning changes it.**
`comb` decides on wave 12 whether the human is attacking or defending; `crossroads` spreads across two
thirds of its table either way.

This is worth more than the dial. It means **fixing the level set is not a normal-mode chore that
inverted mode will need repeating** — the same structural work serves both, and the boards that fail one
mode's quality bar are exactly the boards that fail the other's. It promotes
[`next-steps` §5](../../../production/docs/next-steps.md) from housekeeping to a shared dependency.

## 4. A correction, made before it reached a doc

The first sweep ran 350–4,000g and showed every run on `comb` ending on wave 5, at every setting. I
concluded the lifetime cap was **the wrong shape of dial** — that it makes a defence *stop* rather than
be weaker — and wrote that into `PlayPolicy` before checking it.

It does not hold. Natural spend on these boards is 15,000–20,000g, so the whole first sweep sat far
below the range where the knob does anything but starve the defence in the opening. In the band that
matters the two dials are comparable: on `crossroads`, `--cap 20000` and `--perWave 550` both give 8 of
12 waves able to end a run.

`--perWave` is still the better dial, but for a reason the measurement did not supply: it is a **rate**,
so the same number means the same thing on a table of any length, and it cannot exhaust itself. That is
a principle, not a result, and it is now labelled as one.

## 5. A method error worth not repeating

The first run of the sweep reported that the cap **had no effect at any value from 250 to 3,000** —
identical leak rate, identical stations, identical everything. That is a striking result and it was
entirely false: `dotnet run --no-build` had reused a Release binary compiled before the flag existed.

The tell was that it was *too* clean. A knob that does nothing usually does nothing *noisily*.

**`--no-build` after editing is how a harness measures the previous version of itself.** The same
family as the board editor's capture path, which painted over the map it was told to capture, and the
2026-08-08 balance rows that did not reproduce. Rebuild, then measure — and if a result is suspiciously
flat, check what binary produced it before writing it down.

## What this leaves for a person

1. **Pick per-board defence budgets** once inverted mode exists. `crossroads` ≈ 20,000g total or
   620 g/wave. Two boards cannot be put in band at all without fixing the boards.
2. **`comb` and `spiral` cannot host inverted mode** as they stand, for the same reason they are poor
   normal-mode levels. That is now one problem rather than two.
3. **The dials are not the mode.** They make an existing measurement answer a question about a mode
   that has not been built. Nothing here validates that inverted mode is *fun*.
