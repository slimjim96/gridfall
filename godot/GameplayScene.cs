using System;
using Godot;
using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Gridfall.Io;
using Gridfall.View;
using Gridfall.View.Hud;
using Gridfall.View.Placeholders;

namespace Gridfall;

/// <summary>
/// Wires the view layer together. The scene is built here in code rather than in
/// a .tscn: one place to read, and nothing to drift out of sync.
///
/// This node reads simulation state and queues commands. It writes nothing.
/// </summary>
public sealed partial class GameplayScene : Node3D
{
    private const string MapId = "crossroads";

    private SimDriver _driver = null!;
    private WorldRenderer _world = null!;
    private UnitRenderer _units = null!;
    private Hud _hud = null!;
    private Camera3D _camera = null!;

    private ushort _selectedTower;
    private string _selectedTowerName = "";

    // --shot <path> [--shot-after N]: render N frames, capture, quit. This is how
    // the renderer gets verified without a human at the keyboard.
    private string? _shotPath;
    private int _shotAfterFrames = 90;
    private int _framesRendered;

    public override void _Ready()
    {
        ParseCommandLine();

        string root = ContentFiles.FindRepoRoot();
        MapDef map = ContentFiles.LoadMap(root, MapId);
        ContentSet content = ContentFiles.LoadContent(root, MapId);

        _driver = new SimDriver(map, content, seed: 1);
        _selectedTower = content.TowerIndexOf("arrow-tower");
        _selectedTowerName = content.Tower(_selectedTower).Name;

        BuildEnvironment();

        _camera = new Camera3D();
        AddChild(_camera);
        IsoGrid.ConfigureCamera(_camera, map);

        _world = new WorldRenderer();
        AddChild(_world);
        _world.Initialise(map, _driver.Sim.Path);

        _units = new UnitRenderer();
        AddChild(_units);
        _units.Initialise(_driver);

        _hud = new Hud();
        AddChild(_hud);

        if (_shotPath is not null) SeedForScreenshot();
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;

        // In shot mode the sim is frozen after a fixed number of deterministic
        // steps, so the captured frame is the same frame every run. Advancing by
        // wall clock here would make the screenshot depend on how fast the
        // machine happened to be, which defeats using it as a visual baseline.
        if (_shotPath is null) _driver.Advance(dt);
        _world.RebuildIfChanged();

        // Fixed delta in shot mode too: idle bob and hit flash are view-side and
        // wall-clock driven, so a real delta makes two captures of the same
        // simulation state differ by a few pixels. A fixed frame count times a
        // fixed delta makes the whole frame reproducible, which is what a visual
        // baseline needs.
        _units.Render(_shotPath is null ? dt : 1f / 60f);

        foreach (SimEvent e in _driver.FrameEvents)
            if (e.Kind == EventKind.BuildRejected)
                _hud.ShowRefusal((RejectReason)e.A);

        _hud.Refresh(_driver.State, _selectedTowerName, dt);
        UpdateHover();

        if (_shotPath is not null && ++_framesRendered >= _shotAfterFrames) CaptureAndQuit();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true } key)
        {
            switch (key.Keycode)
            {
                case Key.Space: _driver.Enqueue(new StartWaveCommand()); break;
                case Key.Key1: SelectTower("arrow-tower"); break;
                case Key.Key2: SelectTower("cannon"); break;
                case Key.Escape: GetTree().Quit(); break;
            }
            return;
        }

        if (@event is not InputEventMouseButton { Pressed: true } click) return;
        if (!IsoGrid.TryPick(_camera, click.Position, _driver.Map, out GridCell cell)) return;

        // A click becomes a queued command, never a state change. It also does
        // not assume it will succeed -- the refusal arrives as an event.
        if (click.ButtonIndex == MouseButton.Left)
        {
            _driver.Enqueue(new BuildCommand(cell, _selectedTower));
        }
        else if (click.ButtonIndex == MouseButton.Right)
        {
            int index = _driver.Map.Index(cell);
            SimState state = _driver.State;
            for (int k = 0; k < state.TowerCount; k++)
            {
                int slot = state.TowerSlotByOrder(k);
                if (state.TowerCellIndex[slot] != index) continue;
                _driver.Enqueue(new SellCommand(state.TowerId[slot]));
                break;
            }
        }
    }

    private void SelectTower(string id)
    {
        _selectedTower = _driver.Content.TowerIndexOf(id);
        _selectedTowerName = _driver.Content.Tower(_selectedTower).Name;
    }

    private void UpdateHover()
    {
        Vector2 mouse = GetViewport().GetMousePosition();
        if (!IsoGrid.TryPick(_camera, mouse, _driver.Map, out GridCell cell))
        {
            _world.HideHover();
            return;
        }

        int index = _driver.Map.Index(cell);
        bool legal = _driver.Map.Cells[index] == CellKind.Buildable
                     && !_driver.Sim.Path.IsBlocked(index);
        _world.ShowHover(cell, legal);
    }

    private void BuildEnvironment()
    {
        var light = new DirectionalLight3D
        {
            // Upper left, matching the lighting the art direction assumes.
            Rotation = new Vector3(Mathf.DegToRad(-55), Mathf.DegToRad(-40), 0),
            LightEnergy = 1.1f,
        };
        AddChild(light);

        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = Color.FromHtml("11161c"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Color.FromHtml("46566a"),
            AmbientLightEnergy = 0.6f,
            TonemapMode = Godot.Environment.ToneMapper.Aces,
        };
        AddChild(new WorldEnvironment { Environment = environment });
    }

    // ---- screenshot mode --------------------------------------------------

    private void ParseCommandLine()
    {
        string[] args = OS.GetCmdlineUserArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--shot" && i + 1 < args.Length) _shotPath = args[i + 1];
            if (args[i] == "--shot-after" && i + 1 < args.Length && int.TryParse(args[i + 1], out int n))
                _shotAfterFrames = n;
        }
    }

    /// <summary>
    /// Put something worth looking at on the board: a few towers and a running
    /// wave, stepped deterministically rather than by wall clock so the capture
    /// is the same frame every time.
    /// </summary>
    private void SeedForScreenshot()
    {
        ushort arrow = _driver.Content.TowerIndexOf("arrow-tower");
        ushort cannon = _driver.Content.TowerIndexOf("cannon");

        _driver.Enqueue(new BuildCommand(new GridCell(2, 3), arrow));
        _driver.Enqueue(new BuildCommand(new GridCell(6, 5), arrow));
        _driver.Enqueue(new BuildCommand(new GridCell(9, 3), cannon));
        _driver.Enqueue(new StartWaveCommand());

        for (int t = 0; t < 90; t++) _driver.StepOneTick();

        // Printed so it can be diffed against a headless run of the same script:
        // if the renderer is touching simulation state, this is where it shows.
        GD.Print($"shot-state: tick={_driver.TickCount} hash={_driver.Sim.Hash():x16} " +
                 $"gold={_driver.State.Gold} lives={_driver.State.Lives} " +
                 $"creeps={_driver.State.CreepCount} towers={_driver.State.TowerCount}");
    }

    private bool _capturing;

    private async void CaptureAndQuit()
    {
        if (_capturing) return;
        _capturing = true;

        string path = _shotPath!;
        await ToSignal(RenderingServer.Singleton, RenderingServerInstance.SignalName.FramePostDraw);

        Image image = GetViewport().GetTexture().GetImage();
        Error error = image.SavePng(path);
        GD.Print(error == Error.Ok ? $"shot saved: {path}" : $"shot FAILED: {error}");

        GetTree().Quit();
    }
}
