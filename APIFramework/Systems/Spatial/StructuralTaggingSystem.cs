using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems.Spatial;

/// <summary>
/// PreUpdate system that runs once at boot and then idles.
/// Attaches StructuralTag to every entity matching the obstacle/structural predicate set:
///   - Has ObstacleTag
///   - Has AnchorObjectComponent and PositionComponent and is not an NPC
///
/// Note: WallComponent and DoorComponent do not exist in v0.1; when they are added,
/// extend the predicate here. The completion note documents which predicates were used.
/// A follow-up data packet can switch to per-template "structural: true" flags.
/// </summary>
public sealed class StructuralTaggingSystem : ISystem
{
    private bool _ran;

    public void Update(EntityManager em, float deltaTime)
    {
        if (_ran) return;
        _ran = true;

        // Entities with ObstacleTag are structural (walls, furniture, fixtures)
        foreach (var entity in em.Query<ObstacleTag>())
        {
            if (!entity.Has<StructuralTag>())
                entity.Add(default(StructuralTag));
        }

        // Fixed authored world objects (AnchorObjectComponent) that are not NPCs
        foreach (var entity in em.Query<AnchorObjectComponent>())
        {
            if (!entity.Has<PositionComponent>()) continue;
            if (entity.Has<NpcTag>() || entity.Has<HumanTag>()) continue;
            if (!entity.Has<StructuralTag>())
                entity.Add(default(StructuralTag));
        }
    }
}
