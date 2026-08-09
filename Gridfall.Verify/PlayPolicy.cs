using Gridfall.Core;
using Gridfall.Core.Content;
using Gridfall.Core.Math;
using Gridfall.Core.Path;

namespace Gridfall.Verify;

/// <summary>
/// A scripted player, so the balance sim measures a game somebody is playing
/// rather than an undefended board.
///
/// "Competent" needs an operational definition or the numbers mean nothing.
/// This one is:
///
///   1. Build whenever gold allows, keeping no reserve -- an idle pile of gold
///      is the thing the economy targets are trying to detect. The one exception
///      is waiting out the price of the station it has decided it wants, which
///      is bounded by one wave (see BestAffordableStation).
///   2. Place where a station covers the most cells of the CURRENT route, because
///      coverage is what a human eyeballs. Never place where the game would
///      refuse.
///   3. Buy the best serving-per-gold that is affordable right now, measured
///      against the visitors it has ALREADY MET rather than against base stats,
///      with no lookahead and no saving up for something better. Fussiness is
///      subtracted per hit, so "best" is not a fixed answer: the arrow station
///      wins by 35% against an unarmoured wave and loses by 78% against husks.
///      Ranking on base serving is why no measured run ever built a cannon.
///   4. When there is nowhere useful left to build, upgrade the station covering
///      the most of the route instead. Board saturation is exactly the moment
///      upgrades are for.
///   5. Between waves, repair a station that is visibly about to die BEFORE
///      building a new one. Losing a built station costs more than not having
///      another. A beginner notices a station gone dark red; they do not
///      micro-manage scratches, so this triggers on a threshold rather than on
///      any serving at all.
///   6. Spend down BEFORE pulling the next wave, not during it. A real player
///      with 200 gold in hand does not start wave 1 with one station.
///   7. Never sell, never re-maze deliberately.
///
/// That is a reasonable beginner who understands coverage: clearly better than
/// nothing, clearly worse than a good player. Balance numbers from it are a
/// floor on difficulty, not a verdict -- read them as "even played this way,
/// wave 12 leaks", never as "wave 12 is correctly tuned".
///
/// Deterministic given its seed. The jitter below varies the PLAYER across
/// runs, not the game, which is what makes N runs mean something while nothing
/// in the simulation itself consumes randomness.
/// </summary>
public sealed class PlayPolicy
{
    /// <summary>Ticks between build attempts. A build needs a tick to apply.</summary>
    private const int BuildCooldownTicks = 3;

    /// <summary>Pick uniformly among this many best placements, so runs differ.</summary>
    private const int JitterTopN = 3;

    /// <summary>
    /// Repair a station once it drops below this percentage of full health.
    ///
    /// Not 100: repairing every scratch is micro no beginner does, and because
    /// cost scales with serving taken it would also be indistinguishable from a
    /// continuous drain. 40% is roughly where the placeholder's darkening reads
    /// as "this one is in trouble" rather than "this one has been shot at".
    /// </summary>
    private const int RepairBelowPercent = 40;

    /// <summary>
    /// Sell a station mid-wave once it drops below this. A station this hurt is
    /// probably not surviving the wave, and a destroyed station refunds nothing.
    /// </summary>
    private const int SalvageBelowPercent = 25;

    /// <summary>
    /// Whether the player cuts doomed stations loose mid-wave. Set from the balance
    /// runner: the beginner policy never sells, so without this the sim cannot
    /// see the behaviour `salvage-value` exists to price.
    /// </summary>
    public static bool Salvages;

    /// <summary>
    /// Total gold this player may ever commit, or <c>int.MaxValue</c> for no cap.
    ///
    /// **This is the inverted mode's difficulty dial, measured early.** When the
    /// human is the attacker, the defence is an opponent rather than a
    /// measurement, and an opponent that plays a full economy wins essentially
    /// always -- the shipped corpus says an attacker delivers 0.0-1.6% of its
    /// visitors. Capping what the defence may spend is the one lever that is
    /// **mode-local by construction**: it changes no station, no visitor, no wave
    /// and no map, so normal mode cannot move when it is turned.
    ///
    /// Counted as gold COMMITTED, not gold deducted. A queued command the sim
    /// then refuses still counts against the cap, because the alternative is a
    /// policy that discovers its budget by trying -- and a refused build already
    /// costs a real player the same tick either way.
    /// </summary>
    public static int SpendCap = int.MaxValue;

