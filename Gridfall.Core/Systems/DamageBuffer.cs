namespace Gridfall.Core.Systems;

public enum ServingSource : byte
{
    Projectile = 0,
    Burn = 1,
    VisitorAttack = 2,
}

/// <summary>
/// Serving is produced in phases 5-6 and applied in phase 7. Buffering is what
/// makes simultaneous kills deterministic: two stations that both land a killing
/// blow on the same tick produce one death and one bounty, regardless of which
/// station is evaluated first (engine guide 02).
/// </summary>
public sealed class ServingBuffer
{
    public struct Record
    {
        public int VisitorId;
        public int Amount;
        public ServingSource Source;
    }

    private Record[] _records = new Record[512];
    private int _count;

    public int Count => _count;
    public ref Record this[int i] => ref _records[i];

    public void Add(int visitorId, int amount, ServingSource source)
    {
        if (_count == _records.Length) Array.Resize(ref _records, _records.Length * 2);
        _records[_count].VisitorId = visitorId;
        _records[_count].Amount = amount;
        _records[_count].Source = source;
        _count++;
    }

    public void Clear() => _count = 0;

    /// <summary>
    /// Ascending visitor id, then insertion order within an id. Insertion sort:
    /// small n, no allocation, and stable, so equal ids keep producer order.
    /// </summary>
    public void SortByVisitorId()
    {
        for (int i = 1; i < _count; i++)
        {
            Record v = _records[i];
            int j = i - 1;
            while (j >= 0 && _records[j].VisitorId > v.VisitorId)
            {
                _records[j + 1] = _records[j];
                j--;
            }
            _records[j + 1] = v;
        }
    }
}
