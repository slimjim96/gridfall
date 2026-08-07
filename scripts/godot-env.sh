#!/usr/bin/env bash
# Finds the pinned Godot and fails with something useful when it cannot.
# Sourced by run-game.sh and run-editor.sh; not meant to be run directly.
#
# The project is pinned to 4.6.3 MONO (ADR-0005). `godot` and `godot-4` on this
# machine are 4.7, and a non-mono build is worse than a wrong version: it loads
# the project, silently ignores every C# script, and shows an empty window that
# looks like a broken game.

PINNED="$HOME/projects/godot-install/Godot_v4.6.3-stable_mono_linux_x86_64/Godot_v4.6.3-stable_mono_linux.x86_64"

if command -v godot-mono >/dev/null 2>&1; then
    GODOT="$(command -v godot-mono)"
elif [[ -x "$PINNED" ]]; then
    GODOT="$PINNED"
else
    cat >&2 <<'MSG'
Could not find a Godot 4.6.3 mono binary.

Expected either `godot-mono` on PATH, or:
  ~/projects/godot-install/Godot_v4.6.3-stable_mono_linux_x86_64/Godot_v4.6.3-stable_mono_linux.x86_64

To create the shortcut:
  mkdir -p ~/.local/bin
  ln -sf ~/projects/godot-install/Godot_v4.6.3-stable_mono_linux_x86_64/Godot_v4.6.3-stable_mono_linux.x86_64 \
         ~/.local/bin/godot-mono

Do NOT use `godot` or `godot-4` -- both are 4.7 here, and a non-mono build
ignores every C# script without saying so.
MSG
    exit 1
fi

# A missing display is the most common failure after a wrong binary, and Godot's
# own message for it is buried under a page of harmless ALSA noise.
if [[ -z "${DISPLAY:-}" && -z "${WAYLAND_DISPLAY:-}" ]] && [[ "$*" != *--headless* ]]; then
    echo "warning: no DISPLAY set -- expect 'X11 Display is not available'." >&2
    echo "         On this VM the display belongs to the RDP session; connect first." >&2
elif [[ -n "${DISPLAY:-}" ]] && [[ ! -e "/tmp/.X11-unix/X${DISPLAY#:}" ]] \
     && [[ ! -e "/tmp/.X11-unix/X${DISPLAY%%.*}" ]]; then
    sock="/tmp/.X11-unix/X$(echo "${DISPLAY#:}" | cut -d. -f1)"
    if [[ ! -e "$sock" ]]; then
        echo "warning: DISPLAY=$DISPLAY is set but $sock does not exist." >&2
        echo "         That session has ended -- reconnect over RDP." >&2
    fi
fi

export GODOT
