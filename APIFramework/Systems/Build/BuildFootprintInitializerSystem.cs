using APIFramework.Build;
using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems.Build;

/// <summary>
/// Spawn-phase initializer: attaches <see cref="BuildFootprintComponent"/> to every
/// entity that has a <see cref="PropTypeIdComponent"/> but no footprint yet.
///
/// Phase: PreUpdate — runs before any system that reads footprint data.
/// Idempotent: entities that already have <see cref="BuildFootprintComponent"/> are skipped.
///
/// When the catalog has no entry for a prop's type id, a warning is logged and
/// the component is left unattached — no crash.
/// </summary>
public sealed class BuildFootprintInitializerSystem : ISystem
{
    private readonly BuildFootprintCatalog _catalog;

    public BuildFootprintInitializerSystem(BuildFootprintCatalog catalog)
    {
        _catalog = catalog;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<PropTypeIdComponent>().ToList())
        {
            if (entity.Has<BuildFootprintComponent>()) continue;

            var propTypeId = entity.Get<PropTypeIdComponent>().PropTypeId;
            var footprint  = _catalog.GetByPropType(propTypeId);

            if (footprint is null)
            {
                Console.WriteLine($"[BuildFootprintInitializerSystem] No catalog entry for propTypeId '{propTypeId}' — footprint not attached.");
                continue;
            }

            entity.Add(new BuildFootprintComponent
            {
                WidthTiles        = footprint.WidthTiles,
                DepthTiles        = footprint.DepthTiles,
                BottomHeight      = footprint.BottomHeight,
                TopHeight         = footprint.TopHeight,
                CanStackOnTop     = footprint.CanStackOnTop,
                FootprintCategory = footprint.FootprintCategory
            });
        }
    }
}
