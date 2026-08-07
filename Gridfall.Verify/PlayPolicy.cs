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
///      is the thing the economy targets are trying to detect.
///   2. Place where a tower covers the most cells of the CURRENT route, because
///      coverage is what a human eyeballs. Never place where the game would
///      refuse.
///   3. Buy the best damage-per-gold that is affordable right now, with no
///      lookahead and no saving up for something better.
///   4. When there is nowhere useful left to build, upgrade the tower covering
///      the most of the route instead. Board saturation is exactly the moment
///      upgrades are for.
///   5. Spend down BEFORE pulling the next wave, not during it. A real player
///      with 200 gold in hand does not start wave 1 with one tower.
///   6. Never sell, never re-maze deliberately.
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
    /// Cap on seal checks per build attempt. Each one is a BFS, and on a
    /// saturated board almost every candidate fails -- without a cap the policy
    /// would spend a full board scan of BFS calls every third tick.
    /// </summary>
    private const int MaxSealChecksPerAttempt = 40;

    private readonly Sim _sim;
    private readonly SimRandom _rng;
    private readonly int[] _routeBuffer = new int[4096];
    private readonly List<(int cell, int score)> _candidates = new();

    private int _nextBuildTick;

    public PlayPolicy(Sim sim, uint seed)
    {
        _sim = sim;
        _rng = new SimRandom(seed);
    }

    public int BuildsPlaced { get; private set; }
    public int BuildsRefused { get; private set; }

    /// <summary>
    /// Attempts that found no cell worth building on. A high count means the
    /// policy has saturated the useful space and gold is piling up with nothing
    /// to buy -- which is an economy finding, not a policy bug.
    /// </summary>
    public int NoPlacementFound { get; private set; }

    public int UpgradesBought { get; private set; }

    /// <summary>Call once per tick, before Sim.Tick().</summary>
    public void Update()
    {
        SimStateView state = _sim.State;

        // Between waves: spend down first, one purchase per tick, and only pull
        // the next wave once the gold is committed.
        //
        // The previous version made a single build attempt and started the wave
        // immediately, so the policy entered wave 1 with ONE tower while holding
        // 200 gold -- four towers' worth. That made the early game look like an
        // economy problem when it was the policy failing to spend.
        if (!state.WaveActive && state.CreepCount == 0)
        {
            if (_sim.TickCount < _nextBuildTick) return;
            if (TryBuild()) return;
            if (TryUpgrade()) return;

            _sim.Enqueue(new StartWaveCommand());
            return;
        }

        if (_sim.TickCount < _nextBuildTick) return;
        TryBuild();
    }

    /// <returns>True if a build was queued this tick.</returns>
    private bool TryBuild()
    {
        _nextBuildTick = _sim.TickCount + BuildCooldownTicks;

        ushort? choice = BestAffordableTower();
        if (choice is not { } towerIndex) return false;

        int cell = BestPlacement(_sim.Content.Tower(towerIndex));
        if (cell < 0)
        {
            // Nowhere useful to build. Before upgrades existed the policy simply
            // stopped spending here and gold ran away.
            NoPlacementFound++;
            return TryUpgrade();
        }

        _sim.Enqueue(new BuildCommand(
            new GridCell(cell % _sim.Map.Width, cell / _sim.Map.Width), towerIndex));
        BuildsPlaced++;
        return true;
    }

    /// <summary>
    /// Upgrade the highest-coverage tower that can afford its next level.
    ///
    /// Deliberately only reached when building fails: the content is authored so
    /// upgrading costs more per point of damage than a new tower, so a player who
    /// upgrades while good spots remain is playing badly.
    /// </summary>
    /// <returns>True if an upgrade was queued this tick.</returns>
    private bool TryUpgrade()
    {
        SimStateView state = _sim.State;
        MapDef map = _sim.Map;

        int routeLength = _sim.Path.TraceRoute(map.Index(map.Spawns[0]), _routeBuffer);
        if (routeLength == 0) return false;

        int bestTowerId = -1;
        int bestScore = -1;

        // Ascending tower id, so ties resolve identically every run.
        for (int k = 0; k < state.TowerCount; k++)
        {
            int slot = state.TowerSlotByOrder(k);
            TowerDef def = _sim.Content.Tower(state.TowerDefIndex(slot));
            int level = state.TowerLevel(slot);
            if (level >= def.MaxLevel) continue;
            if (state.Gold < def.Upgrades[level - 1].Cost) continue;

            int score = CoverageScore(state.TowerCellIndex(slot), def, routeLength, map);
            if (score <= bestScore) continue;

            bestScore = score;
            bestTowerId = state.TowerId(slot);
        }

        if (bestTowerId < 0) return false;

        _sim.Enqueue(new UpgradeCommand(bestTowerId));
        UpgradesBought++;
        return true;
    }

    /// <summary>
    /// Best damage-per-gold that is affordable now. No lookahead: a policy that
    /// saves up would be a different, and much harder to justify, definition of
    /// competent.
    /// </summary>
    private ushort? BestAffordableTower()
    {
        int gold = _sim.State.Gold;
        ushort? best = null;
        long bestValue = 0;

        for (ushort i = 0; i < _sim.Content.Towers.Length; i++)
        {
            TowerDef def = _sim.Content.Tower(i);
            if (def.Cost > gold || def.Damage <= 0 || def.CooldownTicks <= 0) continue;

            // damage per tick per gold, scaled to stay in integers
            long value = (long)def.Damage * 1000 / (def.CooldownTicks * (long)def.Cost);
            if (best is not null && value <= bestValue) continue;

            bestValue = value;
            best = i;
        }
        return best;
    }

    /// <summary>
    /// The legal cell whose tower would cover the most of the current route.
    ///
    /// Coverage counted in cells, not in time-in-range: a good enough proxy for
    /// what a human does by eye, and it does not need a damage model.
    /// </summary>
    private int BestPlacement(TowerDef def)
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
        // building at ~21 towers on crossroads while sitting on 3,000 gold -- not
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

    private int CoverageScore(int cell, TowerDef def, int routeLength, MapDef map)
    {
        var towerPos = new FixVec2(
            Fix32.FromInt(cell % map.Width),
            Fix32.FromInt(cell / map.Width));

        int covered = 0;
        for (int i = 0; i < routeLength; i++)
        {
            int routeCell = _routeBuffer[i];
            var pos = new FixVec2(
                Fix32.FromInt(routeCell % map.Width),
                Fix32.FromInt(routeCell / map.Width));

            if (FixVec2.DistanceSquared(towerPos, pos) <= def.RangeSquared) covered++;
        }
        return covered;
    }
}
