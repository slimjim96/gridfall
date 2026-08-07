using System.Collections.Generic;
using Godot;
using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Path;
using Gridfall.Io;
using Gridfall.View;
using Gridfall.View.Placeholders;

namespace Gridfall.Dev;

/// <summary>
/// The board editor: paint a map, see it validated as you paint, press F5 to
/// play it. Spec: tooling/docs/board-editor-spec.md.
///
/// Dev-only. Lives under godot/Dev/, which is excluded from release exports.
///
/// It reuses the game's renderer, the game's picking, and above all the game's
/// validator -- it contributes no rules of its own, so it cannot disagree with
/// the game about what a legal map is. An editor that says yes where the game
/// says no is worse than no editor.
/// </summary>
public sealed partial class BoardEditor : Node3D
{
    private const int UndoDepth = 50;

    private MapDraft _draft = null!;
    private PathSystem _path = null!;
    private WorldRenderer _world = null!;
    private RouteOverlay _routes = null!;
    private EditorHud _hud = null!;
    private Camera3D _camera = null!;

    private CellKind _brush = CellKind.Buildable;
    private int _brushSize = 1;
    private bool _painting;
    private bool _erasing;

    private readonly List<MapDraft> _undo = new();
    private readonly List<MapDraft> _redo = new();
    private List<MapFinding> _findings = new();

    private string _repoRoot = "";
    private string _mapId = "untitled";
    private bool _dirty;

    // Same capture path as the gameplay scene, so the editor can be verified
    // without a human at the keyboard.
    private string? _shotPath;
    private int _shotAfterFrames = 30;
    private int _framesRendered;
    private bool _capturing;

    public override void _Ready()
    {
        _repoRoot = ContentFiles.FindRepoRoot();

        string? requested = ParseMapArg();
        _draft = LoadOrBlank(requested);
        _mapId = _draft.Id;

        _camera = new Camera3D();
        AddChild(_camera);

        _world = new WorldRenderer();
        AddChild(_world);

        _routes = new RouteOverlay();
        AddChild(_routes);

        _hud = new EditorHud();
        AddChild(_hud);

        BuildEnvironment();
        RebuildEverything();

        ParseShotArgs();
        if (_shotPath is not null) SeedForScreenshot();
    }

    public override void _Process(double delta)
    {
        if (_shotPath is not null && ++_framesRendered >= _shotAfterFrames) CaptureAndQuit();
    }

    private void ParseShotArgs()
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
    /// Paint something worth looking at, deterministically: a wall that leaves a
    /// single gap, so the capture exercises the validator, the error highlight,
    /// and the route overlay at once.
    /// </summary>
    private void SeedForScreenshot()
    {
        int mid = _draft.Height / 2;
        for (int y = 1; y < _draft.Height - 1; y++)
            if (y != mid) _draft.Paint(new GridCell(10, y), CellKind.Blocked);

        _brush = CellKind.Blocked;
        _dirty = true;
        RebuildEverything();
        RunMazeEstimate();
        _hud.SetStatus("seeded for capture: a wall with one gap");
    }

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

