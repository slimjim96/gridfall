# WF-X2 · Isometric Presentation Pass

**Workspace:** `presentation` · **Role:** presentation-engineer
**Injection points:** leaf generation (3), gap detection (4)

## Fires when

Anything the player sees, clicks, or hears changes: rendering, camera, HUD, input picking, VFX, audio
hooks, or readability at a new wave density.

## Load

- `docs/iso-grid.md` — the projection contract, always
- `presentation/docs/art-direction.md`
- The relevant spec in `presentation/specs/`
- The **boundary section** of the architecture note: what you may read, which `SimEvent`s exist

## Never load

`engine-systems/decisions/**` · `content-data/**` · sim internals beyond the boundary list ·
`production/01-requirements/*`

## Steps

1. **[deterministic]** Start from the contract in `docs/iso-grid.md`. If your work needs to change a
   constant in it, **change the doc first**, in its own commit, and say who else is affected. Silently
   diverging from the contract is how a renderer and a picker stop agreeing about where a cell is.
2. **[deterministic]** Confirm the data you need is in the boundary list. If it is not, you need an
   architecture change, not a reach into sim state. Ask; do not reach.
3. **[deterministic]** Drive visuals from the **event stream**, not from polling state diffs. Events
   are ordered and deterministic; your reaction to them does not have to be, and that asymmetry is the
   point of the boundary.
4. **[deterministic]** Convert `Fix32` to `float` **only here**, at the boundary, for interpolation.
   Interpolated positions never flow back into Core.
5. **[model call: leaf generation]** Implement the view work. 3D orthographic, contract angles, depth
   from the z-buffer, ground decals at `y = 0.01`.
6. **[deterministic]** Input becomes a **command**, queued. A click never mutates state inline, and it
   never assumes it will succeed — the sim may reject the build, and the rejection arrives as an event.
7. **[deterministic]** Check readability at the peak density the wave tables actually reach, not at a
   comfortable one. Silhouette first, color second.
8. **[deterministic]** `dotnet build` (0/0), then `godot --headless --quit` after scene-structure
   changes to catch broken wiring without a display.
9. **[deterministic]** Write the "what to look at" list: the specific things a human must eyeball,
   framed as questions they can answer in ten seconds each.
10. **[model call: gap detection]** If a feel question has no defensible answer from the art direction
    — how strong, how fast, how loud — **ask one question** with two concrete options. Do not average
    them.

## Output

A spec in `presentation/specs/[slug]-render.md` or `[slug]-ui.md`, plus the Godot-side code.

```markdown
# [Feature] — Presentation
**Slug:** `[slug]` · **Status:** review

## Contract Used
| Constant | Value | Source |
## Events Consumed
| SimEvent | Visual response |
## Input → Command
| Input | Command queued | Rejection shown as |
## Readability
Checked at density: … · Silhouettes distinct: yes / no
## Verified
`dotnet build` … · `godot --headless --quit` …
## What a Human Must Look At
1. …
```

## Done when

- [ ] Every constant used is cited from `docs/iso-grid.md`, none hardcoded
- [ ] Every visual is driven by an event, not a poll
- [ ] Every input path queues a command and handles its rejection
- [ ] Compile and headless checks are green
- [ ] The human-check list is specific enough to answer quickly
- [ ] Nothing claims to have been seen

## Failure modes

- **Mutating sim state from the view.** The one unforgivable one. Queue a command.
- **A hardcoded 0.866 somewhere.** It is in the contract; cite it.
- **Claiming a visual result.** You cannot see the game. "Compiles; not visually run" is honest and
  complete, and the verification engineer will carry it forward as NOT-VERIFIABLE-BY-AGENT.
- **Polling state to find changes.** You will miss the tick where two things happened, and it will look
  like a rendering bug for a week.
- **Readability checked at wave 3.** Wave 18 is where the game becomes unreadable.
