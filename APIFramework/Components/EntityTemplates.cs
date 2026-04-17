using APIFramework.Config;

namespace APIFramework.Core;

using APIFramework.Components;

/// <summary>
/// Factory for spawning pre-configured entities into the world.
/// All starting values come from EntityConfig (loaded from SimConfig.json).
/// Static defaults are available so the simulation always runs even without a config file.
/// </summary>
public static class EntityTemplates
{
    public static Entity SpawnHuman(EntityManager manager, EntityConfig? cfg = null)
    {
        cfg ??= EntityConfig.DefaultHuman;
        var m = cfg.Metabolism;
        var s = cfg.Stomach;
        var e = cfg.Energy;

        var entity = manager.CreateEntity();
        entity.Add(new IdentityComponent { Name = "Human" });
        entity.Add(new MetabolismComponent
        {
            Satiation                 = m.SatiationStart,
            Hydration                 = m.HydrationStart,
            BodyTemp                  = m.BodyTemp,
            SatiationDrainRate        = m.SatiationDrainRate,
            HydrationDrainRate        = m.HydrationDrainRate,
            SleepMetabolismMultiplier = m.SleepMetabolismMultiplier
        });
        entity.Add(new StomachComponent
        {
            CurrentVolumeMl = 0f,
            DigestionRate   = s.DigestionRate,
            NutritionQueued = 0f,
            HydrationQueued = 0f
        });
        entity.Add(new EnergyComponent
        {
            Energy              = e.EnergyStart,
            Sleepiness          = e.SleepinessStart,
            IsSleeping          = false,
            EnergyDrainRate     = e.EnergyDrainRate,
            SleepinessGainRate  = e.SleepinessGainRate,
            EnergyRestoreRate   = e.EnergyRestoreRate,
            SleepinessDrainRate = e.SleepinessDrainRate
        });

        return entity;
    }

    public static Entity SpawnCat(EntityManager manager, EntityConfig? cfg = null)
    {
        cfg ??= EntityConfig.DefaultCat;
        var m = cfg.Metabolism;
        var s = cfg.Stomach;
        var e = cfg.Energy;

        var entity = manager.CreateEntity();
        entity.Add(new IdentityComponent { Name = "Cat" });
        entity.Add(new MetabolismComponent
        {
            Satiation                 = m.SatiationStart,
            Hydration                 = m.HydrationStart,
            BodyTemp                  = m.BodyTemp,
            SatiationDrainRate        = m.SatiationDrainRate,
            HydrationDrainRate        = m.HydrationDrainRate,
            SleepMetabolismMultiplier = m.SleepMetabolismMultiplier
        });
        entity.Add(new StomachComponent
        {
            CurrentVolumeMl = 0f,
            DigestionRate   = s.DigestionRate,
            NutritionQueued = 0f,
            HydrationQueued = 0f
        });
        entity.Add(new EnergyComponent
        {
            Energy              = e.EnergyStart,
            Sleepiness          = e.SleepinessStart,
            IsSleeping          = false,
            EnergyDrainRate     = e.EnergyDrainRate,
            SleepinessGainRate  = e.SleepinessGainRate,
            EnergyRestoreRate   = e.EnergyRestoreRate,
            SleepinessDrainRate = e.SleepinessDrainRate
        });

        return entity;
    }
}