    // ---- editing ----------------------------------------------------------

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false } key) HandleKey(key);
        else if (@event is InputEventMouseButton mb) HandleMouseButton(mb);
        else if (@event is InputEventMouseMotion && (_painting || _erasing)) PaintUnderCursor();
    }

    private void HandleKey(InputEventKey key)
    {
        bool ctrl = key.IsCommandOrControlPressed();

        if (ctrl)
        {
            switch (key.Keycode)
            {
                case Key.S: Save(); return;
                case Key.N: NewMap(); return;
                case Key.Z when key.ShiftPressed: Redo(); return;
                case Key.Z: Undo(); return;
                case Key.Y: Redo(); return;
            }
            return;
        }

        switch (key.Keycode)
        {
            case Key.Key1: _brush = CellKind.Buildable; break;
            case Key.Key2: _brush = CellKind.PathOnly; break;
            case Key.Key3: _brush = CellKind.Blocked; break;
            case Key.Key4: _brush = CellKind.Spawn; break;
            case Key.Key5: _brush = CellKind.Goal; break;
            case Key.Bracketleft: _brushSize = 1; break;
            case Key.Bracketright: _brushSize = 3; break;
            case Key.F4: CycleTheme(); break;
            case Key.F1: _hud.ToggleHelp(); break;
            case Key.F2: _routes.Toggle(); break;
            case Key.F3: _hud.TogglePanel(); break;
            case Key.F5: Playtest(); break;
            case Key.F6: RunMazeEstimate(); break;
            case Key.Escape: GetTree().Quit(); break;
        }
        _hud.SetBrush(_brush, _brushSize, _draft.Theme);
    }

    private void HandleMouseButton(InputEventMouseButton mb)
    {
        if (mb.ButtonIndex == MouseButton.WheelUp) { Zoom(-1.5f); return; }
        if (mb.ButtonIndex == MouseButton.WheelDown) { Zoom(1.5f); return; }

        bool left = mb.ButtonIndex == MouseButton.Left;
        bool right = mb.ButtonIndex == MouseButton.Right;
        if (!left && !right) return;

        if (mb.Pressed)
        {
            PushUndo();                 // one undo entry per stroke, not per cell
            _painting = left;
            _erasing = right;
            PaintUnderCursor();
        }
        else
        {
            _painting = false;
            _erasing = false;
            // Validation runs on stroke END, not per pixel: one BFS per stroke.
            Revalidate();
        }
    }

    private void PaintUnderCursor()
    {
        if (!IsoGrid.TryPick(_camera, GetViewport().GetMousePosition(), _draft.Width, _draft.Height,
                out GridCell centre)) return;

        CellKind kind = _erasing ? CellKind.Buildable : _brush;

        // A 3x3 goal brush would place nine goals and then eight of them would
        // immediately move; only single-cell for the unique glyphs.
        int radius = (kind is CellKind.Goal or CellKind.Spawn) ? 0 : _brushSize / 2;

        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            var cell = new GridCell(centre.X + dx, centre.Y + dy);
            if (_draft.InBounds(cell)) _draft.Paint(cell, kind);
        }

        _dirty = true;
        RebuildGeometry();
    }

    // ---- validation -------------------------------------------------------

    private void Revalidate()
    {
        _findings = MapValidator.Validate(_draft);
        _hud.ShowFindings(_findings, _dirty, _mapId);
        _world.SetErrorCells(ErrorCells());
    }

    private List<GridCell> ErrorCells()
    {
        var cells = new List<GridCell>();
        foreach (MapFinding f in _findings)
            if (f.Severity == MapSeverity.Error && f.Cell.IsValid) cells.Add(f.Cell);
        return cells;
    }

    /// <summary>
    /// Step to the next terrain palette and redraw.
    ///
    /// Editor spec v1 listed theming as out of scope on the grounds that "the
    /// grid is flat and the art is procedural". Themes changed the second half of
    /// that, so the exclusion is lifted -- but only this far: picking a palette is
    /// not decoration, it is choosing which of the shipped ground ramps the map
    /// declares. Terrain height and per-cell decoration are still out.
    ///
    /// Marks the draft dirty, because the theme is saved in the map file and a
    /// change you cannot tell you have made is worse than no feature.
    /// </summary>
    private void CycleTheme()
    {
        var ids = new List<string>(TerrainTheme.Ids);
        int next = (ids.IndexOf(_draft.Theme) + 1) % ids.Count;
        _draft.Theme = ids[next];

        _dirty = true;
        RebuildEverything();
        _hud.SetBrush(_brush, _brushSize, _draft.Theme);
        _hud.SetStatus($"theme: {_draft.Theme}");
    }

    private void RunMazeEstimate()
    {
        if (MapValidator.HasErrors(_findings))
        {
            _hud.SetEstimate("maze estimate: fix the errors first");
            return;
        }

        MapDef map = _draft.ToMapDef();
        int unmazed = new PathSystem(map).RouteLength(map.Spawns[0]);
        int mazed = MapValidator.EstimateMaxMazedPath(map);

        // A lower bound, and it says so. Over the target proves failure; under it
        // proves nothing.
        string verdict = unmazed <= 0 ? "?" : $"{mazed / (float)unmazed:F1}x";
        _hud.SetEstimate($"maze estimate: {verdict} (greedy lower bound, target <= {MapTargets.MaxMazeMultiplier}x)");
    }

    // ---- files ------------------------------------------------------------

    private MapDraft LoadOrBlank(string? mapId)
    {
        if (mapId is null) return MapDraft.Blank(20, 12);

        try
        {
            return MapDraft.From(ContentFiles.LoadMap(_repoRoot, mapId));
        }
        catch (ContentException ex)
        {
            GD.PrintErr($"could not load '{mapId}': {ex.Message}");
            return MapDraft.Blank(20, 12);
        }
    }

    private void Save()
    {
        Revalidate();
        if (MapValidator.HasErrors(_findings))
        {
            // The game's verdict, refusing the write. The editor adds no opinion.
            _hud.SetStatus("SAVE REFUSED -- fix the errors", error: true);
            return;
        }

        string path = ContentFiles.MapPath(_repoRoot, _draft.Id);
        System.IO.File.WriteAllText(path, _draft.ToJson());
        _dirty = false;
        _mapId = _draft.Id;
        _hud.SetStatus($"saved {_draft.Id}.json");
        _hud.ShowFindings(_findings, _dirty, _mapId);
    }

    private void NewMap()
    {
        PushUndo();
        _draft = MapDraft.Blank(20, 12);
        _mapId = _draft.Id;
        _dirty = true;
        RebuildEverything();
    }

    // ---- playtest ---------------------------------------------------------

    private void Playtest()
    {
        Revalidate();
        if (MapValidator.HasErrors(_findings))
        {
            _hud.SetStatus("cannot playtest a map with errors", error: true);
            return;
        }

        // The unsaved draft is what gets played -- that is the entire point.
        GameplayScene.PlaytestDraft = _draft.ToMapDef();
        GetTree().ChangeSceneToFile("res://Main.tscn");
    }

    // ---- undo -------------------------------------------------------------

    private void PushUndo()
    {
        _undo.Add(_draft.Clone());
        if (_undo.Count > UndoDepth) _undo.RemoveAt(0);
        _redo.Clear();
    }

    private void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Add(_draft.Clone());
        _draft = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _dirty = true;
        RebuildEverything();
    }

    private void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Add(_draft.Clone());
        _draft = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _dirty = true;
        RebuildEverything();
    }

    // ---- rendering --------------------------------------------------------

    private void RebuildEverything()
    {
        MapDef map = _draft.ToMapDef();
        _path = new PathSystem(map);

        IsoGrid.ConfigureCamera(_camera, map);
        _world.Initialise(map, _path);
        _routes.Initialise(map, _path);
        // Here rather than only in HandleKey: the label was initialised with a
        // literal and so read "slate" on a mountain map until you pressed a key.
        _hud.SetBrush(_brush, _brushSize, _draft.Theme);
        Revalidate();
    }

    /// <summary>
    /// Geometry changed under the brush. A draft mid-stroke is often illegal --
    /// no goal, a stranded spawn -- so the field is rebuilt on a best-effort
    /// basis and the panel reports what is wrong rather than the editor refusing
    /// to draw.
    /// </summary>
    private void RebuildGeometry()
    {
        MapDef map = _draft.ToMapDef();
        if (!map.Goal.IsValid) { _world.InitialiseGeometryOnly(map); return; }

        _path = new PathSystem(map);
        _world.Initialise(map, _path);
        _routes.Initialise(map, _path);
    }

    private void Zoom(float delta)
        => _camera.Size = Mathf.Clamp(_camera.Size + delta, IsoGrid.MinOrthoSize, IsoGrid.MaxOrthoSize);

    private void BuildEnvironment()
    {
        AddChild(new DirectionalLight3D
        {
            Rotation = new Vector3(Mathf.DegToRad(-55), Mathf.DegToRad(-40), 0),
            LightEnergy = 1.1f,
        });

        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = Color.FromHtml("0d1117"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Color.FromHtml("46566a"),
                AmbientLightEnergy = 0.6f,
                TonemapMode = Godot.Environment.ToneMapper.Aces,
            },
        });
    }

    private static string? ParseMapArg()
    {
        string[] args = OS.GetCmdlineUserArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--map") return args[i + 1];
        return null;
    }
}
