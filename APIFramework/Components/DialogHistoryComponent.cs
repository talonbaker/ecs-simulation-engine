using System.Collections.Generic;

namespace APIFramework.Components;

/// <summary>
/// Per-NPC record of every phrase fragment this NPC has ever spoken.
/// Use counts and calcify state drive voice emergence over time.
/// The Dictionary field is a reference type — copies of this struct share the same instance.
/// </summary>
public struct DialogHistoryComponent
{
    public Dictionary<string, FragmentUseRecord> UsesByFragmentId;

    /// <summary>Per-(listener, fragment) use counter. Key: listener int id. Value: counts per fragment.</summary>
    public Dictionary<int, Dictionary<string, int>> UsesByListenerAndFragmentId;

    public DialogHistoryComponent()
    {
        UsesByFragmentId            = new Dictionary<string, FragmentUseRecord>();
        UsesByListenerAndFragmentId = new Dictionary<int, Dictionary<string, int>>();
    }
}

/// <summary>
/// Mutable record of a single fragment's use history for one NPC.
/// </summary>
public sealed class FragmentUseRecord
{
    public int    UseCount;
    public long   FirstUseTick;
    public long   LastUseTick;
    public double LastUseGameTimeSec;
    public string DominantContext = string.Empty;

    // Number of uses per context — used by DialogCalcifySystem to compute dominance fraction.
    public Dictionary<string, int> ContextCounts { get; } = new();

    public bool Calcified;
}
