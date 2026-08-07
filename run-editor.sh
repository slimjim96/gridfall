#!/usr/bin/env bash
# Launch the board editor.
#
#   ./run-editor.sh                 a blank 20x12 map
#   ./run-editor.sh crossroads      edit an existing map
#   ./run-editor.sh crossroads --shot /tmp/x.png --shot-after 30
#
# Exists because the raw command is long enough to wrap when pasted, and a
# wrapped line makes bash run the second half as its own command -- which
# reports "Missing scene path" and "No such file or directory" and reads like
# two unrelated failures.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$ROOT/scripts/godot-env.sh"
source "$ROOT/scripts/split-args.sh"

GAME_ARGS=()
if [[ $# -gt 0 && $1 != --* ]]; then
    GAME_ARGS=(--map "$1")
    shift
fi

# Engine flags must go BEFORE Godot's `--`, game flags after. Getting it wrong
# is silent: --headless placed after the separator is handed to the game, Godot
# never sees it, and it opens a window anyway.
split_godot_args "$@"

exec "$GODOT" --path "$ROOT/godot" ${ENGINE_ARGS[@]+"${ENGINE_ARGS[@]}"} \
     --scene res://Dev/BoardEditor.tscn \
     -- ${GAME_ARGS[@]+"${GAME_ARGS[@]}"} ${PASSTHROUGH[@]+"${PASSTHROUGH[@]}"}
