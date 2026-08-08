# Board Select & Premium Cue — v1

**Slug:** `board-select` + `premium-hud-cue` · **Status:** done · **Verified at trace:** unchanged

Two small gaps left by the previous two slices, closed together because both are HUD-and-scene work.

## Board select

`run-structure` chose option A — a run is one board, self-contained — but the map was a `const` in
`GameplayScene`, so **`gauntlet` shipped unreachable** and "selectable" was true only in the
requirements doc.

- A keypress list, shown before the first run and again the moment a run ends, so finishing one board
  and choosing the next is **one screen rather than a dead end**.
- Boards are discovered from `content-data/maps/` — the filesystem is the map manager, the same rule
  the board editor follows. A map dropped in appears with no registration.
- Size and theme are read off each map, so the list cannot go stale the way a hand-written one would.
- `--map <id>` for captures and the launcher; a static `PendingMapId` carries the choice across
  `ReloadCurrentScene`, the same mechanism `PlaytestDraft` already used.

Deliberately not a menu system. There is no meta-progression to hang one on yet.

## Premium cue

`wave-pacing` shipped `midWaveBuildPercent 115` on crossroads — towers cost 15% more during a wave —
**and nothing on screen said so.** A price that silently changes is indistinguishable from a bug, and
a decision whose cost the player cannot see is a surprise rather than a decision (pillar 4).

The HUD now shows the live price and flags it: `[Arrow Tower 57 +premium]`, amber. Amber is the
"an offer, not a warning" slot — the premium is a price to weigh, and the reserved red would misread
as a refusal.

**The number comes from the sim's own pricing function.** `Sim.BuildCostOf` delegates to
`CommandSystem.BuildCost`, the same call that deducts the gold, so the HUD cannot quote a price the
sim does not honour. `ThePriceQuotedIsThePriceCharged` pins exactly that.

## Verification

`dotnet build` 0/0 · **200 tests** (+4) · `replay` 30/30, trace unchanged · `--map gauntlet` loads and
plays · three gameplay baselines re-recorded, since the HUD line changed in every frame.

Captured and read back: `[Arrow Tower 57 +premium]` at 50 base × 115%.

## Known Not Verified

- **The selector has not been driven by hand.** Keypress routing and `ReloadCurrentScene` are not
  reachable from a capture; a human should press 1 and 2 and confirm both boards load and that `esc`
  quits from the list.
- Whether ending a run straight into a board list feels right, or wants a beat first.
- `prepTicks 300` is still an untuned placeholder — unchanged by this slice.
