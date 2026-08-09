# Launch the board editor. The Windows half of run-editor.sh.
#
#   .\run-editor.ps1                 a blank 20x12 map
#   .\run-editor.ps1 crossroads      edit an existing map
#   .\run-editor.ps1 crossroads --shot C:\tmp\x.png --shot-after 30
$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
. "$Root\scripts\godot-env.ps1"

Initialize-GodotEnv -Root $Root

$gameArgs = @()
$rest = $args
if ($rest.Count -gt 0 -and -not $rest[0].StartsWith("--")) {
    $gameArgs = @("--map", $rest[0])
    $rest = $rest[1..($rest.Count - 1)]
}
$split = Split-GodotArgs -Args $rest

& $GODOT --path "$Root\godot" @($split.Engine) `
         --scene "res://Dev/BoardEditor.tscn" -- @($gameArgs) @($split.Passthrough)
exit $LASTEXITCODE
