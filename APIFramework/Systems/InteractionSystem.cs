using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems;

/// <summary>
/// Processes food that the entity is currently holding — takes a bite, creates a bolus,
/// sends it down the esophagus. This handles physical food objects held in hand.
///
/// TODO: Food delivery (fridge → hand) goes here once world food sources exist.
///
/// Pipeline position: 6 of 8 — after FeedingSystem and DrinkingSystem have
/// potentially spawned transit entities, before EsophagusSystem moves them.
/// </summary>
public class InteractionSystem : ISystem
{
    private readonly InteractionSystemConfig _cfg;

    public InteractionSystem(InteractionSystemConfig cfg) => _cfg = cfg;

    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var human in em.Query<MetabolismComponent>().ToList())
        {
            // Throat must be clear before we can swallow anything
            bool isEsophagusBusy = em.Query<EsophagusTransitComponent>()
                .Any(t => t.Get<EsophagusTransitComponent>().TargetEntityId == human.Id);
            if (isEsophagusBusy) continue;

            // TODO: Food delivery goes here — fridge, counter, bowl, etc.
            // When a food source entity exists in the world, this system will query for it,
            // pull food from it, and add a FoodObjectComponent to the human.

            // Take a bite of whatever is currently held
            if (human.Has<FoodObjectComponent>())
                TakeBite(em, human);
        }
    }

    private void TakeBite(EntityManager em, Entity human)
    {
        var food  = human.Get<FoodObjectComponent>();
        var bolus = em.CreateEntity();

        bolus.Add(new IdentityComponent("Bolus", "Bolus"));
        bolus.Add(new BolusComponent
        {
            Nutrients = food.NutrientsPerBite,       // full real-biology breakdown
            FoodType  = food.Name,
            Volume    = _cfg.BiteVolumeMl,
            Toughness = food.Toughness
        });
        bolus.Add(new EsophagusTransitComponent
        {
            Progress       = 0f,
            Speed          = _cfg.EsophagusSpeed,
            TargetEntityId = human.Id
        });

        food.BitesRemaining--;
        if (food.BitesRemaining <= 0) human.Remove<FoodObjectComponent>();
        else                          human.Add(food);
    }
}