    /// <summary>
    /// Gold this player may commit **per wave**, or <c>int.MaxValue</c> for no
    /// limit. Refills when a wave starts.
    ///
    /// The other shape of the same dial: a RATE rather than a total.
    ///
    /// Preferred on principle, not on evidence. A rate does not depend on how
    /// long a run is, so the same number means the same thing on a 12-wave table
    /// and a 20-wave one, and a defence cannot exhaust itself and simply fade.
    /// **But measurement did not show it dominating** -- on `crossroads` in the
    /// band that matters the two produce comparable spreads (8 of 12 waves able
    /// to end a run, either way).
    ///
    /// The collapse a lifetime cap *can* cause is real and is an artefact of
    /// setting it far below the run's natural spend: at 2000g on `comb`, 100% of
    /// runs end on wave 5 because the allowance is gone by then. Natural spend
    /// on these boards is 15,000-20,000g, so that is a badly chosen number
    /// rather than a badly shaped knob.
    /// </summary>
    public static int SpendPerWave = int.MaxValue;


    /// <summary>
    /// Cap on seal checks per build attempt. Each one is a BFS, and on a
    /// saturated board almost every candidate fails -- without a cap the policy
    /// would spend a full board scan of BFS calls every third tick.
    /// </summary>
    private const int MaxSealChecksPerAttempt = 40;

    private readonly Sim _sim;
    private readonly SimRandom _rng;
    private readonly VisitorCensus _census;
    private readonly int[] _routeBuffer = new int[4096];
    private readonly List<(int cell, int score)> _candidates = new();

    private int _nextBuildTick;
    private int _pendingRepairCost;

    public PlayPolicy(Sim sim, uint seed)
    {
        _sim = sim;
        _rng = new SimRandom(seed);
        _census = new VisitorCensus(sim.Content);
        _boughtByDef = new int[sim.Content.Stations.Length];
    }

    public int BuildsPlaced { get; private set; }
    public int BuildsRefused { get; private set; }

    private readonly int[] _boughtByDef;

    /// <summary>
    /// Builds queued per station def. Reported because "the policy never bought a
    /// cannon" was true for the entire history of this harness and no number in
    /// any report said so -- an unexercised half of the roster is invisible in
    /// leak rate, and it silently made every balance figure a one-station
    /// measurement.
    /// </summary>
    public int BoughtOf(int defIndex) => _boughtByDef[defIndex];

    /// <summary>Gold committed so far, against <see cref="SpendCap"/>.</summary>
    public int GoldCommitted { get; private set; }

    /// <summary>
    /// Whether this purchase fits inside the cap, and books it if so.
    ///
    /// One place, called by every spend. Three call sites each doing their own
    /// arithmetic is how a budget ends up enforced on builds and quietly ignored
    /// on upgrades.
    /// </summary>
    private bool Afford(int cost)
    {
        if (GoldCommitted + (long)cost > SpendCap) return false;
        if (_committedThisWave + (long)cost > SpendPerWave) return false;
        GoldCommitted += cost;
        _committedThisWave += cost;
        return true;
    }

    private int _committedThisWave;
    private int _allowanceWave = -1;

    /// <summary>
    /// Attempts that found no cell worth building on. A high count means the
    /// policy has saturated the useful space and gold is piling up with nothing
    /// to buy -- which is an economy finding, not a policy bug.
    /// </summary>
    public int NoPlacementFound { get; private set; }

    public int UpgradesBought { get; private set; }

    public int RepairsBought { get; private set; }

    /// <summary>
    /// Gold sunk into repairs. Reported because repair is the first sink whose
    /// size the ENEMY sets rather than the player, so how big it gets is a
    /// finding in its own right, not just an input to the leak rate.
    /// </summary>
    public int GoldSpentRepairing { get; private set; }

    public int StationsSalvaged { get; private set; }

