namespace Gridfall.Core;

/// <summary>
/// Player intent. Enqueue always succeeds; whether the thing happens is decided
/// in phase 1 of the next tick and reported as an event (engine guide 05).
///
/// A command is recorded in the trace as (tick, command), which together with
/// the map and the seed is the entire input to a run. That is what makes replay
/// exact.
/// </summary>
public interface ICommand
{
    CommandKind Kind { get; }
}

public enum CommandKind : byte
{
    Build = 1,
    Sell = 2,
    StartWave = 3,
}

public readonly struct BuildCommand : ICommand
{
    public readonly GridCell Cell;
    public readonly ushort TowerDefIndex;

    public BuildCommand(GridCell cell, ushort towerDefIndex)
    {
        Cell = cell;
        TowerDefIndex = towerDefIndex;
    }

    public CommandKind Kind => CommandKind.Build;
}

public readonly struct SellCommand : ICommand
{
    public readonly int TowerId;
    public SellCommand(int towerId) => TowerId = towerId;
    public CommandKind Kind => CommandKind.Sell;
}

public readonly struct StartWaveCommand : ICommand
{
    public CommandKind Kind => CommandKind.StartWave;
}

/// <summary>
/// Boxing-free command queue. Commands are drained front to back in insertion
/// order -- deterministic by construction.
/// </summary>
public sealed class CommandQueue
{
    public struct Entry
    {
        public CommandKind Kind;
        public GridCell Cell;
        public ushort TowerDefIndex;
        public int TowerId;
    }

    private Entry[] _entries = new Entry[64];
    private int _count;

    public int Count => _count;
    public ref Entry this[int i] => ref _entries[i];

    public void Enqueue(ICommand command)
    {
        Entry e = default;
        switch (command)
        {
            case BuildCommand b:
                e.Kind = CommandKind.Build;
                e.Cell = b.Cell;
                e.TowerDefIndex = b.TowerDefIndex;
                break;
            case SellCommand s:
                e.Kind = CommandKind.Sell;
                e.TowerId = s.TowerId;
                break;
            case StartWaveCommand:
                e.Kind = CommandKind.StartWave;
                break;
            default:
                throw new ArgumentException($"Unknown command type {command.GetType().Name}");
        }

        if (_count == _entries.Length) Array.Resize(ref _entries, _entries.Length * 2);
        _entries[_count++] = e;
    }

    public void Clear() => _count = 0;
}
