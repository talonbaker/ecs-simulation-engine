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

            // STRATEGY 1: Get food from Fridge
            if (human.Has<FoodDesireTag>() && !human.Has<FoodObjectComponent>())
            {
                var fridgeEntity = em.Query<RefrigeratorComponent>().FirstOrDefault();
                if (fridgeEntity != null)
                {
                    var fridge = fridgeEntity.Get<RefrigeratorComponent>();
                    if (fridge.BananaCount > 0)
                    {
                        fridge.BananaCount--;
                        fridgeEntity.Add(fridge); // Update fridge

                        // Human "picks up" the banana (it becomes a component on them)
                        human.Add(new FoodObjectComponent
                        {
                            Name = "Banana",
                            NutritionPerBite = 20f,
                            BitesRemaining = 3,
                            Toughness = 0.2f
                        });
                    }
                }
            }

            // STRATEGY 2: Take a bite of held food
            if (human.Has<FoodObjectComponent>())
            {
                TakeBite(em, human);
            }
        }
    }

    private void TakeBite(EntityManager em, Entity human)
    {
        var food = human.Get<FoodObjectComponent>();

        // 1. Create the Bolus (The transformation!)
        var bolus = em.CreateEntity();
        bolus.Add(new IdentityComponent("Banana Bolus", "Bolus"));
        bolus.Add(new BolusComponent
        {
            NutritionValue = food.NutritionPerBite,
            FoodType = food.Name,
            Volume = 10f
        });

        // 2. Put it in the Esophagus
        bolus.Add(new EsophagusTransitComponent
        {
            Progress = 0f,
            Speed = 0.3f,
            TargetEntityId = human.Id
        });

        // 3. Update the held food
        food.BitesRemaining--;
        if (food.BitesRemaining <= 0) human.Remove<FoodObjectComponent>();
        else human.Add(food);
    }
}