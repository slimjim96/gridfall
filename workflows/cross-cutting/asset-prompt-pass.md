# WF-X4 · Asset Prompt Pass

**Workspace:** `presentation` · **Role:** asset-author
**Injection points:** leaf generation (3), gap detection (4)

## Fires when

A new station, visitor, terrain type, or effect exists as a **placeholder** and needs the prompt set that
will eventually replace it. Also fires when the style anchor changes and a set must be regenerated.

Write the prompts **while the asset is fresh**. The design intent — "cold, still, unsettling" — is in
your head the week the thing is built and gone a month later. That sentence is the most valuable line
in the prompt file and it cannot be reconstructed from the code.

## Load

- `presentation/docs/ludo-prompt-guide.md`
- `presentation/docs/placeholder-standard.md` — the placeholder's silhouette is the spec the generated
  art must honor
- `presentation/prompts/README.md` — the current style anchor, verbatim
- `presentation/prompts/_template.md`
- The two nearest existing assets' prompt files, for consistency
- The design spec's identity sentence for this thing

## Never load

`engine-systems/**` · `content-data/**` · `production/**` · the full prompt catalogue (two neighbors is
the right amount of context; the whole set anchors you into repetition)

## Steps

1. **[deterministic]** Copy `_template.md` to `presentation/prompts/[kind]-[id].md`.
2. **[model call: leaf generation]** Write the **Identity** section first: what it is, what it does,
   and the one feeling it should give. Everything below serves this. If you cannot write the feeling in
   one clause, you do not yet know what you are asking for — **gap detection: ask one question.**
3. **[deterministic]** Record the silhouette the placeholder established and the two assets it must not
   be confusable with. This is the constraint the generated art most often breaks.
4. **[deterministic]** Paste the style anchor **verbatim** from `prompts/README.md`. Do not paraphrase,
   do not "improve" it. Paraphrasing is the single most common cause of a set drifting apart.
5. **[model call: leaf generation]** Write the **sprite form**. State the projection numerically —
   yaw 45°, pitch 30°, 2:1 dimetric. "Isometric" alone gets you three different projections.
6. **[model call: leaf generation]** Write the **mesh form**, with the same subject and form sentences
   as the sprite so the two cannot drift. Origin at base center, Y-up, 1 unit = 1 cell, albedo only.
7. **[deterministic]** Write **asset-specific negatives**. The generic ones are in the template; what
   matters is the failure this particular subject invites. Ice gets radiating crystals. Anything
   described as "fast" gets motion blur. Anything described as "heavy" gets a plinth. Name it.
8. **[model call: leaf generation]** Write each **animation clip** from the standard set (`idle`,
   `move`, `fire`, `hit`, `death` — only the ones this asset needs). Timing in whole frames at 30 fps
   so clips land on tick boundaries. `no root motion` on every mesh clip, always.
9. **[deterministic]** Add **Notes for the human**: the known failure mode, what to check before
   accepting, and any timing that must not be stretched.
10. **[deterministic]** Add the row to `presentation/prompts/README.md` with status `written`.

## Output

`presentation/prompts/[kind]-[id].md` — sprite form, mesh form, every clip, iteration log.

## Done when

- [ ] Identity names a feeling, not just a function
- [ ] Silhouette constraint recorded, with the two confusable neighbors named
- [ ] Style anchor pasted verbatim in every prompt block
- [ ] Both sprite and mesh forms present, with matching subject/form sentences
- [ ] Projection stated numerically in the sprite form
- [ ] Every clip has whole-frame timing at 30 fps and `no root motion`
- [ ] At least one asset-specific negative
- [ ] Index row added with status `written`
- [ ] No claim that any of it was generated or looks good — that is the human's part

## Handoff

To the human, not to a workspace. They run the prompts in Ludo.ai, tweak in an image editor, and report
back. Update the iteration log and the status when they do.

## Failure modes

- **Paraphrasing the anchor.** The set stops looking like a set, and it is invisible until you see two
  assets side by side.
- **Writing prompts before the placeholder exists.** The placeholder's silhouette is the spec. Without
  it you are guessing at the shape and the generated art has nothing to be checked against.
- **Only writing one form.** Until the format question is closed
  ([ADR-0004](../../engine-systems/decisions/ADR-0004-view-asset-abstraction.md)), both. Skipping one
  saves ten minutes and risks the whole set.
- **Generic negatives only.** "No text, no watermark" catches nothing. The useful negative is the one
  specific to this subject.
- **Clip timing in milliseconds that isn't a whole frame count.** 250 ms is 7.5 frames, and the asset
  will land between ticks forever.
- **Claiming the art is good.** You cannot see it. Say what to check instead.
