# Asset Prompts

One file per asset. Each contains the sprite prompt, the mesh prompt, every animation clip, and the
iteration log. Written by agents, run by the human in Ludo.ai, tweaked in an image editor.

How to write them: [`../docs/ludo-prompt-guide.md`](../docs/ludo-prompt-guide.md).
The workflow: [`../../workflows/cross-cutting/asset-prompt-pass.md`](../../workflows/cross-cutting/asset-prompt-pass.md).

## The style anchor — v1

Copy this block **verbatim** into the top of every prompt. Do not paraphrase it; paraphrasing is how a
set drifts out of consistency.

> **Style anchor — Gridfall**
> Isometric tower defense game asset. Clean geometric forms, low detail, strong readable silhouette.
> Flat matte surfaces with soft ambient occlusion; no glossy highlights, no rim lighting, no text.
> Restrained palette, low saturation for terrain and structures, saturated accent only on the element
> that carries the unit's identity. Single unit centered on a transparent background, no ground plane,
> no shadow baked in, no scenery. Neutral studio lighting from the upper left.

Changing the anchor means regenerating **every** asset in the set. Version it here when that happens
and note the date.

## Index

| Asset | Kind | Sprite | Mesh | Clips | Status |
|---|---|---|---|---|---|
| [tower-frost-spire](tower-frost-spire.md) | Tower | written | written | idle, fire | example — not yet run |

Status values: `written` → `generated` → `tweaked` → `in-game`.

## Open question — output format

Whether Ludo.ai gives this project usable 2D sprite sheets or usable 3D `.glb` is not yet settled, so
every prompt set carries **both** forms.
[ADR-0004](../../engine-systems/decisions/ADR-0004-view-asset-abstraction.md) keeps both viable in code.

When the human has run a couple of sets and knows the answer: delete the unused form from every prompt
file in one pass, update the prompt guide, and supersede ADR-0004. That is a success, not a rework.
