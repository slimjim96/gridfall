# Balance Baseline — crossroads

**Date:** 2026-08-06 · **Map:** crossroads · **Runs:** 50 · **Seed:** 1
**Policy:** competent-beginner

The first balance numbers this project has that describe a game somebody is playing. Everything before
this described an undefended board.

## Intent

No knob was changed. This is a **baseline**: what the placeholder content does when played, so future
passes have a before to compare against.

## Results

| Metric | Value | Target | |
|---|---|---|---|
| Leak rate, overall | 3.7% | ≤ 4.0% | ok |
| Runs lost | 2.0% | 15–30% for waves 11–20 | n/a — only 3 waves exist |
| Lives left, avg | 18.0 / 20 | — | |
| Towers built, avg | 11.1 per run | — | |

### Per wave

| Wave | Spawned | Leaked | Leak % | Ticks to clear | Gold at wave start |
|---|---|---|---|---|---|
| 1 | 400 | 0 | 0.0 | 300 | 150 |
| 2 | 700 | 16 | 2.3 | 585 | 14 |
| 3 | 1000 | 61 | 6.1 | 843 | 6 |

## Reading

**The ramp is real.** 0% → 2.3% → 6.1% is a difficulty curve rather than a cliff, which is more than
placeholder numbers had any right to produce. Nothing here needs an emergency fix.

**The economy tightens fast, maybe too fast.** Gold at wave start goes 150 → 14 → 6. By wave 3 the
player is broke every run. Against the target "idle gold at wave 10: 0–2 tower costs" that looks
healthy, but with a no-reserve policy it is partly an artefact — a policy that saved up would show a
different curve. Worth re-checking when there are ten waves rather than three.

**Time-to-clear is over target and rising.** 300 / 585 / 843 ticks is 10 / 19.5 / 28 seconds. The
target band is 20–45 s, so wave 1 is *under* it — it ends before it starts being interesting.

**Wave 3's 6.1% is within the 15% per-wave ceiling** but it is the last wave that exists. The trend
suggests wave 4 or 5 would breach it, which is the useful prediction here.

## What this cannot tell you

- **Only three waves exist.** Every target about waves 10–20 is unmeasurable. This says nothing about
  the late game because there is no late game.
- **The policy is a floor, not a ceiling.** A reasonable beginner: coverage placement, best
  damage-per-gold affordable now, no saving up, no deliberate re-mazing, never sells. A good player
  does better, so "even played this way, wave 3 leaks 6%" is sound; "wave 3 is tuned" is not.
- **Nothing in the simulation is random.** Variance across the 50 runs comes entirely from the policy's
  jitter — it picks among the top 3 placements. That makes N runs meaningful, but the spread measures
  player variation, not game variation.

## Verdict

**Kept — no change.** This is a baseline, and the content is not obviously broken. The actionable
follow-ups are content, not tuning:

| Item | Why |
|---|---|
| More waves — at least 10 | Every late-game target is currently unmeasurable |
| Lengthen wave 1 | 10 s is under the 20–45 s band; it ends before it engages |
| Re-run after wave 4+ exists | The leak trend predicts a breach around wave 4–5 |

## Reproduce

```bash
dotnet run --project Gridfall.Verify -c Release -- balance --map crossroads --runs 50 --seed 1
```
