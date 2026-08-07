#!/usr/bin/env bash
# Launch the game.
#
#   ./run-game.sh
#   ./run-game.sh --shot /tmp/x.png --shot-after 40    capture a frame and quit
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$ROOT/scripts/godot-env.sh"
source "$ROOT/scripts/split-args.sh"

split_godot_args "$@"

exec "$GODOT" --path "$ROOT/godot" ${ENGINE_ARGS[@]+"${ENGINE_ARGS[@]}"} \
     -- ${PASSTHROUGH[@]+"${PASSTHROUGH[@]}"}
