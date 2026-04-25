using APIFramework.Core;
using Warden.Contracts.Telemetry;

namespace Warden.Telemetry;

/// <summary>
/// Maps a <see cref="WorldObjectSnapshot"/> to a <see cref="WorldObjectKind"/> enum value
/// by reading the boolean flags the engine captures from world-object marker components.
///
/// Priority order matches the engine's own world-object classification logic:
///   IsFridge → Fridge, IsSink → Sink, IsToilet → Toilet, IsBed → Bed, else → Other.
/// </summary>
internal static class WorldObjectKindClassifier
{
    public static WorldObjectKind Classify(WorldObjectSnapshot snap)
    {
        if (snap.IsFridge)  return WorldObjectKind.Fridge;
        if (snap.IsSink)    return WorldObjectKind.Sink;
        if (snap.IsToilet)  return WorldObjectKind.Toilet;
        if (snap.IsBed)     return WorldObjectKind.Bed;
        return WorldObjectKind.Other;
    }
}
