# Launch the game. The Windows half of run-game.sh.
#
#   .\run-game.ps1
#   .\run-game.ps1 --map gauntlet
#   .\run-game.ps1 --shot C:\tmp\x.png --shot-after 40
$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
. "$Root\scripts\godot-env.ps1"

Initialize-GodotEnv -Root $Root
$split = Split-GodotArgs -Args $args

& $GODOT --path "$Root\godot" @($split.Engine) -- @($split.Passthrough)
exit $LASTEXITCODE
