# Finds the pinned Godot and builds the C# before launching.
# Dot-sourced by run-game.ps1 and run-editor.ps1; not meant to be run directly.
#
# The Windows half of scripts/godot-env.sh. Same decisions, same order, same
# reasons -- keep the two in step.

function Find-Godot {
    # 1. GODOT_BIN wins. The escape hatch that works on any install layout.
    if ($env:GODOT_BIN -and (Test-Path $env:GODOT_BIN)) { return $env:GODOT_BIN }

    # 2. godot-mono on PATH.
    $onPath = Get-Command godot-mono -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    # 3. The usual places a Godot zip gets unpacked to.
    $candidates = @(
        "$env:LOCALAPPDATA\Godot\Godot_v4.6.3-stable_mono_win64.exe",
        "$env:USERPROFILE\godot\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64.exe",
        "C:\Program Files\Godot\Godot_v4.6.3-stable_mono_win64.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }

    Write-Error @"
Could not find a Godot 4.6.3 mono binary.

Set GODOT_BIN to it:
  `$env:GODOT_BIN = "C:\path\to\Godot_v4.6.3-stable_mono_win64.exe"

It must be the MONO build. A standard build ignores every C# script without
saying so, which looks like a broken game rather than a wrong binary.
"@
    exit 1
}

function Initialize-GodotEnv {
    param([string]$Root)

    $script:GODOT = Find-Godot

    # Godot does NOT rebuild on run -- it loads whatever assembly is already in
    # .godot/mono, so an edited script runs as its previous version with no
    # warning. Refuse to launch a stale assembly.
    $log = dotnet build "$Root\godot\Gridfall.Godot.csproj" -v q --nologo 2>&1
    if ($LASTEXITCODE -ne 0) {
        $log | Write-Host
        Write-Error "C# build failed -- refusing to launch a stale assembly."
        exit 1
    }
}

# Engine flags must go BEFORE Godot's `--`, game flags after. Getting it wrong is
# silent: --headless placed after the separator is handed to the game, Godot
# never sees it, and it opens a window anyway.
function Split-GodotArgs {
    param([string[]]$Args)

    $engine = @()
    $passthrough = @()
    $takesValue = @('--quit-after','--resolution','--position','--display-driver',
                    '--audio-driver','--rendering-driver')
    $flags      = @('--headless','--quit','--verbose','--debug','--fullscreen','--maximized')

    for ($i = 0; $i -lt $Args.Count; $i++) {
        if ($flags -contains $Args[$i]) { $engine += $Args[$i] }
        elseif ($takesValue -contains $Args[$i]) { $engine += $Args[$i]; $i++; $engine += $Args[$i] }
        else { $passthrough += $Args[$i] }
    }
    return @{ Engine = $engine; Passthrough = $passthrough }
}
