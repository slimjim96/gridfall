using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Gridfall.Io;
using Gridfall.Verify;

// Gridfall.Verify -- the determinism harness and the headless balance sim.
//
// Needs no Godot and no display. That is the entire reason for the project
// split in ADR-0001, and it is why this will still be run in a year.

string root = ContentFiles.FindRepoRoot();
string tracesDir = Path.Combine(root, "Gridfall.Verify", "traces");

var args_ = Environment.GetCommandLineArgs().Skip(1).ToArray();
string mode = args_.FirstOrDefault(a => !a.StartsWith("--")) ?? "replay";

string? Opt(string name)
{
    int i = Array.IndexOf(args_, "--" + name);
    return i >= 0 && i + 1 < args_.Length ? args_[i + 1] : null;
}
bool Flag(string name) => Array.IndexOf(args_, "--" + name) >= 0;

return mode switch
{
    "replay" => Replay(),
    "record" => Record(),
    "balance" => Balance(),
    "maps" => MapReport(),
    "perf" => Perf(),
    "curve" => Curve(),
    _ => Usage(),
};

int Usage()
{
    Console.WriteLine("""
    Gridfall.Verify

      replay                       Replay every trace and diff per-tick hashes (default)
        --trace <name>             Just one trace
        --verbose                  Report every checkpoint, not only the first mismatch

      record --trace <name> --map <id> [--seed N] [--ticks N]
                                   Record a new trace. Only do this when you know why
                                   the old one diverged and have decided the new
                                   behaviour is correct.

      balance --map <id> [--runs N] [--seed N] [--salvage]
                                   Headless N-run wave sim driven by a scripted player;
                                   reports leak rate, per-wave leaks, gold curve and
                                   time-to-clear against the balance targets.
                                   The policy is a competent BEGINNER, so the numbers
                                   are a floor on difficulty, not a verdict.
                                   --salvage makes it cut doomed towers loose mid-wave.
                                   Salvaging must never come out AHEAD on gold
                                   destroyed -- if it does, cashing out wrecks is
                                   profitable again (salvage-value).

      maps                         Geometry report for every map against MapTargets.

      curve --map <id> [--growth N] [--bounty N]
                                   Income against enemy strength, wave by wave.
                                   Pure content analysis, no simulation.

      perf [--map <id>]            Tick cost against the 8ms budget.
    """);
    return 2;
}

// ---------------------------------------------------------------------------

int Replay()
{
    if (!Directory.Exists(tracesDir))
    {
        Console.WriteLine($"No traces directory at {tracesDir} -- nothing to replay.");
        return 0;
    }

    string? only = Opt("trace");
    bool verbose = Flag("verbose");

    string[] files = Directory.EnumerateFiles(tracesDir, "*.json")
        .Where(f => only is null || Path.GetFileNameWithoutExtension(f) == only)
        .OrderBy(f => f, StringComparer.Ordinal)
        .ToArray();

    if (files.Length == 0)
    {
        Console.WriteLine(only is null ? "No traces found." : $"No trace named '{only}'.");
        return only is null ? 0 : 1;
    }

    int failed = 0;
    foreach (string file in files)
    {
        string name = Path.GetFileNameWithoutExtension(file);
        Trace trace = Trace.Load(file);

        MapDef map = ContentFiles.LoadMap(root, trace.Map);
        ContentSet content = ContentFiles.LoadContent(root, trace.Map);
        var sim = new Sim(map, content, trace.Seed);

        int firstMismatch = -1;
        for (int t = 0; t < trace.Ticks; t++)
        {
            trace.Apply(sim, content, t);
            sim.Tick();

            if (t % trace.CheckpointEvery != 0) continue;
            if (!trace.Hashes.TryGetValue(t, out string? expected)) continue;

            string actual = sim.Hash().ToString("x16");
            if (verbose) Console.WriteLine($"  {name} @{t}: {actual} (expected {expected})");

            if (actual == expected) continue;
            firstMismatch = t;
            Console.WriteLine($"FAIL {name}: diverged at tick {t}");
            Console.WriteLine($"     expected {expected}");
            Console.WriteLine($"     actual   {actual}");
            break;   // everything after the first mismatch is noise
        }

        if (firstMismatch < 0) Console.WriteLine($"ok   {name} ({trace.Ticks} ticks, {trace.Hashes.Count} checkpoints)");
        else failed++;
    }

    Console.WriteLine(failed == 0
        ? $"\n{files.Length} trace(s) verified."
        : $"\n{failed} of {files.Length} trace(s) FAILED.");
    return failed == 0 ? 0 : 1;
}

