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
    "waves" => WaveReport(),
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
      waves [--map <id>]           Cadence sheet: when each group spawns, wave by wave.

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
    // WHICH wave killed the run, not just how many died. balance-targets.md has
    // carried separate early (0-5%) and late (15-30%) runs-lost targets since it
    // was written, and nothing has ever measured the split.
    var lostAtWave = new List<int>();

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
        if (sim.State.Lives <= 0) { runsLost++; lostAtWave.Add(sim.State.WaveIndex); }
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
    // The halfway point of the table this map actually has, not a wave number
    // carried from a 20-wave game that was never built. The bands mean "the first
    // half should rarely kill you, the second half should" -- a proportion, and
    // hardcoding 10 turned that into a two-wave window on a twelve-wave table
    // where no level could sit in band except by coin flip. See balance-targets.md.
    int earlyThrough = waveCount / 2;

    int lostEarly = lostAtWave.Count(w => w <= earlyThrough);
    int lostLate = lostAtWave.Count(w => w > earlyThrough);
    double earlyRate = 100.0 * lostEarly / runs;
    double lateRate = 100.0 * lostLate / runs;

    Console.WriteLine($"  {"runs lost",-22} {lostRate,6:F1}%        (split below)");
    Console.WriteLine($"  {$"  in waves 1-{earlyThrough}",-22} {earlyRate,6:F1}%        0-5%         {Verdict(earlyRate <= 5.0)}");
    Console.WriteLine($"  {$"  in waves {earlyThrough + 1}-{waveCount}",-22} {lateRate,6:F1}%        15-30%       {Verdict(lateRate is >= 15.0 and <= 30.0)}");
    // The SPREAD matters as much as the mean. A map where every run ends with
    // the same lives has no difficulty curve, only a threshold: when the mean
    // crosses zero, every run crosses at once. That is what a cliff IS, and the
    // mean alone cannot show it (gauntlet-cliff).
    double livesMean = finalLives.Average();
    double livesSd = System.Math.Sqrt(finalLives.Sum(v => (v - livesMean) * (v - livesMean)) / runs);
    Console.WriteLine($"  {"lives left (avg)",-22} {livesMean,6:F1}   " +
                      $"sd {livesSd:F1}, range {finalLives.Min()}-{finalLives.Max()}");
    if (lostAtWave.Count > 0)
    {
        Console.WriteLine($"  {"lost runs died at wave",-22} {lostAtWave.Average(),6:F1} avg " +
                          $"(earliest {lostAtWave.Min()}, latest {lostAtWave.Max()})");

        // The distribution, not just its ends. A mean of 7 means nothing on its
        // own: it is a curve if deaths are spread and a coin flip if half die at
        // wave 3 and half at wave 11. Only this line tells the two apart, and the
        // difference is the whole of route-variance-metric.
        var histogram = new int[waveCount + 1];
        foreach (int w in lostAtWave)
            if (w >= 1 && w <= waveCount) histogram[w]++;

        var bars = new System.Text.StringBuilder();
        for (int w = 1; w <= waveCount; w++)
            if (histogram[w] > 0)
                bars.Append($" w{w}:{100.0 * histogram[w] / runs:F0}%");

        Console.WriteLine($"  {"  by wave",-22}{bars}");
        Console.WriteLine($"  {"  waves that can kill",-22} {histogram.Count(c => c > 0),6} of {waveCount}");
    }
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
        // Let an armed prep timer run out instead of calling the wave early.
        // The policy is a reasonable beginner, and a beginner uses the build
        // window. Calling immediately made the timer invisible to the sim while
        // still charging the mid-wave premium -- 81% runs lost, all artifact.
        if (!sim.State.WaveActive && sim.State.CreepCount == 0 && sim.State.PrepTicksRemaining == 0)
            sim.Enqueue(new StartWaveCommand());
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

