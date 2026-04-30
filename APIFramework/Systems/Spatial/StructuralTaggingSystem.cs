using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems.Spatial;

/// <summary>
/// PreUpdate system that runs once at boot (and is then idle).
/// Walks the entity manager and attaches StructuralTag to every entity matching:
///   - has ObstacleTag, OR
///   - is an AnchorObjectTag entity that is not an NPC
///
/// Also attaches MutableTopologyTag to:
///   - All StructuralTag entities except NPCs
///
/// After this runs, topology changes can safely flow through IWorldMutationApi.
/// </summary>
public sealed class StructuralTaggingSystem : ISystem
{
    private bool _done = false;

    public void Update(EntityManager em, float deltaTime)
    {
        if (_done) return;
        _done = true;

        // Attach StructuralTag to obstacles and named anchors
        foreach (var entity in em.Query<PositionComponent>())
        {
            bool isStructural = false;

            // Check if it's an obstacle
            if (entity.Has<ObstacleTag>())
                isStructural = true;

            // Check for named anchors (world objects) but not NPCs
            if (!isStructural && entity.Has<AnchorObjectTag>() && !entity.Has<NpcTag>())
            {
                isStructural = true;
            }

            if (isStructural && !entity.Has<StructuralTag>())
            {
                entity.Add(new StructuralTag());
            }
        }

        // Attach MutableTopologyTag to movable structural entities (non-NPCs)
        foreach (var entity in em.Query<StructuralTag>())
        {
            if (!entity.Has<NpcTag>() && !entity.Has<MutableTopologyTag>())
            {
                entity.Add(new MutableTopologyTag());
            }
        }
    }
}
