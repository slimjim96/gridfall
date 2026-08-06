# ADR-0005 — Pin Godot to 4.6.3 Mono

**Status:** accepted
**Date:** 2026-08-06 · **Raised by:** `view-layer-foundation`

## Context

Three Godot installations exist on the dev box, and they do not agree:

| Where | Version | C#? |
|---|---|---|
| `~/.local/bin/godot-mono` → `~/projects/godot-install/...` | 4.6.3 mono | yes |
| `/snap/bin/godot-4` | 4.7 mono | yes |
| `~/Downloads/Godot_v4.7.1-stable_linux.x86_64.zip` | 4.7.1 **non-mono** | **no** |

`godot/Gridfall.Godot.csproj` pins `Godot.NET.Sdk/4.6.3`, and everything built and verified in the
view-layer slice used the 4.6.3 mono binary. The snap is what `godot` resolves to by default on PATH,
which means the default is the one that does *not* match the pin.

A non-mono build cannot run Gridfall at all — it will load the project and silently ignore every C#
script, which looks like a broken game rather than a wrong binary.

The sibling project (Scrap Escape, `~/projects/2d-puzzler`) is also on Godot 4.6.3.

## Options

### A. Pin 4.6.3 mono

Keep the SDK, GodotSharp, and the editor binary all at 4.6.3. Verified working: builds 0/0 against real
GodotSharp, renders, and produces a byte-reproducible baseline capture. One toolchain across both Godot
projects on this machine.

Cost: 4.7's improvements are unavailable, and the snap on PATH stays a trap.

### B. Move to 4.7

Bump `Godot.NET.Sdk` and GodotSharp to 4.7.x, matching the snap that PATH already resolves to.

Cost: a 4.7 editor opening a 4.6 project may re-import and rewrite `config/features`; the whole visual
baseline needs re-capturing to confirm nothing shifted; and it diverges from Scrap Escape. None of this
is hard, but none of it buys anything this project currently needs.

## Decision

Chose **A** — pin 4.6.3 mono.

Deciding factor: **nothing needs 4.7.** The renderer is flat quads and primitives on a software
rasterizer at 41 fps; there is no 4.7 feature this project is waiting on. Upgrading now spends
verification effort — rebuild, re-capture, re-diff the baseline — to buy nothing, and it would leave
the two Godot projects on this box on different toolchains.

## Consequences

### Good
- The SDK pin, the editor binary, and the verified baseline all agree.
- One Godot version across Gridfall and Scrap Escape.
- The captured baseline stays valid as a visual regression reference.

### Bad
- `godot` and `godot-4` on PATH resolve to the **wrong** version. Every doc must name `godot-mono`
  explicitly, and someone will still type `godot-4` eventually.
- 4.7 fixes and features are unavailable until this is revisited.
- The pin is enforced only by the csproj and by documentation — there is no check that the *binary*
  matches. A 4.7 editor opening the project would not be caught automatically.

### Forecloses
- Nothing permanently. Revisiting means bumping two version numbers, rebuilding, and re-capturing
  `presentation/docs/board-baseline.png` — then superseding this ADR.

## How to run it

```bash
godot-mono --path ~/projects/claude/gridfall/godot                    # play it
godot-mono --path godot -- --shot /tmp/shot.png --shot-after 40       # capture a frame
godot-mono --path godot --headless --quit                             # wiring check
```

**Do not use `godot`, `godot-4`, or any non-mono build.** The first two are 4.7; the last cannot run C#
at all.