/// <summary>
/// The cadence sheet for a wave table: what spawns, when, and what shape that
/// makes.
///
/// Waves are authored as groups with a delay and a spacing, and the SHAPE that
/// produces is invisible in the JSON -- crossroads wave 12 fires 92 of its
/// spawns in the first quarter and 8 in the last, which nobody chose and nobody
/// could see. This prints it, so a wave can be composed and then read back.
/// </summary>
int WaveReport()
{
    string root = ContentFiles.FindRepoRoot();
    string? only = Opt("map");

    foreach (string file in Directory.GetFiles(Path.Combine(root, "content-data", "waves"), "*.json")
                                     .OrderBy(f => f, StringComparer.Ordinal))
    {
        string mapId = Path.GetFileNameWithoutExtension(file);
        if (only is not null && mapId != only) continue;

        ContentSet content = ContentFiles.LoadContent(root, mapId);
        Console.WriteLine($"\n{mapId} -- {content.Waves.Length} waves\n");
        Console.WriteLine($"{"wave",-5} {"secs",-6} {"spawns",-7} {"cadence",-31} {"Q1:Q4",-8} shape");

        foreach (WaveDef wave in content.Waves)
        {
            var times = new List<int>();
            foreach (WaveEntry e in wave.Entries)
            {
                int at = e.DelayTicks;
                for (int i = 0; i < e.Count; i++) { times.Add(at); at += e.SpacingTicks; }
            }
            if (times.Count == 0) continue;

            int end = times.Max() + 1;

            // Drawn, not tabulated. Reading a shape off a row of numbers is
            // precisely the thing nobody does, which is how every wave came to
            // fade out without anyone noticing.
            const int Cols = 30;
            var bucket = new int[Cols];
            foreach (int at in times) bucket[Math.Min(Cols - 1, at * Cols / end)]++;
            int peak = bucket.Max();
            string bars = string.Concat(bucket.Select(b =>
                " .:-=+*#"[peak == 0 ? 0 : Math.Min(7, b * 7 / peak)]));

            var q = new int[4];
            foreach (int at in times) q[Math.Min(3, at * 4 / end)]++;

            string ratio = q[3] == 0 ? $"{q[0]}:0" : $"{(double)q[0] / q[3]:F1}:1";
            string shape = q[0] > 2 * q[3] ? "FRONT-LOADED" : q[3] > q[0] ? "crescendo" : "even";

            Console.WriteLine($"{wave.Index,-5} {end / 30.0,-6:F1} {times.Count,-7} {bars,-31} {ratio,-8} {shape}");
        }
    }

    Console.WriteLine("\nQ1:Q4 is first quarter vs last. A wave should not end on its lightest.");
    return 0;
}

