namespace Gridfall.Core.Events;

public enum EventKind : byte
{
    None = 0,
    WaveStarted,
    CreepSpawned,
    PathRecomputed,
    BuildPlaced,
    BuildRejected,
    TowerSold,
    TowerUpgraded,
    UpgradeRejected,
    TowerFired,
    CreepDamaged,
    CreepDied,
    CreepLeaked,
    CreepStranded,
    GoldChanged,
    LivesChanged,
    CapacityExceeded,
    WaveCleared,
    GameOver,
}

public enum RejectReason : byte
{
    None = 0,
    NotBuildable = 1,
    OutOfBounds = 2,
    Occupied = 3,
    InsufficientGold = 4,
    WouldSealLane = 5,
    UnknownTower = 6,
    CapacityExceeded = 7,
    AlreadyMaxLevel = 8,
    NoSuchTower = 9,
}

/// <summary>
/// A fact the view can react to. Flat struct: no allocation, no virtual dispatch,
/// and the log is a contiguous array the renderer walks once.
///
/// Emit facts, not instructions. CreepDied(id), never PlayDeathAnimation(id) --
/// Core does not know animations exist (engine guide 05).
/// </summary>
public readonly struct SimEvent
{
    public readonly int Tick;
    public readonly EventKind Kind;
    public readonly int A;
    public readonly int B;
    public readonly GridCell Cell;

    public SimEvent(int tick, EventKind kind, int a = 0, int b = 0, GridCell cell = default)
    {
        Tick = tick;
        Kind = kind;
        A = a;
        B = b;
        Cell = cell;
    }

    public override string ToString() => $"[{Tick}] {Kind}(a={A}, b={B}, cell={Cell})";
}

/// <summary>
/// Ordered, tick-stamped, cleared every tick in phase 9. An event not consumed
/// is gone -- the renderer must not rely on catching up later.
///
/// The log is output, not state: it is not hashed. Two runs that produce the
/// same state must produce the same events, and there is a test for that.
/// </summary>
public sealed class EventLog
{
    private SimEvent[] _events;
    private int _count;

    public EventLog(int capacity = 1024) => _events = new SimEvent[capacity];

    public int Count => _count;

    public SimEvent this[int i] => _events[i];

    public ReadOnlySpan<SimEvent> Span => _events.AsSpan(0, _count);

    public void Add(in SimEvent e)
    {
        if (_count == _events.Length)
        {
            // Grows once past the initial capacity and then stays grown, so the
            // steady-state tick loop still allocates nothing.
            Array.Resize(ref _events, _events.Length * 2);
        }
        _events[_count++] = e;
    }

    public void Clear() => _count = 0;
}