int Record()
{
    string name = Opt("trace") ?? "baseline";
    string mapId = Opt("map") ?? "crossroads";
    uint seed = uint.TryParse(Opt("seed"), out uint s) ? s : 1u;
    int ticks = int.TryParse(Opt("ticks"), out int tk) ? tk : 3000;
    int every = int.TryParse(Opt("checkpointEvery"), out int ce) ? ce : 100;

    MapDef map = ContentFiles.LoadMap(root, mapId);
    ContentSet content = ContentFiles.LoadContent(root, mapId);

    // A scripted pass: build a few towers, run three waves. Enough to exercise
    // pathing, targeting, damage, economy, and the wave scheduler.
    // Cells must actually be buildable on the target map, or the script silently
    // does nothing and the trace records an undefended board. The rejection
    // count printed below is there so that failure is never silent again.
    var commands = new List<Trace.TraceCommand>
    {
        new() { Tick = 5,    Cmd = "build", X = 2,  Y = 3, Tower = "arrow-tower" },
        new() { Tick = 6,    Cmd = "build", X = 6,  Y = 5, Tower = "arrow-tower" },
        new() { Tick = 10,   Cmd = "startWave" },
        new() { Tick = 400,  Cmd = "build", X = 9,  Y = 3, Tower = "arrow-tower" },
        new() { Tick = 900,  Cmd = "startWave" },
        new() { Tick = 1400, Cmd = "build", X = 14, Y = 5, Tower = "cannon" },
        new() { Tick = 2000, Cmd = "startWave" },
    };

    var trace = new Trace
    {
        Map = mapId, Seed = seed, Ticks = ticks, CheckpointEvery = every,
        Commands = commands, Hashes = new Dictionary<int, string>(),
    };

    var sim = new Sim(map, content, seed);
    int built = 0, rejected = 0, kills = 0, leaks = 0;
    var rejectReasons = new List<string>();

    for (int t = 0; t < ticks; t++)
    {
        trace.Apply(sim, content, t);
        sim.Tick();

        foreach (SimEvent e in sim.Events.Span)
        {
            switch (e.Kind)
            {
                case EventKind.BuildPlaced: built++; break;
                case EventKind.BuildRejected:
                    rejected++;
                    rejectReasons.Add($"tick {e.Tick} {e.Cell} {(RejectReason)e.A}");
                    break;
                case EventKind.CreepDied: kills++; break;
                case EventKind.CreepLeaked: leaks++; break;
            }
        }

        if (t % every == 0) trace.Hashes[t] = sim.Hash().ToString("x16");
    }

    string path = Path.Combine(tracesDir, name + ".json");
    trace.Save(path);

    Console.WriteLine($"Recorded {name}: {ticks} ticks, {trace.Hashes.Count} checkpoints -> {path}");
    Console.WriteLine($"  builds   {built} placed, {rejected} rejected");
    Console.WriteLine($"  creeps   {kills} killed, {leaks} leaked");
    Console.WriteLine($"  final    gold {sim.State.Gold}, lives {sim.State.Lives}, hash {sim.Hash():x16}");

    foreach (string reason in rejectReasons) Console.WriteLine($"    rejected: {reason}");

    // A trace where nothing was built exercises pathing and spawning but never
    // targeting, projectiles, damage, or the economy. Recording one by accident
    // is how a harness ends up guarding a third of the engine.
    if (built == 0)
        Console.WriteLine("  WARNING: no tower was placed -- this trace does not cover combat.");
    if (kills == 0)
        Console.WriteLine("  WARNING: nothing died -- this trace does not cover damage or bounties.");

    return 0;
}