int MapReport()
{
    Console.WriteLine($"{"map",-14} {"size",-8} {"buildable",-11} {"path",-6} {"vs floor",-9} {"cover",-6} {"useful",-7} {"maze",-6} {"per route",-10} {"spawns",-7} verdict");
    bool anyErrors = false;
    foreach (string mapId in ContentFiles.MapIds(root))
    {
        MapDef map;
        try { map = ContentFiles.LoadMap(root, mapId); }
        catch (ContentException ex)
        {
            // A map that will not load is the worst verdict this verb can reach,
            // so it has to fail the run too -- it used to print ERROR and exit 0.
            Console.WriteLine($"{mapId,-14} ERROR: {ex.Message}");
            anyErrors = true;
            continue;
        }

        int cells = map.Width * map.Height;
        int buildable = map.Cells.Count(c => c == CellKind.Buildable);
        int pct = 100 * buildable / cells;

        var path = new Gridfall.Core.Path.PathSystem(map);
        path.ForceRebuild();
        int shortest = path.DistanceAt(map.Spawns[0]);

        // The game's verdict first, verbatim. This report used to re-implement
        // the buildable band, the path/floor split and the lane cap -- three
        // rules kept in two places -- and it did not carry MapValidator's
        // stranded-cell check at all. So it printed a clean sheet for spiral,
        // stepwell and driftway while all three were warning in the editor, and
        // an entire level set was signed off on the strength of it.
        //
        // Everything below this line is analysis the validator does not do
        // (cover, useful, maze, density). Anything that is a *rule* belongs in
        // MapValidator, where the loader and the editor will see it too.
        var findings = Gridfall.Core.Content.MapValidator.Validate(
            Gridfall.Core.Content.MapDraft.From(map));
        bool broken = Gridfall.Core.Content.MapValidator.HasErrors(findings);
        anyErrors |= broken;

        var warnings = findings
            .Where(f => f.Severity is MapSeverity.Error or MapSeverity.Warning)
            .Select(f => f.Severity == MapSeverity.Error ? $"ERROR: {f}" : f.ToString())
            .ToList();

        int floor = Gridfall.Core.Content.MapValidator.GeometricFloor(map.Spawns[0], map.Goal);

        // Route cells one tower can cover, from its best buildable cell.
        //
        // Range is fixed in cells while boards are not, so the same tower covers
        // a shrinking fraction of the route as a board grows. Buildable-per-route
        // measures how much defence a map PERMITS; this measures how much one
        // tower BUYS, which is the number wave design actually depends on.
        ContentSet content = ContentFiles.LoadContent(root, mapId);
        TowerDef cheapest = content.Towers.OrderBy(x => x.Cost).First();
        double range = cheapest.Range.ToFloat();

        // The ACTUAL route, walked from the spawn down the distance field.
        //
        // This was every walkable cell, which made both `cover` and `useful`
        // measure coverage of the whole board instead of the path -- `useful`
        // read 100% everywhere, which is what exposed it. Numbers published
        // before 2026-08-08 from the `cover` column were measuring the wrong set.
        var route = new List<(int X, int Y)>();
        {
            int at = map.Index(map.Spawns[0]);
            var seen = new HashSet<int>();
            while (at >= 0 && seen.Add(at))
            {
                route.Add((at % map.Width, at / map.Width));
                if (at == map.Index(map.Goal)) break;

                int here = path.DistanceAt(at), next = -1;
                foreach ((int dx, int dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
                {
                    int nx = at % map.Width + dx, ny = at / map.Width + dy;
                    if (nx < 0 || ny < 0 || nx >= map.Width || ny >= map.Height) continue;
                    int ni = map.Index(nx, ny);
                    int d = path.DistanceAt(ni);
                    if (d != Gridfall.Core.Path.PathSystem.NoDistance && d < here) { next = ni; break; }
                }
                at = next;
            }
        }

        int bestCover = 0;
        for (int i = 0; i < map.Cells.Length; i++)
        {
            if (map.Cells[i] != CellKind.Buildable) continue;
            int tx = i % map.Width, ty = i / map.Width, covered = 0;
            foreach ((int rx, int ry) in route)
            {
                double dx = rx - tx, dy = ry - ty;
                if (dx * dx + dy * dy <= range * range) covered++;
            }
            if (covered > bestCover) bestCover = covered;
        }

        string cover = shortest > 0 ? $"{100.0 * bestCover / route.Count:F0}%" : "?";

        // Share of buildable cells that are within range of the route at all.
        //
        // `cover` measures the BEST cell; this measures how many cells are worth
        // anything. A map can pass every band and still be unwinnable if its
        // buildable area sits away from the route -- `spiral` is 52% buildable,
        // inside the band, and 89 of its cells are a courtyard the creeps never
        // come near. It lost 100% of 150 runs.
        //
        // A viability floor, NOT a difficulty predictor: across twelve maps the
        // middle of this range does not order by outcome. Only the bottom does.
        int reachableBuild = 0, usefulBuild = 0;
        for (int i = 0; i < map.Cells.Length; i++)
        {
            if (map.Cells[i] != CellKind.Buildable) continue;
            reachableBuild++;
            int bx = i % map.Width, by = i / map.Width;
            foreach ((int rx, int ry) in route)
            {
                double dx = rx - bx, dy = ry - by;
                if (dx * dx + dy * dy <= range * range) { usefulBuild++; break; }
            }
        }
        int usefulPct = reachableBuild > 0 ? 100 * usefulBuild / reachableBuild : 0;
        if (usefulPct < 50)
            warnings.Add($"only {usefulPct}% of buildable is within range of the route -- "
                         + "the rest is dead space and the map may be unwinnable");

        // How much the route can be lengthened by legal building -- the greedy
        // lower bound the board editor's F6 uses.
        //
        // Reported, NOT warned on. It was tried as the route-variance metric and
        // it does not work: gauntlet measures 1.0x and crossroads 1.1x, yet their
        // outcome spread over 150 runs is sd 0.0 against sd 7.1. A threshold that
        // separates them does not exist here, and one set at 1.15x flagged nine of
        // twelve maps including a known-good one. See example-levels.md.
        int mazed = Gridfall.Core.Content.MapValidator.EstimateMaxMazedPath(map);
        string maze = Gridfall.Core.Content.MapValidator.Tenths(mazed, shortest) + "x";

        // Buildable cells per route cell. The buildable-percentage band does not
        // capture this, and crossroads passes that band at 4.0 here -- roughly
        // three towers per cell of route, which no enemy design survives.
        // Proposed band 1.5-2.0; reported, not yet enforced (map-density-target).
        double density = shortest > 0 ? buildable / (double)shortest : 0;
        if (density > 2.0)
            warnings.Add($"density {density:F1} buildable/route cell (proposed max 2.0)");

        string verdict = warnings.Count == 0 ? "ok" : string.Join("; ", warnings);
        string lengthening = Gridfall.Core.Content.MapValidator.Tenths(shortest, floor) + "x";
        Console.WriteLine($"{mapId,-14} {map.Width + "x" + map.Height,-8} {pct + "%",-11} {shortest,-6} {lengthening,-9} {cover,-6} {usefulPct + "%",-7} {maze,-6} {density,-10:F1} {map.Spawns.Length,-7} {verdict}");
    }

    // Errors fail the run; warnings do not. Every map in the repo carries the
    // density warning today and it is explicitly not enforced yet, so failing on
    // warnings would make the verb useless as a gate on the day it was added.
    if (anyErrors) Console.WriteLine("\nAt least one map has an ERROR: the game will refuse to load it.");
    return anyErrors ? 1 : 0;
}
