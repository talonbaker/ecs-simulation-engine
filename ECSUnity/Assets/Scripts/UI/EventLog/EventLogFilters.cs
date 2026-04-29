using System.Collections.Generic;
using System.Linq;
using Warden.Contracts.Telemetry;

/// <summary>
/// Immutable filter state for the Event Log panel — WP-3.1.G.
///
/// Filtering contract:
///   NpcFilter     — null = show all NPCs; non-null = participant entity ID must be present.
///   KindFilter    — empty = show all kinds; non-empty = Kind must be in the set.
///   TimeRangeDays — 0 = all time; positive = last N game-days (ticks >= currentTick - N*ticksPerDay).
///
/// Filter instances are immutable; the panel creates a new instance whenever a
/// filter control changes. Immutability keeps aggregator logic side-effect-free.
/// </summary>
public sealed class EventLogFilters
{
    // ── Presets ────────────────────────────────────────────────────────────────

    /// <summary>Default: last 7 game-days, all NPCs, all kinds.</summary>
    public static readonly EventLogFilters Default = new EventLogFilters(
        npcFilter:     null,
        kindFilter:    new HashSet<ChronicleEventKind>(),
        timeRangeDays: 7);

    /// <summary>Show everything: no NPC filter, all kinds, all time.</summary>
    public static readonly EventLogFilters AllTime = new EventLogFilters(
        npcFilter:     null,
        kindFilter:    new HashSet<ChronicleEventKind>(),
        timeRangeDays: 0);

    // ── Properties ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Entity ID of the NPC to filter by, or null for all NPCs.
    /// </summary>
    public string NpcFilter { get; }

    /// <summary>
    /// Set of event kinds to include. Empty set = include all kinds.
    /// </summary>
    public IReadOnlyCollection<ChronicleEventKind> KindFilter { get; }

    /// <summary>
    /// Show events from the last N game-days. 0 = all time.
    /// </summary>
    public int TimeRangeDays { get; }

    // ── Constructor ────────────────────────────────────────────────────────────

    public EventLogFilters(
        string                        npcFilter,
        IReadOnlyCollection<ChronicleEventKind> kindFilter,
        int                           timeRangeDays)
    {
        NpcFilter     = npcFilter;
        KindFilter    = kindFilter ?? new HashSet<ChronicleEventKind>();
        TimeRangeDays = timeRangeDays < 0 ? 0 : timeRangeDays;
    }

    // ── Filter application ────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if <paramref name="entry"/> passes all active filters.
    /// </summary>
    /// <param name="entry">The chronicle entry to evaluate.</param>
    /// <param name="currentTick">Current engine tick (for time-range computation).</param>
    /// <param name="ticksPerDay">Engine ticks per game-day (from SimConfig).</param>
    public bool Passes(ChronicleEntryDto entry, long currentTick, long ticksPerDay)
    {
        if (entry == null) return false;

        // Time-range filter: skip entries older than the window.
        if (TimeRangeDays > 0 && ticksPerDay > 0)
        {
            long windowStart = currentTick - (TimeRangeDays * ticksPerDay);
            if (entry.Tick < windowStart) return false;
        }

        // NPC filter: entry must involve the filtered NPC.
        if (NpcFilter != null)
        {
            if (entry.Participants == null) return false;
            bool found = false;
            foreach (var p in entry.Participants)
                if (p == NpcFilter) { found = true; break; }
            if (!found) return false;
        }

        // Kind filter: entry kind must be in the selected set.
        if (KindFilter.Count > 0 && !KindFilter.Contains(entry.Kind))
            return false;

        return true;
    }

    // ── Factory helpers ───────────────────────────────────────────────────────

    public EventLogFilters WithNpc(string entityId)
        => new EventLogFilters(entityId, KindFilter, TimeRangeDays);

    public EventLogFilters WithKinds(IReadOnlyCollection<ChronicleEventKind> kinds)
        => new EventLogFilters(NpcFilter, kinds, TimeRangeDays);

    public EventLogFilters WithTimeRangeDays(int days)
        => new EventLogFilters(NpcFilter, KindFilter, days);
}
