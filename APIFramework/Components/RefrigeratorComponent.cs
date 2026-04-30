namespace APIFramework.Components;

// TODO: Remove this file entirely when git tooling allows.
// Refrigerator is deferred — food sources are hardcoded for now while core systems are proven out.

/// <summary>
/// Deferred component representing a refrigerator inventory.
/// Currently unused — food sources are hardcoded while core systems are proven out.
/// </summary>
[Obsolete("RefrigeratorComponent is not yet in use. Food sources are hardcoded during system development.")]
public struct RefrigeratorComponent
{
    /// <summary>Number of banana servings remaining in the fridge.</summary>
    public int BananaCount;
}
