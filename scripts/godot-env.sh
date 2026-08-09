#!/usr/bin/env bash
# Finds the pinned Godot and fails with something useful when it cannot.
# Sourced by run-game.sh and run-editor.sh; not meant to be run directly.
#
# The project is pinned to 4.6.3 MONO (ADR-0005). A non-mono build is worse than
# a wrong version: it loads the project, silently ignores every C# script, and
# shows an empty window that looks like a broken game.
#
# Linux and macOS both use this. Windows uses scripts/godot-env.ps1, which makes
# the same decisions in the same order.

# 1. GODOT_BIN wins, always. This is the escape hatch that makes every platform
#    and every install layout work without editing a script:
#      export GODOT_BIN="/path/to/Godot_v4.6.3-stable_mono"
# 2. godot-mono on PATH.
# 3. The known install location for this platform.
LINUX_PINNED="$HOME/projects/godot-install/Godot_v4.6.3-stable_mono_linux_x86_64/Godot_v4.6.3-stable_mono_linux.x86_64"
MAC_PINNED="/Applications/Godot_mono.app/Contents/MacOS/Godot"
MAC_PINNED_USER="$HOME/Applications/Godot_mono.app/Contents/MacOS/Godot"

if [[ -n "${GODOT_BIN:-}" && -x "${GODOT_BIN}" ]]; then
    GODOT="$GODOT_BIN"
elif command -v godot-mono >/dev/null 2>&1; then
    GODOT="$(command -v godot-mono)"
elif [[ -x "$LINUX_PINNED" ]]; then
    GODOT="$LINUX_PINNED"
elif [[ -x "$MAC_PINNED" ]]; then
    GODOT="$MAC_PINNED"
elif [[ -x "$MAC_PINNED_USER" ]]; then
    GODOT="$MAC_PINNED_USER"
else
    cat >&2 <<'MSG'
Could not find a Godot 4.6.3 mono binary.

Set GODOT_BIN to it -- that works on every platform and every install layout:

  export GODOT_BIN="/path/to/Godot_v4.6.3-stable_mono"     # Linux / macOS
  $env:GODOT_BIN = "C:\path\to\Godot_v4.6.3-stable_mono_win64.exe"   # Windows

Or put `godot-mono` on PATH. Otherwise these are checked:
  Linux  ~/projects/godot-install/Godot_v4.6.3-stable_mono_linux_x86_64/...
  macOS  /Applications/Godot_mono.app/Contents/MacOS/Godot

It must be the MONO build. A standard build ignores every C# script without
saying so, which looks like a broken game rather than a wrong binary.
MSG
    exit 1
fi

# Display checks are X11-specific, so they only apply on Linux. macOS always has
# a window server, and Windows is handled in the PowerShell launcher.
if [[ "$(uname -s)" == "Linux" ]]; then
    if [[ -z "${DISPLAY:-}" && -z "${WAYLAND_DISPLAY:-}" ]] && [[ "$*" != *--headless* ]]; then
        echo "warning: no DISPLAY set -- expect 'X11 Display is not available'." >&2
    elif [[ -n "${DISPLAY:-}" ]]; then
        sock="/tmp/.X11-unix/X$(echo "${DISPLAY#:}" | cut -d. -f1)"
        if [[ ! -e "$sock" ]]; then
            echo "warning: DISPLAY=$DISPLAY is set but $sock does not exist." >&2
            echo "         That session has ended -- reconnect first." >&2
        fi
    fi
fi

export GODOT

# Build the C# before launching. Godot does NOT rebuild on run -- it loads
# whatever assembly is already in .godot/mono, so an edited script runs as its
# previous version with no warning at all. That silently produced a screenshot
# of the old renderer and had me debugging code that was never executed.
if [[ -n "${ROOT:-}" ]]; then
    if ! build_log="$(dotnet build "$ROOT/godot/Gridfall.Godot.csproj" -v q --nologo 2>&1)"; then
        echo "$build_log" >&2
        echo "" >&2
        echo "C# build failed -- refusing to launch a stale assembly." >&2
        exit 1
    fi
fi
