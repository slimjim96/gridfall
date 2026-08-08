#!/usr/bin/env bash
# Fit a unit's sprite strips to the pipeline's anchoring rule, in place.
#
#   ./fit-sprite.sh presentation/units/arrow-tower
#   ./fit-sprite.sh presentation/units/arrow-tower --dry-run
#   ./fit-sprite.sh presentation/units/*
#
# Square frames, subject horizontally centred, base flush to the bottom edge --
# see godot/tools/fit-sprite.gd for why each of those is load-bearing. Prints the
# factor to multiply `frameCells` by; it does not edit unit.json for you.
#
# EDITS IN PLACE and there is no undo. Commit first, or pass --dry-run.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$ROOT/scripts/godot-env.sh"

# Paths are resolved against the repo root, not the Godot project, so tab
# completion from where you actually stand does the right thing.
ARGS=()
for a in "$@"; do
    case "$a" in
        --*) ARGS+=("$a") ;;
        /*)  ARGS+=("$a") ;;
        *)   ARGS+=("$ROOT/$a") ;;
    esac
done

exec "$GODOT" --headless --path "$ROOT/godot" \
     --script res://tools/fit-sprite.gd -- ${ARGS[@]+"${ARGS[@]}"}