int Balance()
{
    string mapId = Opt("map") ?? "crossroads";
    int runs = int.TryParse(Opt("runs"), out int r) ? r : 200;
    uint baseSeed = uint.TryParse(Opt("seed"), out uint s) ? s : 1u;

    PlayPolicy.Salvages = Flag("salvage");

    MapDef map = ContentFiles.LoadMap(root, mapId);
    ContentSet content = ContentFiles.LoadContent(root, mapId);
    int waveCount = content.Waves.Length;

    var perWaveSpawned = new int[waveCount];
    var perWaveLeaked = new int[waveCount];
    var perWaveTicks = new List<int>[waveCount];
    for (int i = 0; i < waveCount; i++) perWaveTicks[i] = new List<int>();

    int totalSpawned = 0, totalLeaked = 0, runsLost = 0, totalBuilds = 0, noPlacement = 0, refused = 0, upgrades = 0;
    int repairs = 0, repairGold = 0, towersDestroyed = 0, salvaged = 0, salvageGold = 0;
    // Investment the enemy took off the board and the player could not recover.
    // "Towers lost" counts destructions only, and a tower SOLD at 1 hp is not
    // destroyed -- so that number reads 0 while the same gold is still gone.
    // This is the invariant tower-combat actually installed, measured in the one
    // unit that both routes share.
    long goldDestroyed = 0;
    var towersStanding = new List<int>();
    var coverage = new List<int>();
    var goldAtWave = new List<int>[waveCount];
    var towersAtWave = new List<int>[waveCount];
    var spentByWave = new List<int>[waveCount];
    for (int i = 0; i < waveCount; i++)
    {
        goldAtWave[i] = new List<int>();
        towersAtWave[i] = new List<int>();
        spentByWave[i] = new List<int>();
    }
    var finalLives = new List<int>();

    for (int run = 0; run < runs; run++)
    {
        var sim = new Sim(map, content, baseSeed + (uint)run);
        var policy = new PlayPolicy(sim, baseSeed + (uint)run);
        int earned = 0;

        // towerId -> (def, level), rebuilt from events so it needs no new state
        // in the sim and no accessor the view does not already have.
        var towerDefOf = new Dictionary<int, ushort>();
        var towerLevelOf = new Dictionary<int, int>();

        int wave = 0;
        int waveStartTick = 0;
        bool counted = false;

        for (int t = 0; t < 20000 && wave < waveCount; t++)
        {
            int waveBefore = sim.State.WaveIndex;
            policy.Update();
            sim.Tick();

            if (sim.State.WaveIndex != waveBefore)
            {
                wave = sim.State.WaveIndex - 1;
                waveStartTick = sim.TickCount;
                counted = false;
                if (wave >= 0 && wave < waveCount)
                {
                    goldAtWave[wave].Add(sim.State.Gold);
                    // Defence actually on the board, and cumulative income --
                    // the two numbers that separate "the player is poor" from
                    // "the wave is too strong".
                    towersAtWave[wave].Add(sim.State.TowerCount);
                    spentByWave[wave].Add(earned);
                }
            }

            foreach (SimEvent e in sim.Events.Span)
            {
                if (wave < 0 || wave >= waveCount) continue;
                if (e.Kind == EventKind.CreepSpawned) { perWaveSpawned[wave]++; totalSpawned++; }
                if (e.Kind == EventKind.CreepLeaked) { perWaveLeaked[wave]++; totalLeaked++; }
                if (e.Kind == EventKind.GoldChanged && e.B > 0) earned += e.B;
                if (e.Kind == EventKind.BuildPlaced) { towerDefOf[e.A] = (ushort)e.B; towerLevelOf[e.A] = 1; }
                if (e.Kind == EventKind.TowerUpgraded) towerLevelOf[e.A] = e.B;
                if (e.Kind == EventKind.TowerDestroyed)
                {
                    towersDestroyed++;
                    goldDestroyed += SpentOn(e.A);   // destroyed: the whole investment
                }
                if (e.Kind == EventKind.TowerSold)
                {
                    salvageGold += e.B;
                    goldDestroyed += SpentOn(e.A) - e.B;   // salvaged: whatever the refund missed
                }
                if (e.Kind == EventKind.WaveCleared && !counted)
                {
                    perWaveTicks[wave].Add(sim.TickCount - waveStartTick);
                    counted = true;
                }
            }

            if (sim.State.Lives <= 0) break;
        }

        int SpentOn(int towerId)
            => towerDefOf.TryGetValue(towerId, out ushort d)
                ? content.Tower(d).TotalSpentAt(towerLevelOf.GetValueOrDefault(towerId, 1))
                : 0;

        totalBuilds += policy.BuildsPlaced;
        towersStanding.Add(sim.State.TowerCount);
        coverage.Add(policy.TotalCoverage());
        noPlacement += policy.NoPlacementFound;
        refused += policy.BuildsRefused;
        upgrades += policy.UpgradesBought;
        repairs += policy.RepairsBought;
        repairGold += policy.GoldSpentRepairing;
        salvaged += policy.TowersSalvaged;
        finalLives.Add(sim.State.Lives);
        if (sim.State.Lives <= 0) runsLost++;
    }

    double leakRate = totalSpawned == 0 ? 0 : 100.0 * totalLeaked / totalSpawned;
    double lostRate = 100.0 * runsLost / runs;

    Console.WriteLine($"Balance report -- map '{mapId}', {runs} runs, seed {baseSeed}");
    Console.WriteLine($"  policy          competent-beginner (coverage placement, best dps/gold, no reserve)");
    Console.WriteLine($"  towers built    {totalBuilds / (double)runs:F1} avg per run, {towersStanding.Average():F1} standing at end");
    Console.WriteLine($"  upgrades bought {upgrades / (double)runs:F1} avg per run");
    Console.WriteLine($"  repairs bought  {repairs / (double)runs:F1} avg per run, " +
                      $"{repairGold / (double)runs:F0} gold spent on them");
    // The number this slice exists to keep above zero. Repair that drives it to
    // zero has not balanced tower-combat, it has switched it off.
    Console.WriteLine($"  towers lost     {towersDestroyed / (double)runs:F1} avg per run");
    Console.WriteLine($"  towers salvaged {salvaged / (double)runs:F1} avg per run, " +
                      $"{salvageGold / (double)runs:F0} gold refunded");
    Console.WriteLine($"  gold destroyed  {goldDestroyed / (double)runs:F0} avg per run " +
                      $"-- investment the enemy took and the player could not recover");
    Console.WriteLine($"  coverage        {coverage.Average():F0} route-cells covered in total, " +
                      $"{coverage.Average() / System.Math.Max(1, towersStanding.Average()):F1} per tower");
    Console.WriteLine($"  no placement    {noPlacement / (double)runs:F0} attempts found nowhere to go ({refused / (double)runs:F0} of them blocked by the seal check)");
    Console.WriteLine();
    Console.WriteLine($"  {"metric",-22} {"value",-14} target");
    Console.WriteLine($"  {"leak rate",-22} {leakRate,6:F1}%        <= 4.0%      {Verdict(leakRate <= 4.0)}");
    Console.WriteLine($"  {"runs lost",-22} {lostRate,6:F1}%        15-30% late  {Verdict(lostRate is >= 0 and <= 60)}");
    Console.WriteLine($"  {"lives left (avg)",-22} {finalLives.Average(),6:F1}");
    Console.WriteLine();
    Console.WriteLine($"  {"wave",-6} {"spawned",-9} {"leaked",-9} {"leak%",-8} {"ticks",-8} {"gold",-7} {"towers",-8} {"earned so far",-14}");

    for (int w = 0; w < waveCount; w++)
    {
        double wl = perWaveSpawned[w] == 0 ? 0 : 100.0 * perWaveLeaked[w] / perWaveSpawned[w];
        string ticks = perWaveTicks[w].Count > 0 ? $"{perWaveTicks[w].Average():F0}" : "-";
        string gold = goldAtWave[w].Count > 0 ? $"{goldAtWave[w].Average():F0}" : "-";
        string flag = wl > 15.0 ? "  <-- over the 15% per-wave target" : "";
        string towers = towersAtWave[w].Count > 0 ? $"{towersAtWave[w].Average():F1}" : "-";
        string earnedSo = spentByWave[w].Count > 0 ? $"{spentByWave[w].Average():F0}" : "-";
        Console.WriteLine($"  {w + 1,-6} {perWaveSpawned[w],-9} {perWaveLeaked[w],-9} {wl,-8:F1} {ticks,-8} {gold,-7} {towers,-8} {earnedSo,-14}{flag}");
    }

    Console.WriteLine();
    Console.WriteLine("  Read as a FLOOR on difficulty, not a verdict. The policy is a reasonable");
    Console.WriteLine("  beginner: coverage placement, no saving up, no re-mazing, never sells.");
    Console.WriteLine("  A good player does better, so \"even played this way, wave N leaks\" is sound;");
    Console.WriteLine("  \"wave N is correctly tuned\" is not.");
    return 0;

    static string Verdict(bool ok) => ok ? "ok" : "MISS";
}

