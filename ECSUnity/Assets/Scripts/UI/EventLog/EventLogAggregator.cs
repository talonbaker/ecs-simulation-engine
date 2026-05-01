using System.Collections.Generic;
using Warden.Contracts.Telemetry;

/// <summary>
/// Pulls chronicle entries from <see cref="WorldStateDto.Chronicle"/> and
/// deduplicates by <see cref="ChronicleEntryDto.Id"/> — WP-3.1.G.
///
/// AGGREGATION SOURCES (v0.1)
/// ────────────────────────────
/// v0.1 aggregates only from WorldStateDto.Chronicle (the chronicle projected by
/// Warden.Telemetry). Personal-memory and relationship-memory entries are deferred
/// until those fields are projected into WorldStateDto.
///
/// DEDUPLICATION
/// ─────────────
/// A single narrative event may create entries in both the office-wide chronicle
/// and in personal/relationship memory. Each entry carries a stable string Id;
/// the aggregator keeps only the first occurrence of any given Id.
///
/// SORT ORDER
/// ──────────
/// Entries are returned newest-first (descending by Tick).
/// </summary>
public sealed class EventLogAggregator
{
    // ── Aggregation ───────────────────────────────────────────────────────────

    /// <summary>
    /// Aggregates, deduplicates, and sorts chronicle entries from the given snapshot.
    /// Returns an empty list if the snapshot has no Chronicle data.
    /// </summary>
    /// <param name="worldState">Latest world-state snapshot.</param>
    /// <param name="filters">Active filter state.</param>
    /// <param name="currentTick">Current engine tick (for time-range filter).</param>
    /// <param name="ticksPerDay">Ticks per game-day (from SimConfig; default 1200 at 50 Hz).</param>
    public List<ChronicleEntryDto> Aggregate(
        WorldStateDto    worldState,
        EventLogFilters  filters,
        long             currentTick,
        long             ticksPerDay = 1200)
    {
        var result = new List<ChronicleEntryDto>();
        var seen   = new HashSet<string>();

        if (worldState?.Chronicle == null)
            return result;

        foreach (var entry in worldState.Chronicle)
        {
            if (entry == null) continue;

            // Deduplicate by entry Id.
            string id = entry.Id ?? string.Empty;
            if (!seen.Add(id)) continue;

            // Apply filters.
            if (filters != null && !filters.Passes(entry, currentTick, ticksPerDay))
                continue;

            result.Add(entry);
        }

        // Sort newest-first.
        result.Sort((a, b) => b.Tick.CompareTo(a.Tick));

        return result;
    }

    /// <summary>
    /// Returns all unique participant entity IDs across all chronicle entries.
    /// Used to populate the NPC filter dropdown.
    /// </summary>
    public List<string> GetAllParticipantIds(WorldStateDto worldState)
    {
        var seen   = new HashSet<string>();
        var result = new List<string>();

        if (worldState?.Chronicle == null)
            return result;

        foreach (var entry in worldState.Chronicle)
        {
            if (entry?.Participants == null) continue;
            foreach (var p in entry.Participants)
            {
                if (p != null && seen.Add(p))
                    result.Add(p);
            }
        }

        result.Sort();
        return result;
    }
}
