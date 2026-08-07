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

        _state.Gold = map.StartingGold;
        _state.Lives = map.StartingLives;
        _path.ForceRebuild();
    }

    public int TickCount { get; private set; }

    /// <summary>
    /// Read-only. The renderer gets this and cannot write through it -- see
    /// SimStateView and ADR-0001.
    /// </summary>
    public SimStateView State => new(_state);

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

        CommandSystem.Run(_state, _queue, _map, _content, _path, _events, TickCount);   // 1
        if (_path.RecomputeIfDirty())                                                   // 2
            _events.Add(new SimEvent(TickCount, EventKind.PathRecomputed, _path.Version));
        SpawnSystem.Run(_state, _map, _content, _path, _events, TickCount);             // 3
        MovementSystem.Run(_state, _map, _content, _path, _events, TickCount,
            _leakedCreepIds);                                                           // 4
        TargetingSystem.Run(_state, _map, _content, _path, _events, TickCount);         // 5
        ProjectileSystem.Run(_state, _map, _pending);                                   // 6
        DamageSystem.Run(_state, _pending, _events, TickCount, _leakedCreepIds,
            _scratchDeadIds, _deadDefIndices, _leakedDefIndices);                       // 7
        EconomySystem.Run(_state, _content, _events, TickCount,
            _deadDefIndices, _leakedDefIndices);                                        // 8

        FinalizeTick();                                                                   // 9
    }

    private void FinalizeTick()
    {
        if (_state.WaveActive && SpawnSystem.WaveComplete(_state, _content))
        {
            _state.WaveActive = false;
            _events.Add(new SimEvent(TickCount, EventKind.WaveCleared, _state.WaveIndex));
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