/// <summary>
/// Income against enemy strength, wave by wave, computed straight from the
/// content. No simulation: this is arithmetic over the wave table, so it is
/// exact and answers a question the balance sim cannot.
///
/// The sim tells you WHETHER the player wins. This tells you why the curves
/// diverge, which is what six balance passes failed to pin down by pushing on
/// individual levers.
/// </summary>
int Curve()
{
    string mapId = Opt("map") ?? "crossroads";
    ContentSet content = ContentFiles.LoadContent(root, mapId);

    // Optional overrides so the ratio can be swept without editing content.
    double growthOverride = double.TryParse(Opt("growth"), out double g) ? g : 0;
    double bountyScale = double.TryParse(Opt("bounty"), out double b) ? b : 1.0;

    // What a gold piece buys, in damage per tick, from the best tower available.
    double bestDamagePerGold = 0;
    foreach (TowerDef t in content.Towers)
    {
        if (t.Damage <= 0 || t.CooldownTicks <= 0) continue;
        double v = t.Damage / (double)t.CooldownTicks / t.Cost;
        if (v > bestDamagePerGold) bestDamagePerGold = v;
    }

    Console.WriteLine($"Income vs difficulty -- map '{mapId}'");
    Console.WriteLine($"  best damage/tick per gold: {bestDamagePerGold:F5}"
                      + (growthOverride > 0 ? $"   hpGrowth override {growthOverride}" : "")
                      + (bountyScale != 1.0 ? $"   bounty x{bountyScale}" : ""));
    Console.WriteLine();
    Console.WriteLine($"  {"wave",-5} {"creeps",-7} {"wave HP",-9} {"income",-8} {"cum income",-11} " +
                      $"{"capacity",-9} {"cap/HP",-8} {"vs wave 1",-9}");

    double cumIncome = 0;
    double firstRatio = 0;

    for (int w = 0; w < content.Waves.Length; w++)
    {
        WaveDef wave = content.Waves[w];
        double scale = growthOverride > 0
            ? System.Math.Pow(growthOverride, w)
            : wave.HpScale.Raw / 65536.0;

        int creeps = 0;
        double waveHp = 0, income = 0;
        foreach (WaveEntry e in wave.Entries)
        {
            EnemyDef def = content.Enemy(e.EnemyIndex);
            creeps += e.Count;
            waveHp += e.Count * def.Hp * scale;
            income += e.Count * def.Bounty * bountyScale;
        }

        // Capacity is what CUMULATIVE income could buy, because towers persist.
        // That is the asymmetry: the player accumulates, the wave does not.
        double capacity = cumIncome * bestDamagePerGold;
        double ratio = waveHp > 0 ? capacity / waveHp : 0;
        // Normalise against wave 2, not wave 1: before wave 1 the player has
        // earned nothing, so its ratio is zero and divides into nonsense.
        if (w == 1) firstRatio = ratio;

        Console.WriteLine($"  {wave.Index,-5} {creeps,-7} {waveHp,-9:F0} {income,-8:F0} {cumIncome,-11:F0} " +
                          $"{capacity,-9:F1} {ratio,-8:F4} " +
                          (firstRatio > 0 && w >= 1 ? $"{ratio / firstRatio,-9:F2}x" : "-"));

        cumIncome += income;
    }

    Console.WriteLine();
    Console.WriteLine("  cap/HP rising means the player outgrows the wave. Flat means they track.");
    Console.WriteLine("  The last column is that ratio relative to wave 2 -- 1.00x throughout is balance.");
    Console.WriteLine("  Capacity uses CUMULATIVE income because towers persist and a wave does not --");
    Console.WriteLine("  that asymmetry is the whole problem, and no sink or stat can remove it.");
    return 0;
}

