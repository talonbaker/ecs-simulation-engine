using System.Collections.Generic;

namespace APIFramework.Systems.Coupling;

/// <summary>
/// One entry in the lighting-to-drive coupling table.
/// Loaded from SimConfig.lighting.driveCouplings[] via Newtonsoft.Json.
/// </summary>
public class LightingCouplingEntry
{
    /// <summary>The condition that selects this entry for an NPC's current context.</summary>
    public CouplingCondition        Condition    { get; set; } = new();
    /// <summary>
    /// Drive-name → fractional delta-per-tick map. Keys are lowercase social-drive names
    /// (e.g. "loneliness", "irritation"); values are accumulated by <see cref="SocialDriveAccumulator"/>.
    /// </summary>
    public Dictionary<string, float> DeltasPerTick { get; set; } = new();
}

/// <summary>
/// Ordered list of lighting-to-drive coupling entries with first-match-wins lookup.
/// Built from SimConfig at boot and injected into LightingToDriveCouplingSystem.
/// </summary>
public sealed class LightingDriveCouplingTable
{
    /// <summary>The coupling entries in declaration order — first-match-wins semantics.</summary>
    public IReadOnlyList<LightingCouplingEntry> Entries { get; }

    /// <summary>
    /// Snapshots <paramref name="entries"/> into an immutable, ordered list.
    /// </summary>
    /// <param name="entries">Entries to copy, typically loaded from SimConfig.</param>
    public LightingDriveCouplingTable(IEnumerable<LightingCouplingEntry> entries)
    {
        var list = new List<LightingCouplingEntry>();
        foreach (var e in entries) list.Add(e);
        Entries = list;
    }

    /// <summary>
    /// Returns the first entry whose condition matches <paramref name="ctx"/>, or null if none match.
    /// </summary>
    /// <param name="ctx">The per-NPC lighting context resolved this tick.</param>
    public LightingCouplingEntry? FindFirst(in CouplingMatchContext ctx)
    {
        foreach (var entry in Entries)
            if (entry.Condition.Matches(ctx)) return entry;
        return null;
    }
}