    /// <summary>
    /// Total route cells covered, summed over every standing station. Station COUNT
    /// turned out not to be the thing that matters -- a winding route lets one
    /// station's range reach several legs at once, so a map with a third of the
    /// stations can field the same defence.
    /// </summary>
    public int TotalCoverage()
    {
        SimStateView state = _sim.State;
        MapDef map = _sim.Map;
        int routeLength = _sim.Path.TraceRoute(map.Index(map.Spawns[0]), _routeBuffer);

        int total = 0;
        for (int k = 0; k < state.StationCount; k++)
        {
            int slot = state.StationSlotByOrder(k);
            StationDef def = _sim.Content.Station(state.StationDefIndex(slot));
            total += CoverageScore(state.StationCellIndex(slot), def, routeLength, map);
        }
        return total;
    }

    /// <summary>Call once per tick, before Sim.Tick().</summary>
    public void Update()
    {
        SimStateView state = _sim.State;

        // What the player has met so far. Folded in every tick because a wave can
        // start on any tick and the mid-wave build branch below is entitled to
        // know what is currently walking at it.
        _census.ObserveWavesStarted(state.WaveIndex);

        // The per-wave allowance refills when the wave number moves, which
        // includes the build window BEFORE wave 1 -- a defence that could not
        // build until the first visitor was walking would be measuring the
        // opening, not the allowance.
        if (state.WaveIndex != _allowanceWave)
        {
            _allowanceWave = state.WaveIndex;
            _committedThisWave = 0;
        }

        // Between waves: spend down first, one purchase per tick, and only pull
        // the next wave once the gold is committed.
        //
        // The previous version made a single build attempt and started the wave
        // immediately, so the policy entered wave 1 with ONE station while holding
        // 200 gold -- four stations' worth. That made the early game look like an
        // economy problem when it was the policy failing to spend.
        if (!state.WaveActive && state.VisitorCount == 0)
        {
            if (_sim.TickCount < _nextBuildTick) return;
            if (TryRepair()) return;
            if (TryBuild()) return;
            if (TryUpgrade()) return;

            _sim.Enqueue(new StartWaveCommand());
            return;
        }

        if (_sim.TickCount < _nextBuildTick) return;

        // No repair branch here: the sim refuses a repair while a wave is running,
        // so a policy that tried would only generate rejections. The decision this
        // mechanic creates patience entirely in the between-waves branch above.
        if (Salvages && TrySalvage()) return;
        TryBuild();
    }

    /// <summary>
    /// Cut loose the worst-hurt station mid-wave, before it dies for nothing.
    ///
    /// Mid-wave only: between waves the same station can be repaired, which is
    /// cheaper than selling and rebuilding. This models the player noticing that
    /// selling still works while a wave runs and repairing does not.
    /// </summary>
    /// <returns>True if a sale was queued this tick.</returns>
    private bool TrySalvage()
    {
        SimStateView state = _sim.State;

        int bestStationId = -1;
        long bestAppetite = 0, bestMax = 1;

        for (int k = 0; k < state.StationCount; k++)
        {
            int slot = state.StationSlotByOrder(k);
            StationDef def = _sim.Content.Station(state.StationDefIndex(slot));
            int hp = state.StationStock(slot);

            if (hp * 100L >= def.Stock * (long)SalvageBelowPercent) continue;
            if (bestStationId >= 0 && hp * bestMax >= bestAppetite * def.Stock) continue;

            bestStationId = state.StationId(slot);
            bestAppetite = hp;
            bestMax = def.Stock;
        }

        if (bestStationId < 0) return false;

        _nextBuildTick = _sim.TickCount + BuildCooldownTicks;
        _sim.Enqueue(new SellCommand(bestStationId));
        StationsSalvaged++;
        return true;
    }

