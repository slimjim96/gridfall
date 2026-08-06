# 10 · Recipe — Add a Tower, End to End

Every layer, in order, for a new tower. Running example: **Frost Spire**, which slows creeps in a small
radius.

This recipe crosses four workspaces. Each step names which one owns it, because the load/skip rules
change as you move — you are not meant to be holding all of this at once.

## 0 · Does it earn a slot? — `game-design`

Pillar 5: few towers, deep interactions. A new tower must justify itself against the two it most
resembles. "A faster version of the arrow tower" is a rejection, not a pitch.

Frost Spire earns it: it is the first tower whose value is *not* damage, which changes what the other
seven are for.

Output: a requirements file, then a design spec naming the knobs — `slowAmount`, `slowDuration`,
`radius` — with intent and no values ([WF-01](../../workflows/pipeline/01-requirements-analysis.md),
[WF-02](../../workflows/pipeline/02-game-design.md)).

## 1 · Does it need new behavior? — `engine-systems`

Two very different paths, and picking wrong is the most expensive mistake in this recipe:

| If the tower… | Then it is |
|---|---|
| Fires a projectile that damages one target | **Content only.** A new JSON file, no code. |
| Does anything the engine cannot already express | **A systems change.** Architecture note required. |

Frost Spire applies a slow, and nothing in the engine slows anything yet. So: a systems change first,
following [Chapter 09](09-recipe-new-system.md) — a `SlowSystem` in phase 6, `CreepSlowFactor` and
`CreepSlowEndTick` on `SimState`, hashed, with `MovementSystem` multiplying speed by the factor.

**Build the mechanic before the tower.** A tower is a configuration of mechanics; if the mechanic does
not exist, you are not adding a tower, you are adding a system that happens to have a tower attached.

## 2 · Define the data — `content-data`

```json
// content-data/towers/frost-spire.json
{
  "id": "frost-spire",
  "name": "Frost Spire",
  "cost": 90,
  "range": 2.5,
  "cooldown": 1.2,
  "damage": 0,
  "targeting": "furthest-along-path",
  "effects": [
    { "kind": "slow", "amount": 0.35, "durationSeconds": 1.5, "radius": 1.0 }
  ]
}
```

- Seconds here, ticks inside Core — the loader converts once
  ([Chapter 07](07-content-loading.md)).
- `0.35` becomes `Fix32` through `FromFraction(35, 100)`. No float exists in that path.
- The values come from a **balance pass**, not from taste
  ([WF-X1](../../workflows/cross-cutting/content-balance-pass.md)). Ship placeholders if you must, but
  say in the release note that they are untuned.

## 3 · Load and validate — `engine-systems`

If `effects` is a new field, the loader needs to parse it, validate it (`amount` in (0,1], `radius` > 0),
and every existing tower file needs the field or an explicit default. A missing required field is a
startup crash on purpose.

## 4 · Placeholder art — `presentation`

**Before any final art exists**, the tower needs to be playable. The placeholder standard
([`presentation/docs/placeholder-standard.md`](../../presentation/docs/placeholder-standard.md)) says:
procedural C# geometry, distinct silhouette, palette slot, under an hour.

Frost Spire: a tapered hexagonal prism, cool-blue gradient, taller and thinner than the arrow tower so
the two never read alike at a glance. No detail. It exists so the game can be played and balanced this
week.

## 5 · Register in the view — `presentation`

```csharp
// godot/ — the placeholder factory
case "frost-spire": return new PlaceholderTowerView(Prism(sides: 6, taper: 0.6f), Palette.Frost);
```

Behind `ITowerView`, so the eventual Ludo.ai asset — sprite sheet or `.glb`, both supported — drops in
without touching gameplay code ([ADR-0004](../../engine-systems/decisions/ADR-0004-view-asset-abstraction.md)).

Subscribe to the events the mechanic emits: `CreepSlowApplied` gets a frost tint and a small particle
burst. Events, not polling.

## 6 · Write the asset prompts — `presentation`

The placeholder is disposable; the prompt is the durable artifact. Following
[WF-X4](../../workflows/cross-cutting/asset-prompt-pass.md), write
`presentation/prompts/tower-frost-spire.md`: the Ludo.ai prompt in both sprite and mesh form, the style
anchor tying it to the existing towers, and the animation prompts (idle, fire, sell).

Do this **while the tower is fresh**, not in a later art pass. The design intent — "cool, still,
unsettling; it does not shoot, it chills" — is in your head now and gone in a month.

## 7 · Verify — `production`

| Check | Where |
|---|---|
| `dotnet build` 0/0, `dotnet test` green | Build gate |
| Determinism trace with the tower in play | Core changed, so this is mandatory |
| Hash covers `CreepSlowFactor` / `CreepSlowEndTick` | Hash-coverage test |
| Slow does not stack; refresh replaces | The design spec's interaction rule |
| Balance sim: leak rate, gold curve, time-to-clear vs. targets | `--balance --runs 200` |
| Silhouette distinct from every other tower | **Human** — agents cannot see it |
| Frost tint readable at wave-18 density | **Human** |

The last two are NOT-VERIFIABLE-BY-AGENT and say so in the report
([WF-05](../../workflows/pipeline/05-verification.md)).

## 8 · Release — `production`

The release note lists the new knobs and whether they are tuned. Three untuned knobs on a new tower is
normal and fine — recorded as follow-ups in `content-data`, not as a memory.

## The order matters

Mechanic → data → placeholder → prompts → balance → release.

The two common ways to get it wrong:

- **Art first.** You produce a beautiful asset for a tower whose numbers later make it unshippable.
- **Balance before the placeholder exists.** You cannot playtest what you cannot see, and the sim
  agrees with itself about towers nobody has played against.
