using System.Collections.Generic;

namespace APIFramework.Components;

/// <summary>
/// Physiology action classes that can be vetoed by social inhibitions.
/// Written each tick by PhysiologyGateSystem; consumed by physiology action systems
/// before they fire an autonomous action.
/// </summary>
public enum BlockedActionClass
{
    /// <summary>FeedingSystem will not autonomously trigger eating.</summary>
    Eat,

    /// <summary>SleepSystem will not autonomously trigger sleep onset.</summary>
    Sleep,

    /// <summary>UrinationSystem will not autonomously trigger urination.</summary>
    Urinate,

    /// <summary>DefecationSystem will not autonomously trigger defecation.</summary>
    Defecate
}

/// <summary>
/// Per-NPC veto set. Written each tick by PhysiologyGateSystem; read by physiology
/// systems before they trigger an autonomous action. Empty set (default) = no vetoes;
/// the physiology system runs as it always has.
///
/// NOTE: IReadOnlySet&lt;T&gt; is not available in netstandard2.1. The backing store is
/// a <see cref="HashSet{T}"/>, exposed as <see cref="IReadOnlyCollection{T}"/>. The
/// convenience method <see cref="Contains"/> provides O(1) membership testing.
/// </summary>
public struct BlockedActionsComponent
{
    private readonly HashSet<BlockedActionClass>? _blocked;

    /// <summary>
    /// The set of physiology action classes currently vetoed for this NPC.
    /// Empty when willpower is low or stress is high enough to break the gate open.
    /// </summary>
    public IReadOnlyCollection<BlockedActionClass> Blocked =>
        (IReadOnlyCollection<BlockedActionClass>?)_blocked ?? _empty;

    /// <summary>Constructs a blocked-actions component wrapping the given veto set.</summary>
    public BlockedActionsComponent(HashSet<BlockedActionClass> blocked)
    {
        _blocked = blocked;
    }

    /// <summary>O(1) membership test — prefer this over iterating Blocked.</summary>
    public bool Contains(BlockedActionClass cls) =>
        _blocked != null && _blocked.Contains(cls);

    private static readonly IReadOnlyCollection<BlockedActionClass> _empty =
        new HashSet<BlockedActionClass>();
}
