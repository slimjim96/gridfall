# 08 · Determinism Playbook

The harness went red. This chapter is what to do about it, in order, so you are not guessing at 2am.

## What the harness actually does

A **trace** is the complete input to a run:

```json
{
  "map": "crossroads", "seed": 12345, "ticks": 5400,
  "commands": [ { "tick": 30, "cmd": "build", "cell": [4,7], "def": "frost-spire" },
                { "tick": 96, "cmd": "startWave" } ],
  "hashes":   { "0": "a7f3…", "100": "91bc…", "…": "…" }
}
```

Replay reconstructs the sim from map + seed, applies commands at their recorded ticks, and compares the
hash at each checkpoint. First mismatch wins — everything after it is noise.

```bash
dotnet run --project Gridfall.Verify                                  # all traces
dotnet run --project Gridfall.Verify -- --trace crossroads-baseline --verbose
```

## The first question: which kind of red is it?

| Symptom | Kind | Go to |
|---|---|---|
| Same machine, same build, run twice → different hashes | **A · Nondeterministic within a build** | §A |
| Same machine, hashes match; different machine → diverge | **B · Platform-dependent** | §B |
| Old trace fails, new runs are self-consistent | **C · Behavior changed** | §C |
| Hashes match but the game plays differently | **D · Unhashed state** | §D |

Answer this before touching any code. The four have almost disjoint cause sets, and the common failure
is debugging A while looking at C.

## §A · Nondeterministic within a build

Something in the tick loop is reading an unstable source. Ranked by how often it is the answer:

1. **`Dictionary` or `HashSet` iteration** in a state-affecting path. Iteration order depends on
   insertion history and hash seeds. Search: `foreach.*(Dictionary|HashSet|\.Keys|\.Values)`.
2. **Slot order instead of id order.** After the first swap-remove, slot order stops matching id order.
   Search for `for (int i = 0; i < CreepCount; i++)` in `Systems/` — every one of those should be
   `CreepSlotsByIdAscending()` ([Chapter 04](04-state-and-entities.md)).
3. **`System.Random`, `DateTime`, `Environment.TickCount`, `Guid.NewGuid`.** Search for all of them.
4. **`Parallel.For` / PLINQ** anywhere in Core.
5. **Object identity as a tie-break** — `GetHashCode()` on a reference type, or sorting by object
   reference.
6. **Static mutable state.** A cache on a static field carries between runs in the same process, so run
   two diverges from run one. The harness runs multiple traces per process, which is how this surfaces.

```bash
grep -rnE '\b(float|double|Random|DateTime|Guid|Parallel|AsParallel)\b' Gridfall.Core/
grep -rn 'foreach' Gridfall.Core/Systems/ | grep -E 'Dictionary|HashSet|\.Keys|\.Values'
```

## §B · Platform-dependent

Self-consistent on each machine, different between them.

1. **A `float` or `double` reached Core.** The grep above is the whole diagnosis. Look hardest at code
   added "temporarily" for a visual.
2. **`Fix32.ToFloat()` called inside Core** and the result fed back in.
3. **A platform math call** — `Math.Sqrt`, `MathF.*`, `Math.Sin`. Core uses `FixMath` exclusively.
4. **Culture-dependent parsing.** `double.Parse("0.35")` on a machine with a comma decimal separator.
   `ContentLoader` avoids this by never parsing to a float at all ([Chapter 07](07-content-loading.md)),
   but a new loader path can reintroduce it.
5. **Different content.** Confirm both machines have identical JSON — a stale `.tres` or an uncommitted
   tuning change looks exactly like a determinism bug and is far more common.

Check 5 first. It costs ten seconds and it is the answer more often than 1 through 4 combined.

## §C · Behavior changed

The trace is old and the game moved. This is not necessarily a bug — it is a bug only if you did not
mean to change behavior.

1. **Was the change intentional?** If a slice deliberately changed how targeting works, every trace
   recorded before it is now invalid. Re-record, and say so in the build notes and the release note.
2. **Was the change accidental?** Bisect the traces against commits:
   ```bash
   git bisect start HEAD <last-known-green>
   git bisect run dotnet run --project Gridfall.Verify -- --trace crossroads-baseline
   ```
3. Re-recording a trace is **never** the first response. Re-record only after you know why it diverged
   and have decided the new behavior is correct. A re-recorded trace is a green light you issued
   yourself; issuing it without a diagnosis converts your regression test into a rubber stamp.

## §D · Unhashed state

The nastiest one, because the harness says green.

Symptom: two runs hash identically but visibly play differently, or `Restore(Snapshot())` resumes into
a different game.

Cause: state exists that `Hash()` does not cover — almost always a field added without the matching
hash line and the matching test ([Chapter 04](04-state-and-entities.md)).

```bash
# every field on SimState, against every field mentioned in Hash()
diff <(grep -oP 'public \S+\[\]? \K\w+' Gridfall.Core/SimState.cs | sort) \
     <(grep -oP 'Combine\(h, \K[^)]+' Gridfall.Core/SimState.cs | tr ', ' '\n' | sort -u)
```

There is a snapshot round-trip test for exactly this. If it is failing at the same time, the missing
field is the one that differs after restore — start there.

## Narrowing to the tick

`--verbose` reports the first mismatched checkpoint. Checkpoints are every 100 ticks by default, so:

```bash
# bisect within the window
dotnet run --project Gridfall.Verify -- --trace X --checkpoint-every 1 --from 3400 --to 3500
```

Once you have the exact tick, dump both sides:

```bash
dotnet run --project Gridfall.Verify -- --trace X --dump-state 3447 > a.txt
# … on the other machine, or after the fix …
diff a.txt b.txt
```

`--dump-state` prints every hashed field in hash order, so the diff points straight at the field. Which
field it is usually names the phase, and the phase usually names the file.

## Before you close it

- [ ] The root cause is written in the build notes, not just fixed
- [ ] A test exists that fails on the old code — a determinism bug with no regression test will come back
- [ ] If behavior legitimately changed, traces were re-recorded **and the re-record is mentioned in the
      release note**
- [ ] If the cause was a missing hash field, the hash-coverage test for that field now exists

## Prevention, ranked by how much it saves

1. Add state and its hash line **in the same commit**, with the test.
2. Iterate by id, always, even where order does not matter yet.
3. Never let a `float` into Core, not even for a moment, not even behind an `#if DEBUG`.
4. Run the harness before pushing, not just `dotnet test`.
5. Keep traces short and numerous rather than long and few. A 5,000-tick trace tells you something broke
   somewhere; twenty 250-tick traces tell you which system.
