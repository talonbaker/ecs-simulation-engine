using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

public class BiologicalConditionSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        if (deltaTime <= 0) return;

        foreach (var entity in em.Query<MetabolismComponent>().ToList())
        {
            var meta = entity.Get<MetabolismComponent>();

            // ── Biological tag management ────────────────────────────────────
            // Hunger and Thirst are computed: Hunger = 100 - Satiation, Thirst = 100 - Hydration
            // Thresholds are on the 0-100 sensation scale
            ToggleTag<ThirstTag>   (entity, meta.Thirst >= 30f);  // Hydration < 70 — starting to feel thirsty
            ToggleTag<DehydratedTag>(entity, meta.Thirst >= 70f); // Hydration < 30 — severely dehydrated
            ToggleTag<HungerTag>   (entity, meta.Hunger >= 30f);  // Satiation < 70 — starting to feel hungry
            ToggleTag<StarvingTag> (entity, meta.Hunger >= 80f);  // Satiation < 20 — starving
            ToggleTag<IrritableTag>(entity, meta.Hunger > 60f || meta.Thirst > 60f);

            // ── Water intake ─────────────────────────────────────────────────
            if (!entity.Has<ThirstTag>()) continue;

            bool throatBusy = em.Query<EsophagusTransitComponent>()
                .Any(t => t.Get<EsophagusTransitComponent>().TargetEntityId == entity.Id);
            if (throatBusy) continue;

            // Prevent machine-gun gulping — don't queue more water than the stomach
            // can absorb in the near term. Exception: override when severely dehydrated.
            // TODO: Replace with priority queue urgency scoring when Brain is implemented.
            if (entity.Has<StomachComponent>())
            {
                var stomach = entity.Get<StomachComponent>();
                bool severelyDehydrated = entity.Has<DehydratedTag>();
                if (!severelyDehydrated && stomach.HydrationQueued >= 30f) continue;
                if ( severelyDehydrated && stomach.HydrationQueued >= 60f) continue;
            }

            // Yield the throat to FeedingSystem when hunger is meaningfully more urgent.
            // A 15-point margin avoids reacting to near-equal values.
            // TODO: Replace with priority queue when Brain is implemented.
            if (meta.Hunger >= meta.Thirst + 15f && entity.Has<HungerTag>()) continue;

            var water = em.CreateEntity();
            water.Add(new IdentityComponent("Water", "Liquid"));
            water.Add(new LiquidComponent
            {
                VolumeMl       = 15f,
                HydrationValue = 30f,
                LiquidType     = "Water"
            });
            water.Add(new EsophagusTransitComponent
            {
                Progress       = 0f,
                Speed          = 0.8f,
                TargetEntityId = entity.Id
            });
        }
    }

    private void ToggleTag<T>(Entity entity, bool condition) where T : struct
    {
        if (condition && !entity.Has<T>())      entity.Add(new T());
        else if (!condition && entity.Has<T>()) entity.Remove<T>();
    }
}
