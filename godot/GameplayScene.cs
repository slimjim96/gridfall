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
    private const string DefaultMapId = "crossroads";

    /// <summary>
    /// Board chosen from the selector, carried across a scene reload. Same
    /// mechanism as PlaytestDraft: a static survives ReloadCurrentScene, and the
    /// alternative is a menu scene this game does not need.
    /// </summary>
    public static string? PendingMapId;

    private string _mapId = DefaultMapId;
    private string _repoRoot = "";

    /// <summary>
    /// Set by the board editor before switching scenes: play THIS map, unsaved.
    /// Playtesting a map you have not written to disk is the entire point of F5.
    /// </summary>
    public static MapDef? PlaytestDraft;

    private bool _fromEditor;

    /// <summary>Set by GameOver or RunComplete. The sim stops being advanced.</summary>
    private bool _runEnded;

    private SimDriver _driver = null!;
    private WorldRenderer _world = null!;
    private UnitRenderer _units = null!;
    private RouteOverlay _routes = null!;
    private Hud _hud = null!;
    private StationBar _stationBar = null!;
    private WaveCountdown _countdown = null!;
    private BoardSelect _boards = null!;
    private Camera3D _camera = null!;
    private CameraRig _rig = null!;

    private ushort _selectedStation;
    private string _selectedStationName = "";

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

        string root = _repoRoot = ContentFiles.FindRepoRoot();

        // The game reads the same tile folders the editor does. If it did not,
        // a board would look one way while you painted it and another way when
        // you played it -- exactly the editor/game divergence the tooling rules
        // exist to prevent.
        TileLibrary.Scan(root);
        UnitAssets.Scan(root, _unitsOverride);

        // --map wins, then a selector choice, then the default.
        _mapId = ParseMapArg() ?? PendingMapId ?? DefaultMapId;
        PendingMapId = null;

        MapDef map = PlaytestDraft ?? ContentFiles.LoadMap(root, _mapId);
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
        ContentSet content = ContentFiles.LoadContent(root, _mapId);

        _driver = new SimDriver(map, content, seed: 1);
        // Set properly from the board's roster once the bar is built. Hardcoding
        // "arrow-station" here would start a board that does not offer it with a
        // selection the sim refuses on every click.
        _selectedStation = 0;
        _selectedStationName = content.Station(_selectedStation).Name;

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

        // Both live inside the HUD's CanvasLayer so they scale and sort with the
        // rest of the overlay rather than needing a layer of their own.
        _stationBar = new StationBar();
        _hud.AddChild(_stationBar);
        _stationBar.Populate(map, content);
        SelectSlot(0);

        _countdown = new WaveCountdown();
        _hud.AddChild(_countdown);

        _boards = new BoardSelect { Visible = false };
        AddChild(_boards);

        if (_shotPath is not null) SeedForScreenshot();
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;

        // In shot mode the sim is frozen after a fixed number of deterministic
        // steps, so the captured frame is the same frame every run. Advancing by
        // wall clock here would make the screenshot depend on how fast the
        // machine happened to be, which defeats using it as a visual baseline.
        // A finished run stops advancing. The sim reports the ending and does
        // not stop itself (EconomySystem) -- this is the caller that decides,
        // and until now there was not one: GameOver fired into nothing and the
        // game kept playing at zero patience forever.
        if (_shotPath is null && !_runEnded) _driver.Advance(dt);
        _world.RebuildIfChanged();
        _routes.RebuildLiveIfChanged();

        // Fixed delta in shot mode too: idle bob and hit flash are view-side and
        // wall-clock driven, so a real delta makes two captures of the same
        // simulation state differ by a few pixels. A fixed frame count times a
        // fixed delta makes the whole frame reproducible, which is what a visual
        // baseline needs.
        _units.Render(_shotPath is null ? dt : 1f / 60f);

        foreach (SimEvent e in _driver.FrameEvents)
        {
            if (e.Kind is EventKind.BuildRejected or EventKind.RepairRejected)
                _hud.ShowRefusal((RejectReason)e.A);

            if (e.Kind == EventKind.GameOver) EndRun(won: false);
            if (e.Kind == EventKind.RunComplete) EndRun(won: true);
        }

        _rig.Update(dt);
        // Priced by the sim's own function, never a second copy of the rule --
        // a HUD that quotes a different number than the one charged is worse
        // than no HUD.
        int cost = _driver.SelectedStationCost(_selectedStation);
        bool premium = cost != _driver.Content.Station(_selectedStation).Cost;
        _hud.Refresh(_driver.State, _selectedStationName, cost, premium, dt);
        _stationBar.Refresh(_driver.State, _selectedStation, _driver.SelectedStationCost, premium);

        // The window the CURRENT wave was armed with -- the ring's denominator.
        // Read from the wave def rather than remembered, so calling a wave early
        // cannot leave the next window starting part-drained.
        int waveIndex = _driver.State.WaveIndex;
        int prepTicks = waveIndex < _driver.Content.Waves.Length
            ? _driver.Content.Waves[waveIndex].PrepTicks
            : 0;
        _countdown.Refresh(_driver.State, prepTicks, Sim.TicksPerSecond, dt);

        UpdateHover();

        if (_shotPath is not null && ++_framesRendered >= _shotAfterFrames) CaptureAndQuit();
    }

    /// <summary>--map &lt;id&gt;: play a specific board. Used by captures and by the launcher.</summary>
    private static string? ParseMapArg()
    {
        string[] args = OS.GetCmdlineUserArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--map") return args[i + 1];
        return null;
    }

    private void EndRun(bool won)
    {
        if (_runEnded) return;
        _runEnded = true;

        // Straight into the next choice. A run that ends on a dead screen is
        // the same dead time the prep window was added to remove.
        _boards.Open(_repoRoot,
            won ? $"RUN COMPLETE  --  {_mapId}, {_driver.State.Patience} patience left"
                : $"OVERRUN  --  {_mapId}, cleared {_driver.State.WaveIndex - 1} of {_driver.Content.Waves.Length} waves");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // The rig first: it owns the wheel, middle-drag and the pan keys, and
        // returns false for everything it does not claim.
        if (_rig.HandleInput(@event)) return;

        if (@event is InputEventKey { Pressed: true } key)
        {
            if (_boards.Visible)
            {
                if (_boards.HandleKey(key))
                {
                    PendingMapId = _boards.Chosen;
                    GetTree().ReloadCurrentScene();
                }
                else if (key.Keycode == Key.Escape) GetTree().Quit();
                return;
            }

            switch (key.Keycode)
            {
                case Key.Space: _driver.Enqueue(new StartWaveCommand()); break;
                // Slots, not station ids: the number key selects the nth thing the
                // board offers, so a roster of one has no dead 2 key and a roster
                // that omits the arrow station still starts at 1.
                case >= Key.Key1 and <= Key.Key9:
                    SelectSlot((int)(key.Keycode - Key.Key1));
                    break;
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
            _driver.Enqueue(new BuildCommand(cell, _selectedStation));
        }
        else if (click.ButtonIndex == MouseButton.Right)
        {
            if (StationSlotAt(cell) is { } slot)
                _driver.Enqueue(new SellCommand(_driver.State.StationId(slot)));
        }
        else if (click.ButtonIndex == MouseButton.Middle)
        {
            if (StationSlotAt(cell) is { } slot)
                _driver.Enqueue(new RepairCommand(_driver.State.StationId(slot)));
        }
    }

    /// <summary>
    /// The repair offer for the station under the cursor, or null if there is
    /// nothing to offer.
    ///
    /// Calls StationDef.RepairCostFor rather than reimplementing the curve: a
    /// second copy of the cost formula in the view is a divergence waiting to
    /// happen, and this one would be the copy the player reads.
    /// </summary>
    private string? RepairPromptFor(GridCell cell)
    {
        if (StationSlotAt(cell) is not { } slot) return null;

        SimStateView state = _driver.State;
        StationDef def = _driver.Content.Station(state.StationDefIndex(slot));
        int level = state.StationLevel(slot);
        int hp = state.StationStock(slot);
        int missing = def.Stock - hp;

        // Selling was the one command in the game whose price was never shown
        // before the click -- and now that the price MOVES with serving, hiding it
        // means the player cannot tell a repair from a write-off (pillar 2).
        int sell = def.SalvageValueAt(level, hp);
        string sellPart = $"sell {sell} (right click)";

        if (missing <= 0) return $"{def.Name}  --  {sellPart}";

        int percent = 100 * hp / def.Stock;

        // Naming the rule where the player meets it, rather than only in a
        // refusal after they have already clicked.
        string repairPart = state.WaveActive
            ? "repair between waves"
            : $"repair {def.RepairCostFor(level, missing)} gold (middle click)";

        return $"{def.Name} at {percent}%  --  {repairPart}  ·  {sellPart}";
    }

    /// <summary>The slot of the station standing on a cell, or null.</summary>
    private int? StationSlotAt(GridCell cell)
    {
        int index = _driver.Map.Index(cell);
        SimStateView state = _driver.State;
        for (int k = 0; k < state.StationCount; k++)
        {
            int slot = state.StationSlotByOrder(k);
            if (state.StationCellIndex(slot) == index) return slot;
        }
        return null;
    }

    /// <summary>
    /// Select the nth station this board offers. Out-of-range is ignored rather
    /// than clamped: pressing 3 on a two-station board should do nothing, not
    /// quietly select the cannon.
    /// </summary>
    private void SelectSlot(int slot)
    {
        if (slot < 0 || slot >= _stationBar.Order.Count) return;
        _selectedStation = _stationBar.Order[slot];
        _selectedStationName = _driver.Content.Station(_selectedStation).Name;
    }

    private void UpdateHover()
    {
        // Shot mode has no mouse, so hover a fixed cell -- one squarely on the
        // lane, where a build forces a visible detour. Without this the capture
        // shows the live route only and the preview goes unverified.
        if (_shotPath is not null)
        {
            // A seed that makes a claim about a hovered station sets the cell; the
            // rest hover a cell squarely on the lane, where a build forces a
            // visible detour.
            GridCell hovered = _shotHoverCell ?? new GridCell(10, 4);
            _world.ShowHover(hovered, true);
            _world.ShowRange(hovered, SelectedRangeCells(), true);
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

        // Shown on every hovered cell, not only buildable ones: "how far would
        // this reach from here" is the question being asked while looking for a
        // spot, and refusing to answer it on the cell you are considering is the
        // opposite of helpful. Legality is already carried by the hover colour.
        _world.ShowRange(cell, SelectedRangeCells(), buildable);
        _hud.ShowRepairPrompt(RepairPromptFor(cell));

        // Only preview where a build is actually possible: showing a hypothetical
        // route for a cell you cannot build on answers a question nobody asked.
        if (buildable) _routes.ShowPreviewFor(cell);
        else _routes.ClearPreview();
    }

    /// <summary>
    /// The selected station's reach in cells, at the level it would be built —
    /// level 1, since a placement is always a fresh station.
    ///
    /// Read off StationDef.Range, the same value TargetingSystem compares against,
    /// so the ring cannot promise a reach the station does not have.
    /// </summary>
    private float SelectedRangeCells()
        => _driver.Content.Station(_selectedStation).Range.ToFloat();

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
    /// Put something worth looking at on the board: a few stations and a running
    /// wave, stepped deterministically rather than by wall clock so the capture
    /// is the same frame every time.
    /// </summary>
    /// <summary>
    /// The prep window, partly spent, with a station or two already up.
    ///
    /// Its own seed because the default one starts a wave immediately and the
    /// countdown is only ever on screen when a wave is NOT running -- the two
    /// claims cannot share a frame, and neither should perturb the other's
    /// committed baseline.
    ///
    /// Ticks a fixed distance into the window rather than capturing at tick 0:
    /// a full ring proves the widget draws, and a partly-drained one proves it
    /// is reading the counter.
    /// </summary>
    private void SeedCountdown()
    {
        ushort arrow = _driver.Content.StationIndexOf("arrow-station");
        _driver.Enqueue(new BuildCommand(new GridCell(2, 3), arrow));
        _driver.StepOneTick();
        _driver.Enqueue(new BuildCommand(new GridCell(6, 5), arrow));
        _driver.StepOneTick();

        // No StartWaveCommand: the window before wave 1 is armed by the Sim
        // constructor, so the countdown is already running and spending ticks is
        // the whole seed. 120 of 300 leaves the ring visibly past halfway.
        for (int t = 0; t < 120; t++) _driver.StepOneTick();

        GD.Print($"countdown seed: prep {_driver.State.PrepTicksRemaining} ticks left, "
                 + $"wave active {_driver.State.WaveActive}");
    }

    private void SeedForScreenshot()
    {
        if (_shotSeed == "sappers") { SeedSappers(); return; }
        if (_shotSeed == "repair") { SeedRepair(); return; }
        if (_shotSeed == "formats") { SeedFormats(); return; }
        if (_shotSeed == "countdown") { SeedCountdown(); return; }

        ushort arrow = _driver.Content.StationIndexOf("arrow-station");

        // Budgeted deliberately. Starting gold is 200 and a level-2 upgrade costs
        // 110, so the first attempt at this seed built three stations, had 18 gold
        // left, and the upgrades were correctly refused -- producing a capture
        // that showed no level cue at all and "verified" nothing.
        _driver.Enqueue(new BuildCommand(new GridCell(2, 3), arrow));   // 200 -> 150
        _driver.StepOneTick();

        int upgraded = _driver.State.StationId(_driver.State.StationSlotByOrder(0));
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
        for (int k = 0; k < _driver.State.StationCount; k++)
        {
            int slot = _driver.State.StationSlotByOrder(k);
            levels.Append($" t{_driver.State.StationId(slot)}=L{_driver.State.StationLevel(slot)}");
        }

        GD.Print($"shot-state: tick={_driver.TickCount} hash={_driver.Sim.Hash():x16} " +
                 $"gold={_driver.State.Gold} patience={_driver.State.Patience} " +
                 $"visitors={_driver.State.VisitorCount} stations={_driver.State.StationCount}" +
                 $" levels:{levels}");
    }

    /// <summary>
    /// Both asset formats on the board at once, with visitors walking behind them.
    ///
    /// `arrow-station` resolves to a SpriteUnitView and `cannon` to a MeshUnitView
    /// (see presentation/units/), so one frame checks both halves of ADR-0004.
    ///
    /// The claim being checked is **occlusion**, which is the property the whole
    /// format question turns on. Stations go on row 5 and visitors walk row 4:
    /// the camera sits at +X+Z, so a larger grid `x + y` is nearer, which puts
    /// row 5 in FRONT of row 4. If either view fails to write depth, its visitors
    /// show through it and the frame says so immediately.
    /// </summary>
    private void SeedFormats()
    {
        ushort arrow = _driver.Content.StationIndexOf("arrow-station");
        ushort cannon = _driver.Content.StationIndexOf("cannon");

        _driver.Enqueue(new BuildCommand(new GridCell(9, 5), arrow));     // 300 -> 250
        _driver.StepOneTick();
        _driver.Enqueue(new BuildCommand(new GridCell(10, 5), cannon));   // 250 -> 160
        _driver.StepOneTick();

        _driver.Enqueue(new StartWaveCommand());

        // Freeze on the board STATE, not on a guessed tick count. A frame taken
        // while the visitors are still at the spawn would show two stations occluding
        // nothing and would have verified precisely nothing.
        int waited = 0;
        while (waited < 4000 && !VisitorIsBehindTheStations()) { _driver.StepOneTick(); waited++; }

        GD.Print($"shot-state: tick={_driver.TickCount} hash={_driver.Sim.Hash():x16} " +
                 $"gold={_driver.State.Gold} visitors={_driver.State.VisitorCount} " +
                 $"stations={_driver.State.StationCount} waited={waited} " +
                 $"occluded={VisitorIsBehindTheStations()}");
    }

    /// <summary>A visitor on the lane directly behind the two seeded stations.</summary>
    private bool VisitorIsBehindTheStations()
    {
        for (int k = 0; k < _driver.State.VisitorCount; k++)
        {
            int slot = _driver.State.VisitorSlotByOrder(k);
            int index = _driver.State.VisitorCellIndex(slot);
            int x = index % _driver.Map.Width;
            int y = index / _driver.Map.Width;
            if (y == 4 && x is >= 9 and <= 11) return true;
        }
        return false;
    }

    /// <summary>
    /// Runs to the first wave that contains sappers and freezes with at least
    /// one of them on the board beside a depleted station.
    ///
    /// Sappers first appear at wave 5, so unlike the upgrade seed this one has
    /// to actually play the game to get there. Everything is stepped through
    /// StepOneTick, so the frame stays byte-reproducible.
    /// </summary>
    private void SeedSappers()
    {
        ushort arrow = _driver.Content.StationIndexOf("arrow-station");
        ushort sapper = _driver.Content.VisitorIndexOf("sapper");

        // Build across the board as gold allows. One cursor over the list, never
        // than parked in a corner. One cursor over the list, never revisited: the
        // first version re-offered cells it had already built on, so every build
        // after the sixth was refused and the run was lost before wave 5.
        int cost = _driver.Content.Station(arrow).Cost;

        // Cells the sim has refused. Without this the seed livelocks: the first
        // legal-looking cell is offered, the seal check refuses it, and the same
        // cell is offered again next tick forever -- two stations built while
        // holding 248 gold.
        var refused = new HashSet<int>();
        int pending = -1;

        for (int t = 0; t < 20_000; t++)
        {
            // Recomputed rather than cached: every build can move the route, so
            // a list taken once goes stale and starts naming cells nowhere near
            // where the visitors now walk.
            // Between waves only. That is the fix AND it keeps the cost simple:
            // no mid-wave premium ever applies, so def.Cost is the real price.
            bool between = !_driver.State.WaveActive;
            bool built = false;
            if (between && pending < 0 && _driver.State.Gold >= cost
                && TryNextPlacement(refused, out GridCell spot))
            {
                pending = _driver.Map.Index(spot);
                _driver.Enqueue(new BuildCommand(spot, arrow));
                built = true;
            }

            // Spend first, THEN call the wave -- the order PlayPolicy uses.
            //
            // This loop used to start the wave on the tick it went inactive and
            // build afterwards, so every station was bought mid-wave. Harmless
            // while building cost the same either way; the moment
            // midWaveBuildPercent existed, the seed paid the premium on all 28
            // stations, could afford 5, and finished the capture at 0 patience. A
            // verification seed must not model the one playstyle the economy is
            // designed to discourage.
            if (between && !built && pending < 0)
                _driver.Enqueue(new StartWaveCommand());

            _driver.StepOneTick();

            foreach (SimEvent e in _driver.FrameEvents)
            {
                if (e.Kind == EventKind.BuildRejected && pending >= 0) refused.Add(pending);
                if (e.Kind is EventKind.BuildRejected or EventKind.BuildPlaced) pending = -1;
            }

            // Wait for real serving, not the first scratch. One 22-point hit on
            // an 800-hp station is a 3% tint shift -- a capture taken there would
            // "verify" a cue nobody could see.
            if (SapperOnBoard(sapper) && AWoundedStationExists(0.3f)) break;
        }

        int wounded = 0, sappers = 0;
        for (int k = 0; k < _driver.State.StationCount; k++)
        {
            int slot = _driver.State.StationSlotByOrder(k);
            if (_driver.State.StationStock(slot) < _driver.Content.Station(_driver.State.StationDefIndex(slot)).Stock)
                wounded++;
        }
        for (int k = 0; k < _driver.State.VisitorCount; k++)
            if (_driver.State.VisitorDefIndex(_driver.State.VisitorSlotByOrder(k)) == sapper) sappers++;

        // Where the worst-hurt station is on screen, so a verification crop can be
        // aimed at the cue instead of hunting for it.
        int worstSlot = -1;
        float worst = 2f;
        for (int k = 0; k < _driver.State.StationCount; k++)
        {
            int slot = _driver.State.StationSlotByOrder(k);
            float f = (float)_driver.State.StationStock(slot)
                      / _driver.Content.Station(_driver.State.StationDefIndex(slot)).Stock;
            if (f >= worst) continue;
            worst = f;
            worstSlot = slot;
        }

        string worstAt = "none";
        if (worstSlot >= 0)
        {
            int c = _driver.State.StationCellIndex(worstSlot);
            Vector3 world = IsoGrid.CellCentre(c % _driver.Map.Width, c / _driver.Map.Width,
                IsoGrid.TerrainHeight(_driver.Map, c % _driver.Map.Width, c / _driver.Map.Width));
            Vector2 screen = _camera.UnprojectPosition(world);
            worstAt = $"cell({c % _driver.Map.Width},{c / _driver.Map.Width}) " +
                      $"screen({screen.X:F0},{screen.Y:F0}) hp={worst:P0}";
        }

        GD.Print($"shot-state: tick={_driver.TickCount} hash={_driver.Sim.Hash():x16} " +
                 $"wave={_driver.State.WaveIndex} gold={_driver.State.Gold} " +
                 $"patience={_driver.State.Patience} visitors={_driver.State.VisitorCount} " +
                 $"stations={_driver.State.StationCount} sappers={sappers} wounded={wounded}");
        GD.Print($"shot-worst: {worstAt}");
    }

    /// <summary>
    /// Freezes BETWEEN waves with a depleted station under the cursor, so the
    /// capture shows the repair offer rather than the serving alone.
    ///
    /// Between waves is not incidental framing: repair is refused while a wave
    /// runs, so a capture taken mid-wave would show the rule's refusal text and
    /// verify the opposite of what criterion 12 claims.
    /// </summary>
    private void SeedRepair()
    {
        ushort arrow = _driver.Content.StationIndexOf("arrow-station");
        ushort sapper = _driver.Content.VisitorIndexOf("sapper");
        int cost = _driver.Content.Station(arrow).Cost;

        var refused = new HashSet<int>();
        int pending = -1;
        bool sappersSeen = false;

        for (int t = 0; t < 20_000; t++)
        {
            // Stop pulling waves once a station is hurt enough to be worth showing:
            // the shot has to land in the gap between waves, and the only way to
            // reach that gap is to stop asking for the next one.
            bool holding = sappersSeen && AWoundedStationExists(0.6f);

            // Between waves only, and spend before calling the next one -- same
            // fix and same reason as SeedSappers: a seed that buys its stations
            // mid-wave pays midWaveBuildPercent on every one and finishes the
            // capture overrun at 5 stations instead of 28.
            bool between = !_driver.State.WaveActive;
            bool built = false;

            if (between && pending < 0 && _driver.State.Gold >= cost
                && TryNextPlacement(refused, out GridCell spot))
            {
                pending = _driver.Map.Index(spot);
                _driver.Enqueue(new BuildCommand(spot, arrow));
                built = true;
            }

            if (!holding && between && !built && pending < 0)
                _driver.Enqueue(new StartWaveCommand());

            _driver.StepOneTick();

            foreach (SimEvent e in _driver.FrameEvents)
            {
                if (e.Kind == EventKind.BuildRejected && pending >= 0) refused.Add(pending);
                if (e.Kind is EventKind.BuildRejected or EventKind.BuildPlaced) pending = -1;
            }

            if (SapperOnBoard(sapper)) sappersSeen = true;

            // The frame we want: hurt station, no wave running, board still alive.
            if (holding && !_driver.State.WaveActive && _driver.State.VisitorCount == 0) break;
        }

        int worstSlot = -1;
        float worst = 2f;
        for (int k = 0; k < _driver.State.StationCount; k++)
        {
            int slot = _driver.State.StationSlotByOrder(k);
            float f = (float)_driver.State.StationStock(slot)
                      / _driver.Content.Station(_driver.State.StationDefIndex(slot)).Stock;
            if (f >= worst) continue;
            worst = f;
            worstSlot = slot;
        }

        string prompt = "none";
        if (worstSlot >= 0)
        {
            int c = _driver.State.StationCellIndex(worstSlot);
            _shotHoverCell = new GridCell(c % _driver.Map.Width, c / _driver.Map.Width);
            prompt = RepairPromptFor(_shotHoverCell.Value) ?? "none";
        }

        GD.Print($"shot-state: tick={_driver.TickCount} hash={_driver.Sim.Hash():x16} " +
                 $"wave={_driver.State.WaveIndex} waveActive={_driver.State.WaveActive} " +
                 $"gold={_driver.State.Gold} patience={_driver.State.Patience} " +
                 $"stations={_driver.State.StationCount} worstAppetite={worst:P0}");
        GD.Print($"shot-prompt: {prompt}");
    }

    /// <summary>First route-adjacent cell that is free and not already blocked.</summary>
    private bool TryNextPlacement(HashSet<int> refused, out GridCell cell)
    {
        var taken = new HashSet<int>();
        for (int k = 0; k < _driver.State.StationCount; k++)
            taken.Add(_driver.State.StationCellIndex(_driver.State.StationSlotByOrder(k)));

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
        for (int k = 0; k < _driver.State.VisitorCount; k++)
            if (_driver.State.VisitorDefIndex(_driver.State.VisitorSlotByOrder(k)) == sapper) return true;
        return false;
    }

    private bool AWoundedStationExists(float below)
    {
        for (int k = 0; k < _driver.State.StationCount; k++)
        {
            int slot = _driver.State.StationSlotByOrder(k);
            int full = _driver.Content.Station(_driver.State.StationDefIndex(slot)).Stock;
            if (_driver.State.StationStock(slot) < full * below) return true;
        }
        return false;
    }

    /// <summary>
    /// Buildable cells beside the route the visitors actually walk, nearest the
    /// spawn first. Coverage placement, the same idea the balance policy uses.
    ///
    /// It has to come from the flow field, not the terrain. crossroads is a
    /// mazing map: the route runs over buildable cells, so "adjacent to a
    /// PathOnly cell" found nine placements. Building in plain index order was
    /// no better -- it filled a far corner, killed nothing, earned no bounty,
    /// and the seeded run was dead by wave 12 with nine stations both times.
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
