using System.Collections.Generic;
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
    /// <summary>
    /// Spawns a human entity.  The optional <paramref name="spawnX"/> /
    /// <paramref name="spawnZ"/> parameters let the caller place it anywhere in
    /// the world — useful when spawning many humans at distinct grid positions.
    /// Defaults to (5, 5) — the centre of the 10×10 world.
    /// </summary>
    public static Entity SpawnHuman(EntityManager manager, EntityConfig? cfg = null,
                                    float spawnX = 5f, float spawnZ = 5f,
                                    string? name = null)
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
        entity.Add(new IdentityComponent { Name = name ?? "Human" });
        entity.Add(new HumanTag());
        entity.Add(new PositionComponent  { X = spawnX, Y = 0f, Z = spawnZ });
        entity.Add(new MovementComponent  { Speed = 0.04f, ArrivalDistance = 0.4f });
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

    /// <summary>
    /// Adds the four social components plus NpcTag to <paramref name="entity"/>.
    /// Used by tests and (later) the cast generator. Any field not supplied gets
    /// a sensible zero/default — overwrite after the call for specific test cases.
    /// </summary>
    public static Entity WithSocial(
        Entity entity,
        SocialDrivesComponent? drives           = null,
        WillpowerComponent?    willpower         = null,
        PersonalityComponent?  personality       = null,
        InhibitionsComponent?  inhibitions       = null)
    {
        entity.Add(new NpcTag());
        entity.Add(drives      ?? new SocialDrivesComponent());
        entity.Add(willpower   ?? new WillpowerComponent(50, 50));
        entity.Add(personality ?? new PersonalityComponent(0, 0, 0, 0, 0));
        entity.Add(inhibitions ?? new InhibitionsComponent(new List<Inhibition>()));
        return entity;
    }

    /// <summary>
    /// Spawns a room entity with <see cref="RoomTag"/> and <see cref="RoomComponent"/>.
    /// Also adds a <see cref="PositionComponent"/> at the center of the bounds so the
    /// spatial index can register the room as a positioned entity.
    /// Illumination is optional — the lighting engine populates it in WP-1.2.A.
    /// </summary>
    public static Entity Room(
        EntityManager    manager,
        string           id,
        string           name,
        RoomCategory     category,
        BuildingFloor    floor,
        BoundsRect       bounds,
        RoomIllumination illumination = default)
    {
        var entity = manager.CreateEntity();
        entity.Add(new RoomTag());
        entity.Add(new RoomComponent
        {
            Id           = id,
            Name         = name,
            Category     = category,
            Floor        = floor,
            Bounds       = bounds,
            Illumination = illumination,
        });
        // Center of bounds for spatial registration (float midpoint)
        entity.Add(new PositionComponent
        {
            X = bounds.X + bounds.Width  * 0.5f,
            Y = 0f,
            Z = bounds.Y + bounds.Height * 0.5f,
        });
        return entity;
    }

    /// <summary>
    /// Adds a <see cref="ProximityComponent"/> to an existing entity.
    /// Defaults match <see cref="ProximityComponent.Default"/> (2/8/32 tiles).
    /// </summary>
    public static Entity WithProximity(
        Entity entity,
        int conversationTiles = 2,
        int awarenessTiles    = 8,
        int sightTiles        = 32)
    {
        entity.Add(new ProximityComponent
        {
            ConversationRangeTiles = conversationTiles,
            AwarenessRangeTiles    = awarenessTiles,
            SightRangeTiles        = sightTiles,
        });
        return entity;
    }

    /// <summary>
    /// Spawns a light fixture entity with <see cref="LightSourceTag"/> and <see cref="LightSourceComponent"/>.
    /// Also adds a <see cref="PositionComponent"/> at (tileX, tileY) for spatial registration.
    /// </summary>
    public static Entity LightSource(
        EntityManager manager,
        string        id,
        LightKind     kind,
        LightState    state,
        int           intensity,
        int           colorTemperatureK,
        int           tileX,
        int           tileY,
        string        roomId)
    {
        var entity = manager.CreateEntity();
        entity.Add(new LightSourceTag());
        entity.Add(new LightSourceComponent
        {
            Id                = id,
            Kind              = kind,
            State             = state,
            Intensity         = intensity,
            ColorTemperatureK = colorTemperatureK,
            TileX             = tileX,
            TileY             = tileY,
            RoomId            = roomId,
        });
        entity.Add(new PositionComponent { X = tileX, Y = 0f, Z = tileY });
        return entity;
    }

    /// <summary>
    /// Spawns a light aperture (window/skylight) entity with <see cref="LightApertureTag"/>
    /// and <see cref="LightApertureComponent"/>.
    /// Also adds a <see cref="PositionComponent"/> at (tileX, tileY) for spatial registration.
    /// </summary>
    public static Entity LightAperture(
        EntityManager  manager,
        string         id,
        int            tileX,
        int            tileY,
        string         roomId,
        ApertureFacing facing,
        double         areaSqTiles)
    {
        var entity = manager.CreateEntity();
        entity.Add(new LightApertureTag());
        entity.Add(new LightApertureComponent
        {
            Id          = id,
            TileX       = tileX,
            TileY       = tileY,
            RoomId      = roomId,
            Facing      = facing,
            AreaSqTiles = areaSqTiles,
        });
        entity.Add(new PositionComponent { X = tileX, Y = 0f, Z = tileY });
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
        entity.Add(new CatTag());
        entity.Add(new PositionComponent  { X = 3f, Y = 0f, Z = 4f });
        entity.Add(new MovementComponent  { Speed = 0.06f, ArrivalDistance = 0.4f });
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
