using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Systems;

public class InteractionSystem : ISystem
{
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var human in em.Query<MetabolismComponent>().ToList())
        {
            // Only act if the throat is clear
            bool isEsophagusBusy = em.Query<EsophagusTransitComponent>()
                .Any(t => t.Get<EsophagusTransitComponent>().TargetEntityId == human.Id);

            if (isEsophagusBusy) continue;

            // TODO: Food delivery goes here — fridge, counter, bowl, etc.
            // When a food source entity exists in the world, this system will query for it,
            // pull food from it, and add a FoodObjectComponent to the human.

            // Take a bite of whatever is already held
            if (human.Has<FoodObjectComponent>())
            {
                TakeBite(em, human);
            }
        }
    }

    private void TakeBite(EntityManager em, Entity human)
    {
        var food = human.Get<FoodObjectComponent>();

        // Create the bolus from the bite
        var bolus = em.CreateEntity();
        bolus.Add(new IdentityComponent("Banana Bolus", "Bolus"));
        bolus.Add(new BolusComponent
        {
            NutritionValue = food.NutritionPerBite,
            FoodType       = food.Name,
            Volume         = 50f,  // ml — reasonable bite volume
            Toughness      = food.Toughness
        });

        // Send it down the esophagus toward this human's stomach
        bolus.Add(new EsophagusTransitComponent
        {
            Progress     = 0f,
            Speed        = 0.3f,
            TargetEntityId = human.Id
        });

        // Consume one bite from the held food
        food.BitesRemaining--;
        if (food.BitesRemaining <= 0) human.Remove<FoodObjectComponent>();
        else human.Add(food);
    }
}
