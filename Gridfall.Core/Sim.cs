using Gridfall.Core.Content;
using Gridfall.Core.Events;
using Gridfall.Core.Math;
using Gridfall.Core.Path;
using Gridfall.Core.Systems;

namespace Gridfall.Core;

/// <summary>
/// The simulation. A pure function of its inputs: feed it a map, a seed, and a
/// list of commands, and it produces the same game every time, on any machine.
///
/// It cannot see the renderer and does not know time exists -- it advances only
/// when someone calls Tick(). In the game that caller is a fixed-timestep
/// accumulator; in the harness it is a for loop. Both produce identical games.
/// </summary>
public sealed class Sim
{
    public const int TickMs = 33;
    public const int TicksPerSecond = 30;

    private readonly MapDef _map;
    private readonly ContentSet _content;
    private readonly SimState _state;
    private readonly PathSystem _path;
    private readonly SimRandom _random;
    private readonly CommandQueue _queue = new();
    private readonly EventLog _events = new();
    private readonly DamageBuffer _pending = new();
    private readonly DamageBuffer _pendingTowerDamage = new();

    // Reused across ticks so the steady-state loop allocates nothing.
    private readonly List<int> _leakedCreepIds = new(64);
    private readonly List<int> _scratchDeadIds = new(64);
    private readonly List<int> _deadDefIndices = new(64);
    private readonly List<int> _leakedDefIndices = new(64);

    public Sim(MapDef map, ContentSet content, uint seed)
    {
        _map = map;
        _content = content;
        _state = new SimState();
        _path = new PathSystem(map);
        _random = new SimRandom(seed);

        // The map file and the tower files are loaded independently, so this is
        // the first moment both are in hand -- and the last moment a typo in a
        // roster is cheap. Left unchecked it would present as a tower that is
        // missing from the toolbar for no stated reason, on one board.
        foreach (string towerId in map.TowerIds)
            if (!content.Towers.Any(t => t.Id == towerId))
                throw new Content.ContentException(
                    $"map '{map.Id}' offers tower '{towerId}', which does not exist. "
                    + $"Known: {string.Join(", ", content.Towers.Select(t => t.Id))}");

        // The window before wave 1 counts too -- 300 gold and nowhere to spend
        // it under time pressure is the first decision of the run.
        ArmPrepTimer();

        _state.Gold = map.StartingGold;
        _state.Lives = map.StartingLives;
        // No rebuild here: the PathSystem constructor builds its own field. Calling
        // ForceRebuild as well bumps Version to 2 at tick 0, and Version is hashed,
        // so every hash in every recorded trace shifts. The harness caught exactly
        // that when the constructor changed.
    }

    public int TickCount { get; private set; }

    /// <summary>
    /// Read-only. The renderer gets this and cannot write through it -- see
    /// SimStateView and ADR-0001.
    /// </summary>
    public SimStateView State => new(_state);

    /// <summary>
    /// A tower's current price, mid-wave premium included. Read-only: the view
    /// needs the number to display and must not re-derive it.
    /// </summary>
    public int BuildCostOf(ushort towerIndex)
        => Systems.CommandSystem.BuildCost(_content.Tower(towerIndex), _state, _content);

    /// <summary>
    /// The mutable state, for first-party tooling only: the test suite proving
    /// hash coverage, and the perf harness setting up a board. Not visible to
    /// the Godot project, which is the point.
    /// </summary>
    internal SimState MutableState => _state;

    public EventLog Events => _events;
    public MapDef Map => _map;
    public ContentSet Content => _content;
    public PathSystem Path => _path;
    public SimRandom Random => _random;

    /// <summary>Player intent. Always succeeds; the outcome is decided in phase 1.</summary>
    public void Enqueue(ICommand command) => _queue.Enqueue(command);

    public ulong Hash() => _state.Hash(TickCount, _random, _path);

