namespace APIFramework.Components;

/// <summary>Physical condition of an authored world object at game start.</summary>
public enum AnchorObjectPhysicalState
{
    Present,
    PresentDegraded,
    PresentGreatlyDegraded,
    Absent
}

/// <summary>
/// Data component on world-object entities spawned from <c>objectsAtAnchors[]</c>
/// in the world definition. Carries the authored identity and physical state.
/// </summary>
public struct AnchorObjectComponent
{
    public string                   Id            { get; init; }
    public string                   RoomId        { get; init; }
    public string                   Description   { get; init; }
    public AnchorObjectPhysicalState PhysicalState { get; init; }
}
