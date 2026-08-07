# Early Economy 2 — v1

**Slug:** `early-economy-2` · **Status:** done · **Verified at trace:** `240e010e52e5d640`

## What Shipped

The difficulty curve has a shape. Waves 1–4 are flat; the ramp starts at wave 5 and runs at 1.14.

```json
"hpGrowth": 1.14,
"hpGrowthFrom": 4
```

- **`hpGrowthFrom`** — a new wave-table field: the wave the ramp starts from. Defaults to 1, which
  reproduces the previous curve exactly, so `gauntlet` is provably unchanged.
- **The balance report now splits runs lost** into waves 1–10 and 11+, against the two targets
  `balance-targets.md` has carried since it was written, and reports the mean wave a lost run died on.

## The result that matters

The brief was "wave 3 leaks 14.1%, worst wave for six passes". **Wave 3 was a symptom.**

`balance-targets.md` has always had two runs-lost targets. The sim printed one number, labelled it
`15-30% late`, and checked it against a 0–60% band that appears nowhere in the doc. Split:

| | Before | After | Target | |
|---|---|---|---|---|
| Runs lost, waves 1–10 | **25.5%** | **3.5%** | 0–5% | **ok** |
| Runs lost, waves 11+ | **0.5%** | **21.5%** | 15–30% | **ok** |
| Lost runs died at wave | **4.3** | **10.9** | — | |
| Wave 3 leak | **14.1%** | **4.3%** | ≤ 15% | ok |
| Wave 12 leak | 0.1% | 6.7% | ≤ 15% | ok |
| Leak rate | 1.2% | 1.6% | ≤ 4% | ok |

The 26.0% that six passes read as "ok" was 25.5% early and 0.5% late — the exact inverse of intent. The
game was decided by wave 4 and the last eight waves were a formality.

**This is the first configuration to hit both runs-lost targets at once.** `income-vs-difficulty`
claimed that title by hitting one target twice without splitting them.

The leak distribution inverted with it:

| | waves 1–5 | waves 11–12 |
|---|---|---|
| Before | **91%** of all leaks | 1.6% |
| After | 12% | **87%** |

## Why six passes could not fix it

`hpGrowth` is one scalar applying **from wave 1**. Wave 3 sits at `growth²`, wave 12 at `growth¹¹`. Any
rate that threatens wave 12 also inflates waves 2–4 — and waves 2–4 are where the player is broke, so
they were the binding constraint on the whole curve.

Every previous pass faced a forced choice between a lethal opening and a trivial ending, and took the
ending, because that was the half the targets could not see.

`content-data/CONTEXT.md` says **one knob per pass**, for attribution. That rule is why this survived six
attempts: the curve needed its two ends moved in opposite directions, which one knob cannot do. The rule
is right about attribution and wrong about arity — sweep a **grid** and attribution survives.

## The fix that was not shipped

Two knobs do separate the ends, and `startingGold 500 / hpGrowth 1.10` passes every target with lost
runs dying at wave 12.0. It was rejected:

| | gold 500, growth 1.10 | **flat to 4, then 1.14** |
|---|---|---|
| lost, waves 1–10 | 0.0% | 3.5% |
| lost, waves 11+ | 27.5% | 21.5% |
| died at wave | 12.0 (range **11–12**) | 10.9 (range **3–12**) |
| starting gold | 500 | **300, unchanged** |

With 500 gold **nobody can lose before wave 11** — ten waves with no stakes, which is a gate rather than
a curve. And 500 gold is ten towers before wave 1, which turns the opening from a decision into a
formality. Deaths spanning waves 3–12 keep a tail.

The decisive measurement: **wave 3 lands at 4.3% in every shaped row, at any rate, at unchanged starting
gold.** The six-pass problem was never the player's wallet — wave 3 carried `1.08²` of scaling before
the player owned a board. Remove the scaling from the opening and wave 3 fixes itself.

## Player-Facing Change

The first four waves are a real opening: the same enemies at the same strength, while you build a board
and learn the route. Difficulty starts at wave 5 and does not stop.

The last two waves are now where runs end. Wave 12 leaks 6.7% against a board of 56 towers.

## Also Worth Knowing

- **The shipped configuration is not robust to casual edits.** Growth 1.135 → 1.14 → 1.145 moves
  runs-lost 11% → 29% → 47% — a 0.005 change swings it ~18 points, because `growth⁸` amplifies rate
  changes and the wave-12 margin is thin. Not `gauntlet`'s cliff (0.005 → 0% to 90%), but close enough
  that `difficulty-slope` is now the pressing follow-up.
- **Do not lower `hpGrowthFrom` to 1 "for consistency".** The flat opening is what allows the late rate
  to be 1.14 instead of 1.08. There is a note in the wave JSON saying so.
- **`gauntlet` is fully trivial** — 0% leak, 0% runs lost, 20/20 lives — and the split metric is what
  made that legible. It is unchanged by this pass and needs the shaped curve too, but it has a known
  cliff at 1.125.
- The trace diverged at **tick 900**, exactly where the script starts wave 2, predicted from the content
  change before the replay ran.

## Follow-Ups Not Done

| Item | Workspace | Slug |
|---|---|---|
| `gauntlet` is fully trivial and has a cliff at 1.125 — the shaped curve may be its answer | content-data | `gauntlet-cliff` |
| A 0.005 growth change swings runs-lost ~18 points; nothing measures slope | tooling | `difficulty-slope` |
| The 1–10 / 11–20 split is inherited from a 20-wave design and buckets 10 of 12 waves as "early" | content-data | `wave-band-targets` |
| The disputed HP-growth band (1.10–1.18) is now met by the late rate, 1.14 | content-data | `hp-growth-target` |
| `TowerDef.SellValue` is loaded and read by nothing | engine-systems | `dead-sell-value` |

## Known Not Verified

- Whether a flat four-wave opening reads as a tutorial or as filler. That is a pacing claim and needs a
  human.
- Whether the 1–10 / 11–20 band split is the right one for a 12-wave game. It is applied as written.