    /// <summary>Advance exactly one 33 ms step. Nine phases, always in this order.</summary>
    public void Tick()
    {
        _events.Clear();

        CommandSystem.Run(_state, _queue, _map, _content, _path, _events, TickCount, _random);   // 1
        if (_path.RecomputeIfDirty())                                                   // 2
            _events.Add(new SimEvent(TickCount, EventKind.PathRecomputed, _path.Version));
        SpawnSystem.Run(_state, _map, _content, _path, _events, TickCount);             // 3
        MovementSystem.Run(_state, _map, _content, _path, _events, TickCount,
            _leakedCreepIds);                                                           // 4
        // Phase 5 has two participants. Towers fire FIRST, so a tower destroyed
        // this tick still gets its shot off -- fairer, and easier to reason
        // about than the reverse. Order is fixed and load-bearing (ADR-0006).
        TargetingSystem.Run(_state, _map, _content, _path, _events, TickCount);         // 5a
        EnemyAttackSystem.Run(_state, _map, _content, _pendingTowerDamage);             // 5b
        ProjectileSystem.Run(_state, _map, _pending);                                   // 6
        DamageSystem.Run(_state, _content, _pending, _pendingTowerDamage, _path,
            _events, TickCount, _leakedCreepIds,
            _scratchDeadIds, _deadDefIndices, _leakedDefIndices);                       // 7
        EconomySystem.Run(_state, _content, _events, TickCount,
            _deadDefIndices, _leakedDefIndices);                                        // 8

        FinalizeTick();                                                                   // 9
    }

    /// <summary>
    /// Start the build window for the next wave, if that wave asks for one.
    ///
    /// Read off the NEXT wave rather than the one just cleared: the prep window
    /// belongs to what is coming, so a table can give a long breather before its
    /// finale without lengthening every gap before it.
    /// </summary>
    private void ArmPrepTimer()
    {
        if (_state.WaveIndex >= _content.Waves.Length) return;
        _state.PrepTicksRemaining = _content.Waves[_state.WaveIndex].PrepTicks;
    }

    private void FinalizeTick()
    {
        if (_state.WaveActive && SpawnSystem.WaveComplete(_state, _content))
        {
            _state.WaveActive = false;
            _events.Add(new SimEvent(TickCount, EventKind.WaveCleared, _state.WaveIndex));

            // Clearing the last wave alive is the win. Detected on the same
            // transition rather than stored, so no new hashed state and no
            // determinism trace to re-record.
            // Paid before the prep timer is armed, so the build window opens with
            // the money in hand -- that ordering IS the feature.
            WaveDef cleared = _content.Waves[_state.WaveIndex - 1];
            if (cleared.ClearGold > 0)
            {
                _state.Gold += cleared.ClearGold;
                _events.Add(new SimEvent(TickCount, EventKind.GoldChanged, _state.Gold, cleared.ClearGold));
            }

            if (_state.WaveIndex >= _content.Waves.Length && _state.Lives > 0)
                _events.Add(new SimEvent(TickCount, EventKind.RunComplete, _state.WaveIndex));
            else
                ArmPrepTimer();
        }

        // The build window counts down only between waves, and starts the next
        // one itself when it runs out. A wave that begins on its own is what
        // makes the gap a resource rather than an intermission.
        if (!_state.WaveActive && _state.PrepTicksRemaining > 0)
        {
            _state.PrepTicksRemaining--;
            if (_state.PrepTicksRemaining == 0) _queue.Enqueue(new StartWaveCommand());
        }

        TickCount++;
    }

    // ---- snapshot ---------------------------------------------------------

    public SimSnapshot Snapshot()
    {
        var copy = new SimState();
        _state.CopyTo(copy);
        return new SimSnapshot(copy, _random.Clone(), TickCount, _path.Version, _path.CostSpan.ToArray());
    }

    /// <summary>
    /// Restore then N ticks must match N ticks without the round trip, hash for
    /// hash. There is a test for that, and it is the fastest way to discover a
    /// field you forgot to snapshot and also forgot to hash.
    /// </summary>
    public void Restore(SimSnapshot snapshot)
    {
        snapshot.State.CopyTo(_state);
        _random.CopyFrom(snapshot.Random);
        TickCount = snapshot.TickCount;
        _path.RestoreFrom(snapshot.Cost, snapshot.PathVersion);
        _events.Clear();
    }
}

public sealed class SimSnapshot
{
    public SimSnapshot(SimState state, SimRandom random, int tickCount, ushort pathVersion, byte[] cost)
    {
        State = state;
        Random = random;
        TickCount = tickCount;
        PathVersion = pathVersion;
        Cost = cost;
    }

    public SimState State { get; }
    public SimRandom Random { get; }
    public int TickCount { get; }
    public ushort PathVersion { get; }
    public byte[] Cost { get; }
}
