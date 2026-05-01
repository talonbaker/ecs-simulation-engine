namespace APIFramework.Components;

/// <summary>
/// Human-readable identity for any entity (NPC, world object, room, etc.).
/// <see cref="Name"/> is shown in UI; <see cref="Value"/> is an optional secondary label.
/// </summary>
public struct IdentityComponent
{
    /// <summary>Display name shown in UI/debug overlays.</summary>
    public string Name;
    /// <summary>Optional secondary label (kind, archetype, free-form text).</summary>
    public string Value;

    /// <summary>Constructs an identity with the given <paramref name="name"/> and optional <paramref name="value"/>.</summary>
    public IdentityComponent(string name, string value = "")
    {
        Name = name;
        Value = value;
    }

    /// <summary>Returns <see cref="Name"/> so default UI rendering shows the friendly name.</summary>
    // Defaulting ToString to Name ensures your UI stays clean by default
    public override string ToString() => Name;
}