using System.Collections.Generic;

namespace APIFramework.Systems.Coupling;

/// <summary>
/// One entry in the lighting-to-drive coupling table.
/// Loaded from SimConfig.lighting.driveCouplings[] via Newtonsoft.Json.
/// </summary>
public class LightingCouplingEntry
{
    public CouplingCondition        Condition    { get; set; } = new();
    public Dictionary<string, float> DeltasPerTick { get; set; } = new();
}

/// <summary>
/// Ordered list of lighting-to-drive coupling entries with first-match-wins lookup.
/// Built from SimConfig at boot and injected into LightingToDriveCouplingSystem.
/// </summary>
public sealed class LightingDriveCouplingTable
{
    public IReadOnlyList<LightingCouplingEntry> Entries { get; }

    public LightingDriveCouplingTable(IEnumerable<LightingCouplingEntry> entries)
    {
        var list = new List<LightingCouplingEntry>();
        foreach (var e in entries) list.Add(e);
        Entries = list;
    }

    /// <summary>
    /// Returns the first entry whose condition matches <paramref name="ctx"/>, or null if none match.
    /// </summary>
    public LightingCouplingEntry? FindFirst(in CouplingMatchContext ctx)
    {
        foreach (var entry in Entries)
            if (entry.Condition.Matches(ctx)) return entry;
        return null;
    }
}
