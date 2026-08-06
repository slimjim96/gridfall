# WF-X1 · Content & Balance Pass

**Workspace:** `content-data` · **Role:** content-designer
**Injection points:** leaf generation (3), gap detection (4)

## Fires when

Any number changes: a tower stat, an enemy stat, a wave composition, a map layout, an economy value.
Also fires when a slice ships a new knob with no default.

## Load

- `content-data/docs/balance-targets.md`
- The one data file being changed
- The two nearest-neighbor definitions (the towers/enemies this one competes with)
- The last balance report for this map
- The design spec's tuning-knob table, for the *intent*

## Never load

`engine-systems/**` · `presentation/**` · the full catalogue of towers and enemies · any code

## Steps

1. **[deterministic]** Find the intent. Every knob has one in a design spec: what raising it is *for*.
   No intent → **gap detection: ask one question** or push it back to `game-design`. A number with no
   intent cannot be evaluated, only argued about.
2. **[deterministic]** Record the current baseline: run the balance sim **before** changing anything.
   Without a before, the after means nothing.
   ```bash
   dotnet run --project Gridfall.Verify -- --balance --map <map> --runs 200 --seed 1
   ```
3. **[deterministic]** Change **one knob**. One. Two knobs in one pass and you cannot attribute the
   result to either.
4. **[deterministic]** Validate the file against the def schema. A malformed def fails at load, in the
   editor, in front of the human.
5. **[deterministic]** Re-run the sim with the same seed and run count.
6. **[model call: leaf generation]** Write the delta report: leak rate, gold curve at waves 5/10/15,
   time-to-clear, and share of runs lost — each as before → after, against the target.
7. **[deterministic]** Judge against `balance-targets.md`. Inside target → keep. Outside → revert and
   say so. A pass that reports "moved the number, missed the target, kept it anyway" is not a pass.
8. **[deterministic]** If the change was to a **map**, additionally verify:
   - every spawn still reaches the goal
   - no build placement can fully block a lane (the never-fully-blockable rule)
   - the path length at maximum mazing is within the wave timing budget
9. **[deterministic]** Regenerate the `.tres` resources from JSON. Never hand-edit them.

## Output

`content-data/docs/reports/[YYYY-MM-DD]-[slug]-balance.md`, plus the changed data file.

```markdown
# Balance Pass — [knob]
**Date:** · **Map:** · **Runs:** · **Seed:**

## Intent
## Change
| File | Field | Before | After |
## Results
| Metric | Before | After | Target | In target? |
| leak rate | | | | |
| gold @ w5/w10/w15 | | | | |
| time-to-clear | | | | |
| runs lost | | | | |
## Verdict
kept / reverted — because …
## Side Effects Noticed
```

## Done when

- [ ] Exactly one knob moved
- [ ] Before and after used the same seed and run count
- [ ] Every metric compared against a stated target
- [ ] Verdict is kept **or** reverted, with a reason
- [ ] `.tres` regenerated, JSON is still the source of truth

## Handoff

`handoff` to `production` if a slice is waiting on the value. Otherwise the report is the deliverable
and the data file is already live.

## Failure modes

- **Tuning by feel.** The sim costs seconds. Run it.
- **Moving two knobs.** You will learn nothing and believe you learned something.
- **Changing the seed between before and after.** Then you measured noise.
- **A new field instead of a new rule.** If the tower needs behavior it does not have, that is
  `engine-systems`. Data describes; it does not decide.
- **Skipping the map invariants.** A map that can be fully blocked is a soft-locked game, and it will
  not show up in leak rate — it shows up as a run that never ends.