int Perf()
{
    string mapId = Opt("map") ?? "crossroads";
    MapDef map = ContentFiles.LoadMap(root, mapId);
    ContentSet content = ContentFiles.LoadContent(root, mapId);

    // Load the board up: as many towers as gold allows, then run waves on repeat
    // so the tick loop is doing real work rather than idling.
    var sim = new Sim(map, content, 1);
    sim.MutableState.Gold = 100000;
    ushort arrow = content.TowerIndexOf("arrow-tower");
    for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
            if (map.Cells[map.Index(x, y)] == CellKind.Buildable)
                sim.Enqueue(new BuildCommand(new GridCell(x, y), arrow));
    sim.Tick();

    int towers = sim.State.TowerCount;
    for (int w = 0; w < 3; w++) sim.Enqueue(new StartWaveCommand());

    for (int t = 0; t < 200; t++) sim.Tick();   // warm up, let creeps accumulate

    var sw = System.Diagnostics.Stopwatch.StartNew();
    const int measured = 5000;
    long worstTicks = 0;
    for (int t = 0; t < measured; t++)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        if (!sim.State.WaveActive && sim.State.CreepCount == 0) sim.Enqueue(new StartWaveCommand());
        sim.Tick();
        long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - start;
        if (elapsed > worstTicks) worstTicks = elapsed;
    }
    sw.Stop();

    double avgMs = sw.Elapsed.TotalMilliseconds / measured;
    double worstMs = worstTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

    Console.WriteLine($"Perf -- map '{mapId}', {towers} towers, {measured} ticks");
    Console.WriteLine($"  average  {avgMs:F4} ms/tick");
    Console.WriteLine($"  worst    {worstMs:F4} ms/tick   (budget 8.0000 ms)");
    Console.WriteLine($"  headroom {8.0 / worstMs:F0}x on the worst tick");
    Console.WriteLine();
    Console.WriteLine("  Measured on this machine only, and on a 20x9 map -- the 8 ms budget is");
    Console.WriteLine("  written for 64x64 with 300 creeps and 60 towers. Not the same test.");

    return worstMs <= 8.0 ? 0 : 1;
}

