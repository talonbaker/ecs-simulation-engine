using System;
using System.Linq;
using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Attaches <see cref="SocialMaskComponent"/> to every NPC that does not yet have one.
/// Baseline mask strength is derived from personality: high Conscientiousness raises it,
/// high Extraversion lowers it.
///
/// Phase: PreUpdate (0) — idempotent; runs before any system that reads SocialMaskComponent.
/// </summary>
public sealed class MaskInitializerSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<NpcTag>().ToList())
        {
            if (entity.Has<SocialMaskComponent>()) continue;

            int baseline = 30;
            if (entity.Has<PersonalityComponent>())
            {
                var p = entity.Get<PersonalityComponent>();
                baseline = Math.Clamp(p.Conscientiousness * 10 + p.Extraversion * (-5) + 30, 0, 100);
            }

            entity.Add(new SocialMaskComponent { Baseline = baseline });
        }
    }
}
