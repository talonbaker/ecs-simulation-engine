using APIFramework.Components;
using APIFramework.Core;
using Warden.Contracts.Telemetry;

namespace Warden.Telemetry;

/// <summary>
/// Maps a raw <see cref="Entity"/> to its <see cref="SpeciesType"/> by inspecting
/// the tag components the engine attaches during
/// <see cref="EntityTemplates.SpawnHuman"/> / <see cref="EntityTemplates.SpawnCat"/>.
///
/// Rules:
///   HumanTag present → <see cref="SpeciesType.Human"/>
///   CatTag   present → <see cref="SpeciesType.Cat"/>
///   Neither           → <see cref="SpeciesType.Unknown"/>
/// </summary>
internal static class SpeciesClassifier
{
    public static SpeciesType Classify(Entity entity)
    {
        if (entity.Has<HumanTag>()) return SpeciesType.Human;
        if (entity.Has<CatTag>())   return SpeciesType.Cat;
        return SpeciesType.Unknown;
    }
}
