namespace APIFramework.Components;

/// <summary>Physical condition of an authored world object at game start.</summary>
public enum AnchorObjectPhysicalState
{
    /// <summary>Object is intact and functional.</summary>
    Present,
    /// <summary>Object is visibly worn but still recognisable.</summary>
    PresentDegraded,
    /// <summary>Object is heavily damaged; near-derelict.</summary>
    PresentGreatlyDegraded,
    /// <summary>Object is referenced by the world definition but is not physically present.</summary>
    Absent
}

/// <summary>
/// Data component on world-object entities spawned from <c>objectsAtAnchors[]</c>
/// in the world definition. Carries the authored identity and physical state.
/// </summary>
public struct AnchorObjectComponent
{
    /// <summary>Stable authored object id from the world definition.</summary>
    public string                   Id            { get; init; }
    /// <summary>Id of the <see cref="RoomComponent"/> the object is anchored to.</summary>
    public string                   RoomId        { get; init; }
    /// <summary>Free-form description from the world bible (e.g. "box of floppy disks").</summary>
    public string                   Description   { get; init; }
    /// <summary>Initial physical condition at world boot.</summary>
    public AnchorObjectPhysicalState PhysicalState { get; init; }
}
