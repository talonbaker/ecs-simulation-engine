namespace APIFramework.Core;

using APIFramework.Components;

public static class EntityTemplates
{
    public static Entity SpawnHuman(EntityManager manager)
    {
        var entity = manager.CreateEntity();
        entity.Add(new IdentityComponent { Name = "Human" });
        entity.Add(new MetabolismComponent
        {
            Satiation         = 100f,  // Starts fully satiated
            Hydration         = 100f,  // Starts fully hydrated
            BodyTemp          = 37.0f,
            SatiationDrainRate = 0.3f, // Satiation depletes: 100→0 in ~5.5 min at 1x speed
            HydrationDrainRate = 0.5f  // Hydration depletes: 100→0 in ~3.3 min at 1x speed
        });
        entity.Add(new StomachComponent
        {
            CurrentVolumeMl = 0f,
            DigestionRate   = 2.0f,
            NutritionQueued = 0f,
            HydrationQueued = 0f
        });

        return entity;
    }

    public static Entity SpawnCat(EntityManager manager)
    {
        var entity = manager.CreateEntity();
        entity.Add(new IdentityComponent { Name = "Cat" });
        entity.Add(new MetabolismComponent
        {
            Satiation         = 100f,
            Hydration         = 100f,
            BodyTemp          = 38.5f,
            SatiationDrainRate = 0.15f,
            HydrationDrainRate = 0.25f
        });
        entity.Add(new StomachComponent
        {
            CurrentVolumeMl = 0f,
            DigestionRate   = 1.0f,
            NutritionQueued = 0f,
            HydrationQueued = 0f
        });

        return entity;
    }
}
