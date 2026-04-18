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
        var m  = cfg.Metabolism;
        var s  = cfg.Stomach;
        var e  = cfg.Energy;
        var si = cfg.SmallIntestine;
        var li = cfg.LargeIntestine;
        var co = cfg.Colon;
        var bl = cfg.Bladder;

        var entity = manager.CreateEntity();
        entity.Add(new IdentityComponent { Name = "Human" });
        entity.Add(new MetabolismComponent
        {
            Satiation                 = m.SatiationStart,
            Hydration                 = m.HydrationStart,
            BodyTemp                  = m.BodyTemp,
            SatiationDrainRate        = m.SatiationDrainRate,
            HydrationDrainRate        = m.HydrationDrainRate,
            SleepMetabolismMultiplier = m.SleepMetabolismMultiplier,
            NutrientStores            = new NutrientProfile()   // body starts with empty stores
        });
        entity.Add(new StomachComponent
        {
            CurrentVolumeMl = 0f,
            DigestionRate   = s.DigestionRate,
            NutrientsQueued = new NutrientProfile()   // empty stomach — zeroed profile
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

        var mo = cfg.Mood;
        entity.Add(new MoodComponent
        {
            Joy          = mo.JoyStart,
            Trust        = mo.TrustStart,
            Fear         = mo.FearStart,
            Surprise     = mo.SurpriseStart,
            Sadness      = mo.SadnessStart,
            Disgust      = mo.DisgustStart,
            Anger        = mo.AngerStart,
            Anticipation = mo.AnticipationStart
        });

        // ── Digestive pipeline (small intestine → large intestine → colon) ────
        entity.Add(new SmallIntestineComponent
        {
            ChymeVolumeMl          = 0f,
            AbsorptionRate         = si.AbsorptionRate,
            Chyme                  = new NutrientProfile(),
            ResidueToLargeFraction = si.ResidueToLargeFraction
        });
        entity.Add(new LargeIntestineComponent
        {
            ContentVolumeMl       = 0f,
            WaterReabsorptionRate = li.WaterReabsorptionRate,
            MobilityRate          = li.MobilityRate,
            StoolFraction         = li.StoolFraction
        });
        entity.Add(new ColonComponent
        {
            StoolVolumeMl   = 0f,
            UrgeThresholdMl = co.UrgeThresholdMl,
            CapacityMl      = co.CapacityMl
        });
        entity.Add(new BladderComponent
        {
            VolumeML        = 0f,
            FillRate        = bl.FillRate,
            UrgeThresholdMl = bl.UrgeThresholdMl,
            CapacityMl      = bl.CapacityMl
        });

        return entity;
    }

    public static Entity SpawnCat(EntityManager manager, EntityConfig? cfg = null)
    {
        cfg ??= EntityConfig.DefaultCat;
        var m  = cfg.Metabolism;
        var s  = cfg.Stomach;
        var e  = cfg.Energy;
        var si = cfg.SmallIntestine;
        var li = cfg.LargeIntestine;
        var co = cfg.Colon;
        var bl = cfg.Bladder;

        var entity = manager.CreateEntity();
        entity.Add(new IdentityComponent { Name = "Cat" });
        entity.Add(new MetabolismComponent
        {
            Satiation                 = m.SatiationStart,
            Hydration                 = m.HydrationStart,
            BodyTemp                  = m.BodyTemp,
            SatiationDrainRate        = m.SatiationDrainRate,
            HydrationDrainRate        = m.HydrationDrainRate,
            SleepMetabolismMultiplier = m.SleepMetabolismMultiplier,
            NutrientStores            = new NutrientProfile()   // body starts with empty stores
        });
        entity.Add(new StomachComponent
        {
            CurrentVolumeMl = 0f,
            DigestionRate   = s.DigestionRate,
            NutrientsQueued = new NutrientProfile()   // empty stomach — zeroed profile
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

        var mo = cfg.Mood;
        entity.Add(new MoodComponent
        {
            Joy          = mo.JoyStart,
            Trust        = mo.TrustStart,
            Fear         = mo.FearStart,
            Surprise     = mo.SurpriseStart,
            Sadness      = mo.SadnessStart,
            Disgust      = mo.DisgustStart,
            Anger        = mo.AngerStart,
            Anticipation = mo.AnticipationStart
        });

        // ── Digestive pipeline ────────────────────────────────────────────────
        entity.Add(new SmallIntestineComponent
        {
            ChymeVolumeMl          = 0f,
            AbsorptionRate         = si.AbsorptionRate,
            Chyme                  = new NutrientProfile(),
            ResidueToLargeFraction = si.ResidueToLargeFraction
        });
        entity.Add(new LargeIntestineComponent
        {
            ContentVolumeMl       = 0f,
            WaterReabsorptionRate = li.WaterReabsorptionRate,
            MobilityRate          = li.MobilityRate,
            StoolFraction         = li.StoolFraction
        });
        entity.Add(new ColonComponent
        {
            StoolVolumeMl   = 0f,
            UrgeThresholdMl = co.UrgeThresholdMl,
            CapacityMl      = co.CapacityMl
        });
        entity.Add(new BladderComponent
        {
            VolumeML        = 0f,
            FillRate        = bl.FillRate,
            UrgeThresholdMl = bl.UrgeThresholdMl,
            CapacityMl      = bl.CapacityMl
        });

        return entity;
    }
}
