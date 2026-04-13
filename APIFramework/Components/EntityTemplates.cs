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
            // Rates represented in percentages per second
            Hunger = 0f,
            HungerRate = 1.2f,
            Thirst = 0f,
            ThirstRate = 2.5f,
            BodyTemp = 37.0f
        });

        return entity;
    }

    public static Entity SpawnCat(EntityManager manager)
    {
        var entity = manager.CreateEntity();
        entity.Add(new IdentityComponent { Name = "Cat" });
        entity.Add(new MetabolismComponent
        {
            Hunger = 0f,
            HungerRate = 0.5f,
            Thirst = 0f,
            ThirstRate = 0.8f,
            BodyTemp = 38.5f
        });

        return entity;
    }
}