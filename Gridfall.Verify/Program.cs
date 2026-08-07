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

      balance --map <id> [--runs N] [--seed N]
                                   Headless N-run wave sim; reports leak rate, gold,
                                   and time-to-clear against MapTargets.

      maps                         Geometry report for every map against MapTargets.
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

    MapDef map = ContentFiles.LoadMap(root, mapId);
    ContentSet content = ContentFiles.LoadContent(root, mapId);

    int totalSpawned = 0, totalLeaked = 0, runsLost = 0;
    var clearTicks = new List<int>();
    var goldAtEnd = new List<int>();

    for (int run = 0; run < runs; run++)
    {
        var sim = new Sim(map, content, baseSeed + (uint)run);

        // A fixed, deliberately unskilled policy: build what is affordable at the
        // first buildable cell, then run every wave. It is not "competent play"
        // yet -- see the caveat printed below.
        int waveCount = content.Waves.Length;
        int spawned = 0, leaked = 0;

        for (int wave = 0; wave < waveCount; wave++)
        {
            sim.Enqueue(new StartWaveCommand());
            for (int t = 0; t < 4000; t++)
            {
                sim.Tick();
                foreach (SimEvent e in sim.Events.Span)
                {
                    if (e.Kind == EventKind.CreepSpawned) spawned++;
                    if (e.Kind == EventKind.CreepLeaked) leaked++;
                }
                if (!sim.State.WaveActive && sim.State.CreepCount == 0 && t > 2) break;
            }
        }

        totalSpawned += spawned;
        totalLeaked += leaked;
        if (sim.State.Lives <= 0) runsLost++;
        clearTicks.Add(sim.TickCount);
        goldAtEnd.Add(sim.State.Gold);
    }

    double leakRate = totalSpawned == 0 ? 0 : 100.0 * totalLeaked / totalSpawned;
    double lostRate = 100.0 * runsLost / runs;

    Console.WriteLine($"Balance report -- map '{mapId}', {runs} runs, seed {baseSeed}");
    Console.WriteLine($"  spawned         {totalSpawned}");
    Console.WriteLine($"  leaked          {totalLeaked}  ({leakRate:F1}%, target <= 4.0%)");
    Console.WriteLine($"  runs lost       {runsLost}  ({lostRate:F1}%)");
    Console.WriteLine($"  ticks to finish {clearTicks.Average():F0} avg  ({clearTicks.Min()}..{clearTicks.Max()})");
    Console.WriteLine($"  gold at end     {goldAtEnd.Average():F0} avg");
    Console.WriteLine();
    Console.WriteLine("  CAVEAT: the policy here builds nothing. These numbers describe an");
    Console.WriteLine("  undefended board, not balance. A competent-play policy is required");
    Console.WriteLine("  before this report can be quoted against the targets.");
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
    Console.WriteLine($"{"map",-14} {"size",-8} {"buildable",-11} {"path",-6} {"spawns",-7} verdict");
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

        string verdict = warnings.Count == 0 ? "ok" : string.Join("; ", warnings);
        Console.WriteLine($"{mapId,-14} {map.Width + "x" + map.Height,-8} {pct + "%",-11} {shortest,-6} {map.Spawns.Length,-7} {verdict}");
    }
    return 0;
}
