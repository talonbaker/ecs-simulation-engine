using APIFramework.Components;

namespace APIFramework.Systems.Lighting;

/// <summary>
/// Singleton service that holds the current sun position. Only SunSystem writes to it;
/// all other systems read the exposed CurrentSunState property.
/// </summary>
public sealed class SunStateService
{
    /// <summary>The sun state as of the most recent SunSystem tick.</summary>
    public SunStateRecord CurrentSunState { get; private set; }

    /// <summary>Called by SunSystem each tick. Also callable from tests to inject a specific sun state.</summary>
    /// <param name="state">The new sun state to publish.</param>
    public void UpdateSunState(SunStateRecord state)
    {
        CurrentSunState = state;
    }
}
