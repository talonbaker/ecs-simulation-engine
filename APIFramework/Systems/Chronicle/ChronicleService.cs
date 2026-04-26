using System.Collections.Generic;

namespace APIFramework.Systems.Chronicle;

/// <summary>
/// Global narrative memory. Ring buffer bounded at <c>maxEntries</c>; oldest entry
/// dropped on overflow. Sorted by insertion order (which is tick-ascending because
/// <see cref="PersistenceThresholdDetector"/> processes candidates in tick order).
/// </summary>
public sealed class ChronicleService
{
    private readonly List<ChronicleEntry> _entries;
    private readonly int                  _maxEntries;

    public ChronicleService(int maxEntries = 4096)
    {
        _maxEntries = maxEntries > 0 ? maxEntries : 1;
        _entries    = new List<ChronicleEntry>(_maxEntries);
    }

    public IReadOnlyList<ChronicleEntry> All => _entries;

    public void Append(ChronicleEntry entry)
    {
        if (_entries.Count >= _maxEntries)
            _entries.RemoveAt(0);
        _entries.Add(entry);
    }
}
