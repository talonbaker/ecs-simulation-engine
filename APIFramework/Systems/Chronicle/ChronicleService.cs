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

    /// <summary>
    /// Creates a new chronicle ring buffer.
    /// </summary>
    /// <param name="maxEntries">Maximum entries retained; oldest are dropped on overflow. Values ≤ 0 are clamped to 1.</param>
    public ChronicleService(int maxEntries = 4096)
    {
        _maxEntries = maxEntries > 0 ? maxEntries : 1;
        _entries    = new List<ChronicleEntry>(_maxEntries);
    }

    /// <summary>All entries currently in the chronicle, in insertion (tick-ascending) order.</summary>
    public IReadOnlyList<ChronicleEntry> All => _entries;

    /// <summary>
    /// Appends <paramref name="entry"/> to the chronicle. If capacity has been reached,
    /// the oldest entry is dropped first.
    /// </summary>
    /// <param name="entry">Entry to append.</param>
    public void Append(ChronicleEntry entry)
    {
        if (_entries.Count >= _maxEntries)
            _entries.RemoveAt(0);
        _entries.Add(entry);
    }
}
