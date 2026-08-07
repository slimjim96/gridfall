using System;
using System.Collections.Generic;
using Godot;
using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Gridfall.Io;
using Gridfall.View;
using Gridfall.View.Hud;
using Gridfall.View.Units;

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

    /// <summary>
    /// Set by the board editor before switching scenes: play THIS map, unsaved.
    /// Playtesting a map you have not written to disk is the entire point of F5.
    /// </summary>
    public static MapDef? PlaytestDraft;

    private bool _fromEditor;

    private SimDriver _driver = null!;
    private WorldRenderer _world = null!;
    private UnitRenderer _units = null!;
    private RouteOverlay _routes = null!;
    private Hud _hud = null!;
    private Camera3D _camera = null!;
    private CameraRig _rig = null!;

    private ushort _selectedTower;
    private string _selectedTowerName = "";

    // --shot <path> [--shot-after N]: render N frames, capture, quit. This is how
    // the renderer gets verified without a human at the keyboard.
    private string? _shotPath;
    private int _shotAfterFrames = 90;
    private int _framesRendered;

    // --shot-seed <name>: which board state to set up before capturing. Each
    // slice that makes a visual claim gets its own seed, so verifying a new one
    // never perturbs an already-committed baseline.
    private string _shotSeed = "upgrades";

    /// <summary>--theme <id>: draw this map in another palette, for theme captures.</summary>
    private string? _themeOverride;

    /// <summary>
    /// --units <dir>: read final unit art from somewhere other than
    /// presentation/units. Verification only -- see UnitAssets.Scan.
    /// </summary>
    private string? _unitsOverride;

    /// <summary>Cell the shot-mode cursor rests on, when the seed cares.</summary>
    private GridCell? _shotHoverCell;

    public override void _Ready()
    {
        ParseCommandLine();

        string root = ContentFiles.FindRepoRoot();

        // The game reads the same tile folders the editor does. If it did not,
        // a board would look one way while you painted it and another way when
        // you played it -- exactly the editor/game divergence the tooling rules
        // exist to prevent.
        TileLibrary.Scan(root);
        UnitAssets.Scan(root, _unitsOverride);

        MapDef map = PlaytestDraft ?? ContentFiles.LoadMap(root, MapId);
        if (_themeOverride is not null)
        {
            // Round-trip through the draft rather than adding a setter: MapDef is
            // immutable on purpose, and the editor's own serialiser is the one
            // definition of how a map is rebuilt.
            MapDraft draft = MapDraft.From(map);
            draft.Theme = _themeOverride;
            map = draft.ToMapDef();
        }
        _fromEditor = PlaytestDraft is not null;
        PlaytestDraft = null;
        ContentSet content = ContentFiles.LoadContent(root, MapId);

        _driver = new SimDriver(map, content, seed: 1);
        _selectedTower = content.TowerIndexOf("arrow-tower");
        _selectedTowerName = content.Tower(_selectedTower).Name;

        BuildEnvironment();

        _camera = new Camera3D();
        AddChild(_camera);

        _rig = new CameraRig();
        AddChild(_rig);
        _rig.Initialise(_camera, map);
        // Shot mode must not move the camera: every committed baseline depends
        // on the board being framed identically each run.
        _rig.Locked = _shotPath is not null;

        var backdrop = new Backdrop();
        AddChild(backdrop);
        backdrop.Initialise(map);

        _world = new WorldRenderer();
        AddChild(_world);
        _world.Initialise(map, _driver.Sim.Path);

        _routes = new RouteOverlay();
        AddChild(_routes);
        _routes.Initialise(map, _driver.Sim.Path);

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
        _routes.RebuildLiveIfChanged();

        // Fixed delta in shot mode too: idle bob and hit flash are view-side and
        // wall-clock driven, so a real delta makes two captures of the same
        // simulation state differ by a few pixels. A fixed frame count times a
        // fixed delta makes the whole frame reproducible, which is what a visual
        // baseline needs.
        _units.Render(_shotPath is null ? dt : 1f / 60f);

        foreach (SimEvent e in _driver.FrameEvents)
            if (e.Kind is EventKind.BuildRejected or EventKind.RepairRejected)
                _hud.ShowRefusal((RejectReason)e.A);

        _rig.Update(dt);
        _hud.Refresh(_driver.State, _selectedTowerName, dt);
        UpdateHover();

        if (_shotPath is not null && ++_framesRendered >= _shotAfterFrames) CaptureAndQuit();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // The rig first: it owns the wheel, middle-drag and the pan keys, and
        // returns false for everything it does not claim.
        if (_rig.HandleInput(@event)) return;

        if (@event is InputEventKey { Pressed: true } key)
        {
            switch (key.Keycode)
            {
                case Key.Space: _driver.Enqueue(new StartWaveCommand()); break;
                case Key.Key1: SelectTower("arrow-tower"); break;
                case Key.Key2: SelectTower("cannon"); break;
                case Key.R: _routes.Toggle(); break;
                case Key.Escape:
                    // Back to the editor with the draft intact, or out of the game.
                    if (_fromEditor) GetTree().ChangeSceneToFile("res://Dev/BoardEditor.tscn");
                    else GetTree().Quit();
                    break;
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
            if (TowerSlotAt(cell) is { } slot)
                _driver.Enqueue(new SellCommand(_driver.State.TowerId(slot)));
        }
        else if (click.ButtonIndex == MouseButton.Middle)
        {
            if (TowerSlotAt(cell) is { } slot)
                _driver.Enqueue(new RepairCommand(_driver.State.TowerId(slot)));
        }
    }

    /// <summary>
    /// The repair offer for the tower under the cursor, or null if there is
    /// nothing to offer.
    ///
    /// Calls TowerDef.RepairCostFor rather than reimplementing the curve: a
    /// second copy of the cost formula in the view is a divergence waiting to
    /// happen, and this one would be the copy the player reads.
    /// </summary>
    private string? RepairPromptFor(GridCell cell)
    {
        if (TowerSlotAt(cell) is not { } slot) return null;

        SimStateView state = _driver.State;
        TowerDef def = _driver.Content.Tower(state.TowerDefIndex(slot));
        int level = state.TowerLevel(slot);
        int hp = state.TowerHp(slot);
        int missing = def.Hp - hp;

        // Selling was the one command in the game whose price was never shown
        // before the click -- and now that the price MOVES with damage, hiding it
        // means the player cannot tell a repair from a write-off (pillar 2).
        int sell = def.SalvageValueAt(level, hp);
        string sellPart = $"sell {sell} (right click)";

        if (missing <= 0) return $"{def.Name}  --  {sellPart}";

        int percent = 100 * hp / def.Hp;

        // Naming the rule where the player meets it, rather than only in a
        // refusal after they have already clicked.
        string repairPart = state.WaveActive
            ? "repair between waves"
            : $"repair {def.RepairCostFor(level, missing)} gold (middle click)";

        return $"{def.Name} at {percent}%  --  {repairPart}  ·  {sellPart}";
    }

    /// <summary>The slot of the tower standing on a cell, or null.</summary>
    private int? TowerSlotAt(GridCell cell)
    {
        int index = _driver.Map.Index(cell);
        SimStateView state = _driver.State;
        for (int k = 0; k < state.TowerCount; k++)
        {
            int slot = state.TowerSlotByOrder(k);
            if (state.TowerCellIndex(slot) == index) return slot;
        }
        return null;
    }

    private void SelectTower(string id)
    {
        _selectedTower = _driver.Content.TowerIndexOf(id);
        _selectedTowerName = _driver.Content.Tower(_selectedTower).Name;
    }

    private void UpdateHover()
    {
        // Shot mode has no mouse, so hover a fixed cell -- one squarely on the
        // lane, where a build forces a visible detour. Without this the capture
        // shows the live route only and the preview goes unverified.
        if (_shotPath is not null)
        {
            // A seed that makes a claim about a hovered tower sets the cell; the
            // rest hover a cell squarely on the lane, where a build forces a
            // visible detour.
            GridCell hovered = _shotHoverCell ?? new GridCell(10, 4);
            _world.ShowHover(hovered, true);
            _hud.ShowRepairPrompt(RepairPromptFor(hovered));
            if (_shotHoverCell is null) _routes.ShowPreviewFor(hovered);
            return;
        }

        Vector2 mouse = GetViewport().GetMousePosition();
        if (!IsoGrid.TryPick(_camera, mouse, _driver.Map, out GridCell cell))
        {
            _world.HideHover();
            _routes.ClearPreview();
            _hud.ShowRepairPrompt(null);
            return;
        }

        int index = _driver.Map.Index(cell);
        bool buildable = _driver.Map.Cells[index] == CellKind.Buildable
                         && !_driver.Sim.Path.IsBlocked(index);
        _world.ShowHover(cell, buildable);
        _hud.ShowRepairPrompt(RepairPromptFor(cell));

        // Only preview where a build is actually possible: showing a hypothetical
        // route for a cell you cannot build on answers a question nobody asked.
        if (buildable) _routes.ShowPreviewFor(cell);
        else _routes.ClearPreview();
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
            if (args[i] == "--shot-seed" && i + 1 < args.Length) _shotSeed = args[i + 1];
            if (args[i] == "--shot-after" && i + 1 < args.Length && int.TryParse(args[i + 1], out int n))
                _shotAfterFrames = n;
            if (args[i] == "--theme" && i + 1 < args.Length) _themeOverride = args[i + 1];
            if (args[i] == "--units" && i + 1 < args.Length) _unitsOverride = args[i + 1];
        }
    }

    /// <summary>
    /// Put something worth looking at on the board: a few towers and a running
    /// wave, stepped deterministically rather than by wall clock so the capture
    /// is the same frame every time.
    /// </summary>
    private void SeedForScreenshot()
    {
        if (_shotSeed == "sappers") { SeedSappers(); return; }
        if (_shotSeed == "repair") { SeedRepair(); return; }
        if (_shotSeed == "formats") { SeedFormats(); return; }

        ushort arrow = _driver.Content.TowerIndexOf("arrow-tower");

        // Budgeted deliberately. Starting gold is 200 and a level-2 upgrade costs
        // 110, so the first attempt at this seed built three towers, had 18 gold
        // left, and the upgrades were correctly refused -- producing a capture
        // that showed no level cue at all and "verified" nothing.
        _driver.Enqueue(new BuildCommand(new GridCell(2, 3), arrow));   // 200 -> 150
        _driver.StepOneTick();

        int upgraded = _driver.State.TowerId(_driver.State.TowerSlotByOrder(0));
        _driver.Enqueue(new UpgradeCommand(upgraded));                   // 150 -> 40
        _driver.StepOneTick();   // apply it before reading gold: a command queued
                                 // is not a command applied, and checking too
                                 // early saw 150 and skipped the wait entirely.
        _driver.Enqueue(new StartWaveCommand());

        // Wait for affordability rather than guessing a tick count -- the first
        // attempt guessed 55 and missed the 50-gold build by two.
        int waited = 0;
        while (_driver.State.Gold < 50 && waited < 240) { _driver.StepOneTick(); waited++; }

        // A plain neighbour, so the level cue has something to be compared against.
        // Always leave ticks AFTER the enqueue: the first version spent its whole
        // budget waiting, then enqueued a command that no tick ever applied.
        _driver.Enqueue(new BuildCommand(new GridCell(6, 5), arrow));
        for (int t = 0; t < 30; t++) _driver.StepOneTick();

        var levels = new System.Text.StringBuilder();
        for (int k = 0; k < _driver.State.TowerCount; k++)
        {
            int slot = _driver.State.TowerSlotByOrder(k);
            levels.Append($" t{_driver.State.TowerId(slot)}=L{_driver.State.TowerLevel(slot)}");
        }

        GD.Print($"shot-state: tick={_driver.TickCount} hash={_driver.Sim.Hash():x16} " +
                 $"gold={_driver.State.Gold} lives={_driver.State.Lives} " +
                 $"creeps={_driver.State.CreepCount} towers={_driver.State.TowerCount}" +
                 $" levels:{levels}");
    }

    /// <summary>
    /// Both asset formats on the board at once, with creeps walking behind them.
    ///
    /// `arrow-tower` resolves to a SpriteUnitView and `cannon` to a MeshUnitView
    /// (see presentation/units/), so one frame checks both halves of ADR-0004.
    ///
    /// The claim being checked is **occlusion**, which is the property the whole
    /// format question turns on. Towers go on row 5 and creeps walk row 4:
    /// the camera sits at +X+Z, so a larger grid `x + y` is nearer, which puts
    /// row 5 in FRONT of row 4. If either view fails to write depth, its creeps
    /// show through it and the frame says so immediately.
    /// </summary>
    private void SeedFormats()
    {
        ushort arrow = _driver.Content.TowerIndexOf("arrow-tower");
        ushort cannon = _driver.Content.TowerIndexOf("cannon");

        _driver.Enqueue(new BuildCommand(new GridCell(9, 5), arrow));     // 300 -> 250
        _driver.StepOneTick();
        _driver.Enqueue(new BuildCommand(new GridCell(10, 5), cannon));   // 250 -> 160
        _driver.StepOneTick();

        _driver.Enqueue(new StartWaveCommand());

        // Freeze on the board STATE, not on a guessed tick count. A frame taken
        // while the creeps are still at the spawn would show two towers occluding
        // nothing and would have verified precisely nothing.
        int waited = 0;
        while (waited < 4000 && !CreepIsBehindTheTowers()) { _driver.StepOneTick(); waited++; }

        GD.Print($"shot-state: tick={_driver.TickCount} hash={_driver.Sim.Hash():x16} " +
                 $"gold={_driver.State.Gold} creeps={_driver.State.CreepCount} " +
                 $"towers={_driver.State.TowerCount} waited={waited} " +
                 $"occluded={CreepIsBehindTheTowers()}");
    }

    /// <summary>A creep on the lane directly behind the two seeded towers.</summary>
    private bool CreepIsBehindTheTowers()
    {
        for (int k = 0; k < _driver.State.CreepCount; k++)
        {
            int slot = _driver.State.CreepSlotByOrder(k);
            int index = _driver.State.CreepCellIndex(slot);
            int x = index % _driver.Map.Width;
            int y = index / _driver.Map.Width;
            if (y == 4 && x is >= 9 and <= 11) return true;
        }
        return false;
    }

    /// <summary>
    /// Runs to the first wave that contains sappers and freezes with at least
    /// one of them on the board beside a damaged tower.
    ///
    /// Sappers first appear at wave 5, so unlike the upgrade seed this one has
    /// to actually play the game to get there. Everything is stepped through
    /// StepOneTick, so the frame stays byte-reproducible.
    /// </summary>
    private void SeedSappers()
    {
        ushort arrow = _driver.Content.TowerIndexOf("arrow-tower");
        ushort sapper = _driver.Content.EnemyIndexOf("sapper");

        // Build across the board as gold allows. One cursor over the list, never
        // than parked in a corner. One cursor over the list, never revisited: the
        // first version re-offered cells it had already built on, so every build
        // after the sixth was refused and the run was lost before wave 5.
        int cost = _driver.Content.Tower(arrow).Cost;

        // Cells the sim has refused. Without this the seed livelocks: the first
        // legal-looking cell is offered, the seal check refuses it, and the same
        // cell is offered again next tick forever -- two towers built while
        // holding 248 gold.
        var refused = new HashSet<int>();
        int pending = -1;

        for (int t = 0; t < 20_000; t++)
        {
            if (!_driver.State.WaveActive) _driver.Enqueue(new StartWaveCommand());

            // Recomputed rather than cached: every build can move the route, so
            // a list taken once goes stale and starts naming cells nowhere near
            // where the creeps now walk.
            if (pending < 0 && _driver.State.Gold >= cost
                && TryNextPlacement(refused, out GridCell spot))
            {
                pending = _driver.Map.Index(spot);
                _driver.Enqueue(new BuildCommand(spot, arrow));
            }

            _driver.StepOneTick();

            foreach (SimEvent e in _driver.FrameEvents)
            {
                if (e.Kind == EventKind.BuildRejected && pending >= 0) refused.Add(pending);
                if (e.Kind is EventKind.BuildRejected or EventKind.BuildPlaced) pending = -1;
            }

            // Wait for real damage, not the first scratch. One 22-point hit on
            // an 800-hp tower is a 3% tint shift -- a capture taken there would
            // "verify" a cue nobody could see.
            if (SapperOnBoard(sapper) && AWoundedTowerExists(0.3f)) break;
        }

        int wounded = 0, sappers = 0;
        for (int k = 0; k < _driver.State.TowerCount; k++)
        {
            int slot = _driver.State.TowerSlotByOrder(k);
            if (_driver.State.TowerHp(slot) < _driver.Content.Tower(_driver.State.TowerDefIndex(slot)).Hp)
                wounded++;
        }
        for (int k = 0; k < _driver.State.CreepCount; k++)
            if (_driver.State.CreepDefIndex(_driver.State.CreepSlotByOrder(k)) == sapper) sappers++;

        // Where the worst-hurt tower is on screen, so a verification crop can be
        // aimed at the cue instead of hunting for it.
        int worstSlot = -1;
        float worst = 2f;
        for (int k = 0; k < _driver.State.TowerCount; k++)
        {
            int slot = _driver.State.TowerSlotByOrder(k);
            float f = (float)_driver.State.TowerHp(slot)
                      / _driver.Content.Tower(_driver.State.TowerDefIndex(slot)).Hp;
            if (f >= worst) continue;
            worst = f;
            worstSlot = slot;
        }

        string worstAt = "none";
        if (worstSlot >= 0)
        {
            int c = _driver.State.TowerCellIndex(worstSlot);
            Vector3 world = IsoGrid.CellCentre(c % _driver.Map.Width, c / _driver.Map.Width);
            Vector2 screen = _camera.UnprojectPosition(world);
            worstAt = $"cell({c % _driver.Map.Width},{c / _driver.Map.Width}) " +
                      $"screen({screen.X:F0},{screen.Y:F0}) hp={worst:P0}";
        }

        GD.Print($"shot-state: tick={_driver.TickCount} hash={_driver.Sim.Hash():x16} " +
                 $"wave={_driver.State.WaveIndex} gold={_driver.State.Gold} " +
                 $"lives={_driver.State.Lives} creeps={_driver.State.CreepCount} " +
                 $"towers={_driver.State.TowerCount} sappers={sappers} wounded={wounded}");
        GD.Print($"shot-worst: {worstAt}");
    }

    /// <summary>
    /// Freezes BETWEEN waves with a damaged tower under the cursor, so the
    /// capture shows the repair offer rather than the damage alone.
    ///
    /// Between waves is not incidental framing: repair is refused while a wave
    /// runs, so a capture taken mid-wave would show the rule's refusal text and
    /// verify the opposite of what criterion 12 claims.
    /// </summary>
    private void SeedRepair()
    {
        ushort arrow = _driver.Content.TowerIndexOf("arrow-tower");
        ushort sapper = _driver.Content.EnemyIndexOf("sapper");
        int cost = _driver.Content.Tower(arrow).Cost;

        var refused = new HashSet<int>();
        int pending = -1;
        bool sappersSeen = false;

        for (int t = 0; t < 20_000; t++)
        {
            // Stop pulling waves once a tower is hurt enough to be worth showing:
            // the shot has to land in the gap between waves, and the only way to
            // reach that gap is to stop asking for the next one.
            bool holding = sappersSeen && AWoundedTowerExists(0.6f);
            if (!holding && !_driver.State.WaveActive) _driver.Enqueue(new StartWaveCommand());

            if (pending < 0 && _driver.State.Gold >= cost
                && TryNextPlacement(refused, out GridCell spot))
            {
                pending = _driver.Map.Index(spot);
                _driver.Enqueue(new BuildCommand(spot, arrow));
            }

            _driver.StepOneTick();

            foreach (SimEvent e in _driver.FrameEvents)
            {
                if (e.Kind == EventKind.BuildRejected && pending >= 0) refused.Add(pending);
                if (e.Kind is EventKind.BuildRejected or EventKind.BuildPlaced) pending = -1;
            }

            if (SapperOnBoard(sapper)) sappersSeen = true;

            // The frame we want: hurt tower, no wave running, board still alive.
            if (holding && !_driver.State.WaveActive && _driver.State.CreepCount == 0) break;
        }

        int worstSlot = -1;
        float worst = 2f;
        for (int k = 0; k < _driver.State.TowerCount; k++)
        {
            int slot = _driver.State.TowerSlotByOrder(k);
            float f = (float)_driver.State.TowerHp(slot)
                      / _driver.Content.Tower(_driver.State.TowerDefIndex(slot)).Hp;
            if (f >= worst) continue;
            worst = f;
            worstSlot = slot;
        }

        string prompt = "none";
        if (worstSlot >= 0)
        {
            int c = _driver.State.TowerCellIndex(worstSlot);
            _shotHoverCell = new GridCell(c % _driver.Map.Width, c / _driver.Map.Width);
            prompt = RepairPromptFor(_shotHoverCell.Value) ?? "none";
        }

        GD.Print($"shot-state: tick={_driver.TickCount} hash={_driver.Sim.Hash():x16} " +
                 $"wave={_driver.State.WaveIndex} waveActive={_driver.State.WaveActive} " +
                 $"gold={_driver.State.Gold} lives={_driver.State.Lives} " +
                 $"towers={_driver.State.TowerCount} worstHp={worst:P0}");
        GD.Print($"shot-prompt: {prompt}");
    }

    /// <summary>First route-adjacent cell that is free and not already blocked.</summary>
    private bool TryNextPlacement(HashSet<int> refused, out GridCell cell)
    {
        var taken = new HashSet<int>();
        for (int k = 0; k < _driver.State.TowerCount; k++)
            taken.Add(_driver.State.TowerCellIndex(_driver.State.TowerSlotByOrder(k)));

        foreach (GridCell candidate in BuildableBesideRoute())
        {
            int index = _driver.Map.Index(candidate);
            if (taken.Contains(index) || refused.Contains(index)) continue;
            if (_driver.Sim.Path.IsBlocked(index)) continue;
            cell = candidate;
            return true;
        }

        cell = default;
        return false;
    }

    private bool SapperOnBoard(ushort sapper)
    {
        for (int k = 0; k < _driver.State.CreepCount; k++)
            if (_driver.State.CreepDefIndex(_driver.State.CreepSlotByOrder(k)) == sapper) return true;
        return false;
    }

    private bool AWoundedTowerExists(float below)
    {
        for (int k = 0; k < _driver.State.TowerCount; k++)
        {
            int slot = _driver.State.TowerSlotByOrder(k);
            int full = _driver.Content.Tower(_driver.State.TowerDefIndex(slot)).Hp;
            if (_driver.State.TowerHp(slot) < full * below) return true;
        }
        return false;
    }

    /// <summary>
    /// Buildable cells beside the route the creeps actually walk, nearest the
    /// spawn first. Coverage placement, the same idea the balance policy uses.
    ///
    /// It has to come from the flow field, not the terrain. crossroads is a
    /// mazing map: the route runs over buildable cells, so "adjacent to a
    /// PathOnly cell" found nine placements. Building in plain index order was
    /// no better -- it filled a far corner, killed nothing, earned no bounty,
    /// and the seeded run was dead by wave 12 with nine towers both times.
    /// </summary>
    private List<GridCell> BuildableBesideRoute()
    {
        MapDef map = _driver.Map;
        var route = new int[4096];
        var seen = new HashSet<int>();
        var result = new List<GridCell>();

        foreach (GridCell spawn in map.Spawns)
        {
            int count = _driver.Sim.Path.TraceRoute(map.Index(spawn), route);
            for (int i = 0; i < count; i++)
            {
                int rx = route[i] % map.Width, ry = route[i] / map.Width;
                foreach ((int dx, int dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
                {
                    int nx = rx + dx, ny = ry + dy;
                    if (nx < 0 || ny < 0 || nx >= map.Width || ny >= map.Height) continue;

                    var cell = new GridCell(nx, ny);
                    int index = map.Index(cell);
                    if (map.Cells[index] != CellKind.Buildable) continue;
                    if (!seen.Add(index)) continue;
                    result.Add(cell);
                }
            }
        }
        return result;
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
