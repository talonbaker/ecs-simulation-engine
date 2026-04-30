using System;
using System.Linq;
using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// PreUpdate phase. Attaches <see cref="SocialMaskComponent"/> to every NPC that
/// does not yet have one, with baseline derived from personality: high Conscientiousness
/// raises baseline; high Extraversion lowers it.
/// </summary>
/// <remarks>
/// Reads: <see cref="NpcTag"/>, <see cref="PersonalityComponent"/>.<br/>
/// Writes: <see cref="SocialMaskComponent"/> (idempotent — never overwritten).<br/>
/// Phase: PreUpdate, before any system that reads <see cref="SocialMaskComponent"/>.
/// </remarks>
public sealed class MaskInitializerSystem : ISystem
{
    /// <summary>Per-tick idempotent attach pass; assigns each NPC a personality-shaped mask baseline.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds, unused).</param>
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