    /// <summary>
    /// Repair the worst-hurt station that is below the threshold and affordable.
    ///
    /// Worst-hurt by health FRACTION, not by points missing: an 800-hp arrow
    /// station at 200 is in more danger than a 1440-hp cannon at 400, and the
    /// beginner being modelled is reading the colour, which tracks the fraction.
    /// </summary>
    /// <returns>True if a repair was queued this tick.</returns>
    private bool TryRepair()
    {
        SimStateView state = _sim.State;

        int bestStationId = -1;
        long bestAppetite = 0, bestMax = 1;   // compared as a fraction, cross-multiplied

        // Ascending station id, so ties resolve identically every run.
        for (int k = 0; k < state.StationCount; k++)
        {
            int slot = state.StationSlotByOrder(k);
            StationDef def = _sim.Content.Station(state.StationDefIndex(slot));
            int hp = state.StationStock(slot);

            if (hp * 100L >= def.Stock * (long)RepairBelowPercent) continue;

            int cost = def.RepairCostFor(state.StationLevel(slot), def.Stock - hp);
            if (state.Gold < cost) continue;

            // hp/def.Stock < bestAppetite/bestMax, without dividing.
            if (bestStationId >= 0 && hp * bestMax >= bestAppetite * def.Stock) continue;

            bestStationId = state.StationId(slot);
            bestAppetite = hp;
            bestMax = def.Stock;
            _pendingRepairCost = cost;
        }

        if (bestStationId < 0) return false;
        if (!Afford(_pendingRepairCost)) return false;

        _nextBuildTick = _sim.TickCount + BuildCooldownTicks;
        _sim.Enqueue(new RepairCommand(bestStationId));
        RepairsBought++;
        GoldSpentRepairing += _pendingRepairCost;
        return true;
    }

    /// <returns>True if a build was queued this tick.</returns>
    private bool TryBuild()
    {
        _nextBuildTick = _sim.TickCount + BuildCooldownTicks;

        ushort? choice = BestAffordableStation();
        if (choice is not { } stationIndex) return false;

        int cell = BestPlacement(_sim.Content.Station(stationIndex));
        if (cell < 0)
        {
            // Nowhere useful to build. Before upgrades existed the policy simply
            // stopped spending here and gold ran away.
            NoPlacementFound++;
            return TryUpgrade();
        }

        if (!Afford(_sim.Content.Station(stationIndex).Cost)) return false;

        _sim.Enqueue(new BuildCommand(
            new GridCell(cell % _sim.Map.Width, cell / _sim.Map.Width), stationIndex));
        BuildsPlaced++;
        _boughtByDef[stationIndex]++;
        return true;
    }

    /// <summary>
    /// Upgrade the highest-coverage station that can afford its next level.
    ///
    /// Deliberately only reached when building fails: the content is authored so
    /// upgrading costs more per point of serving than a new station, so a player who
    /// upgrades while good spots remain is playing badly.
    /// </summary>
    /// <returns>True if an upgrade was queued this tick.</returns>
    private bool TryUpgrade()
    {
        SimStateView state = _sim.State;
        MapDef map = _sim.Map;

        int routeLength = _sim.Path.TraceRoute(map.Index(map.Spawns[0]), _routeBuffer);
        if (routeLength == 0) return false;

        int bestStationId = -1;
        int bestScore = -1;
        int bestCost = 0;

        // Ascending station id, so ties resolve identically every run.
        for (int k = 0; k < state.StationCount; k++)
        {
            int slot = state.StationSlotByOrder(k);
            StationDef def = _sim.Content.Station(state.StationDefIndex(slot));
            int level = state.StationLevel(slot);
            if (level >= def.MaxLevel) continue;

            int cost = def.Upgrades[level - 1].Cost;
            if (state.Gold < cost) continue;

            int score = CoverageScore(state.StationCellIndex(slot), def, routeLength, map);
            if (score <= bestScore) continue;

            bestScore = score;
            bestStationId = state.StationId(slot);
            bestCost = cost;
        }

        if (bestStationId < 0) return false;
        if (!Afford(bestCost)) return false;

        _sim.Enqueue(new UpgradeCommand(bestStationId));
        UpgradesBought++;
        return true;
    }

