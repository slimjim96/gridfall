#!/usr/bin/env bash
# Sorts arguments into engine flags (before Godot's `--`) and game flags (after).
# Sourced by run-game.sh and run-editor.sh; not meant to be run directly.
#
# Godot silently ignores an engine flag that lands on the game side: pass
# --headless after the separator and it opens a window anyway, with no warning.
# That cost a hung test, so the split is explicit rather than assumed.

split_godot_args() {
    ENGINE_ARGS=()
    PASSTHROUGH=()

    while [[ $# -gt 0 ]]; do
        case "$1" in
            --headless|--quit|--verbose|--debug|--fullscreen|--maximized)
                ENGINE_ARGS+=("$1"); shift ;;
            --quit-after|--resolution|--position|--display-driver|--audio-driver|--rendering-driver)
                ENGINE_ARGS+=("$1" "${2:-}"); shift 2 ;;
            *)
                PASSTHROUGH+=("$1"); shift ;;
        esac
    done
}