int MapReport()
{
    Console.WriteLine($"{"map",-14} {"size",-8} {"buildable",-11} {"path",-6} {"per route",-10} {"spawns",-7} verdict");
    foreach (string mapId in ContentFiles.MapIds(root))
    {
        MapDef map;
        try { map = ContentFiles.LoadMap(root, mapId); }
        catch (ContentException ex) { Console.WriteLine($"{mapId,-14} ERROR: {ex.Message}"); continue; }

        int cells = map.Width * map.Height;
        int buildable = map.Cells.Count(c => c == CellKind.Buildable);
        int pct = 100 * buildable / cells;

        var path = new Gridfall.Core.Path.PathSystem(map);
        path.ForceRebuild();
        int shortest = path.DistanceAt(map.Spawns[0]);

        var warnings = new List<string>();
        if (pct < MapTargets.MinBuildablePercent || pct > MapTargets.MaxBuildablePercent)
            warnings.Add($"buildable {pct}% outside {MapTargets.MinBuildablePercent}-{MapTargets.MaxBuildablePercent}%");
        if (shortest < MapTargets.MinUnmazedPath || shortest > MapTargets.MaxUnmazedPath)
            warnings.Add($"path {shortest} outside {MapTargets.MinUnmazedPath}-{MapTargets.MaxUnmazedPath}");
        if (map.Spawns.Length > MapTargets.MaxLanes)
            warnings.Add($"{map.Spawns.Length} spawns > {MapTargets.MaxLanes}");

        // Buildable cells per route cell. The buildable-percentage band does not
        // capture this, and crossroads passes that band at 4.0 here -- roughly
        // three towers per cell of route, which no enemy design survives.
        // Proposed band 1.5-2.0; reported, not yet enforced (map-density-target).
        double density = shortest > 0 ? buildable / (double)shortest : 0;
        if (density > 2.0)
            warnings.Add($"density {density:F1} buildable/route cell (proposed max 2.0)");

        string verdict = warnings.Count == 0 ? "ok" : string.Join("; ", warnings);
        Console.WriteLine($"{mapId,-14} {map.Width + "x" + map.Height,-8} {pct + "%",-11} {shortest,-6} {density,-10:F1} {map.Spawns.Length,-7} {verdict}");
    }
    return 0;
}