    /// <summary>
    /// The best serving-per-gold on the board's roster, measured against the
    /// visitors already met -- or null if it is not affordable yet.
    ///
    /// Two things had to change together here, and either alone does nothing:
    ///
    /// **Value is census-relative.** Base serving ranks the arrow station first on
    /// every board and every wave, so the roster had a second station in it that
    /// the harness never bought, and every balance figure in the repo was measured
    /// on a one-station game.
    ///
    /// **The policy will not substitute down.** It used to buy the best station it
    /// could afford THIS TICK, which on any roster means the cheapest one is
    /// bought the instant its price is reached and gold never gets near the price
    /// of anything else. That is a structural block, not a preference: with a 50g
    /// station on the board, the 90g one is unreachable no matter what the census
    /// says. So when the best buy is out of reach the policy holds instead, and
    /// the wait is self-limiting -- holding makes <see cref="TryBuild"/> fail,
    /// which is what pulls the next wave, which is what pays for the station.
    ///
    /// The cost is that up to (price - 1) gold can sit across one wave. That is
    /// the "no reserve" rule bending, and it is bounded by one wave and one
    /// station's price; the idle-gold pathology the rule exists to detect is
    /// thousands of gold across a whole run.
    /// </summary>
    private ushort? BestAffordableStation()
    {
        ushort? best = null;
        long bestValue = 0;

        for (ushort i = 0; i < _sim.Content.Stations.Length; i++)
        {
            // The board's roster, from the same method CommandSystem enforces it
            // with. Skipping it here would not cheat -- the build would simply be
            // refused -- but the policy would burn its turn choosing a station it
            // cannot place, and the balance figures for a restricted board would
            // describe a player who never builds anything.
            if (!_sim.Map.Offers(_sim.Content, i)) continue;

            StationDef def = _sim.Content.Station(i);
            if (def.Serving <= 0 || def.CooldownTicks <= 0) continue;

            // Effective serving per tick per gold, scaled to stay in integers.
            long value = _census.ValuePerGold(def);
            if (best is not null && value <= bestValue) continue;

            bestValue = value;
            best = i;
        }

        if (best is null) return null;
        return _sim.Content.Station(best.Value).Cost <= _sim.State.Gold ? best : null;
    }

    /// <summary>
    /// The legal cell whose station would cover the most of the current route.
    ///
    /// Coverage counted in cells, not in time-in-range: a good enough proxy for
    /// what a human does by eye, and it does not need a serving model.
    /// </summary>
    private int BestPlacement(StationDef def)
    {
        MapDef map = _sim.Map;
        PathSystem path = _sim.Path;

        int routeLength = path.TraceRoute(map.Index(map.Spawns[0]), _routeBuffer);
        if (routeLength == 0) return -1;

        _candidates.Clear();
        int bestScore = 0;

        // Ascending cell index, so ties resolve the same way every run.
        for (int cell = 0; cell < map.Cells.Length; cell++)
        {
            if (map.Cells[cell] != CellKind.Buildable) continue;
            if (path.IsBlocked(cell)) continue;

            int score = CoverageScore(cell, def, routeLength, map);
            if (score == 0) continue;

            _candidates.Add((cell, score));
            if (score > bestScore) bestScore = score;
        }

        if (_candidates.Count == 0) return -1;

        // Top-N by score, then jitter among them. Sorting by score descending and
        // cell ascending keeps the candidate list itself deterministic.
        _candidates.Sort((a, b) => a.score != b.score ? b.score - a.score : a.cell - b.cell);

        // Jitter among the best few, so runs differ.
        int window = System.Math.Min(JitterTopN, _candidates.Count);
        for (int attempt = 0; attempt < window; attempt++)
        {
            int pick = _candidates[_rng.NextInt(window)].cell;

            // Ask the game, do not guess: a placement that would seal a lane is
            // refused, and the policy should not be the thing that discovers it.
            if (path.WouldRemainConnected(pick)) return pick;
            BuildsRefused++;
        }

        // Then settle. Trying only the top few and giving up made the policy stop
        // building at ~21 stations on crossroads while sitting on 3,000 gold -- not
        // because the board was full, but because its three favourite cells were
        // all chokepoints the game refuses. A real player takes a worse spot; so
        // does this now. Reporting that as an economy finding would have been
        // measuring the policy, not the game.
        int scanned = 0;
        foreach ((int cell, int _) in _candidates)
        {
            if (++scanned > MaxSealChecksPerAttempt) break;
            if (path.WouldRemainConnected(cell)) return cell;
            BuildsRefused++;
        }

        return -1;
    }

    private int CoverageScore(int cell, StationDef def, int routeLength, MapDef map)
    {
        var stationPos = new FixVec2(
            Fix32.FromInt(cell % map.Width),
            Fix32.FromInt(cell / map.Width));

        int covered = 0;
        for (int i = 0; i < routeLength; i++)
        {
            int routeCell = _routeBuffer[i];
            var pos = new FixVec2(
                Fix32.FromInt(routeCell % map.Width),
                Fix32.FromInt(routeCell / map.Width));

            if (FixVec2.DistanceSquared(stationPos, pos) <= def.RangeSquared) covered++;
        }
        return covered;
    }
}
