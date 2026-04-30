using System.Collections.Generic;

namespace APIFramework.Components;

/// <summary>
/// Per-NPC record of every phrase fragment this NPC has ever spoken.
/// Use counts and calcify state drive voice emergence over time.
/// The Dictionary field is a reference type — copies of this struct share the same instance.
/// </summary>
public struct DialogHistoryComponent
{
    /// <summary>Per-fragment use records keyed by fragment id.</summary>
    public Dictionary<string, FragmentUseRecord> UsesByFragmentId;

    /// <summary>Per-(listener, fragment) use counter. Key: listener int id. Value: counts per fragment.</summary>
    public Dictionary<int, Dictionary<string, int>> UsesByListenerAndFragmentId;

    /// <summary>Initialises both backing dictionaries to empty.</summary>
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
    /// <summary>Total times this NPC has spoken the fragment.</summary>
    public int    UseCount;
    /// <summary>SimulationClock.CurrentTick of the very first use.</summary>
    public long   FirstUseTick;
    /// <summary>SimulationClock.CurrentTick of the most recent use.</summary>
    public long   LastUseTick;
    /// <summary>Game-seconds-since-start of the most recent use.</summary>
    public double LastUseGameTimeSec;
    /// <summary>Context label most frequently associated with this fragment (computed by DialogCalcifySystem).</summary>
    public string DominantContext = string.Empty;

    /// <summary>Number of uses per context — used by DialogCalcifySystem to compute dominance fraction.</summary>
    public Dictionary<string, int> ContextCounts { get; } = new();

    /// <summary>True once the fragment has stabilised into the NPC's calcified vocabulary.</summary>
    public bool Calcified;
}
