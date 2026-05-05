using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Lighting;
using Xunit;

namespace APIFramework.Tests.Systems.Lighting;

/// <summary>
/// AT-09: Empty room → AmbientLevel == 0.
/// AT-10: Room with one On source at intensity 80 → AmbientLevel within falloff range of 80.
/// AT-11: DominantSourceId is the highest-contribution source in a multi-source room.
/// AT-12: Color temperature is intensity-weighted across multiple sources.
/// </summary>
public class IlluminationAccumulationSystemTests
{
    private static LightingConfig DefaultCfg() => new();

    private static (EntityManager em, IlluminationAccumulationSystem sys,
                    LightSourceStateSystem statesSys, ApertureBeamSystem beamSys,
                    SunStateService sunService)
        Setup(int seed = 42)
    {
        var em        = new EntityManager();
        var rng       = new SeededRandom(seed);
        var cfg       = DefaultCfg();
        var clock     = new SimulationClock();
        var sunSvc    = new SunStateService();
        var statesSys = new LightSourceStateSystem(rng, cfg);
        var beamSys   = new ApertureBeamSystem(sunSvc, clock);
        var illumSys  = new IlluminationAccumulationSystem(statesSys, beamSys, cfg);
        return (em, illumSys, statesSys, beamSys, sunSvc);
    }

    private static Entity SpawnRoom(EntityManager em, string id = "r1",
                                    int x = 0, int y = 0, int w = 10, int h = 10)
    {
        return EntityTemplates.Room(em, id, "Office", RoomCategory.Office, BuildingFloor.First,
            new BoundsRect(x, y, w, h));
    }

    private static Entity SpawnSource(EntityManager em, string roomId, int tileX, int tileY,
                                      LightState state = LightState.On, int intensity = 80,
                                      int colorK = 4000, string? id = null)
    {
        return EntityTemplates.LightSource(em,
            id: id ?? System.Guid.NewGuid().ToString(),
            kind: LightKind.OverheadFluorescent,
            state: state,
            intensity: intensity,
            colorTemperatureK: colorK,
            tileX: tileX, tileY: tileY,
            roomId: roomId);
    }

    private static Entity SpawnAperture(EntityManager em, string roomId, int tileX, int tileY,
                                        ApertureFacing facing = ApertureFacing.South,
                                        double area = 4.0, string? id = null)
    {
        return EntityTemplates.LightAperture(em,
            id: id ?? System.Guid.NewGuid().ToString(),
            tileX: tileX, tileY: tileY,
            roomId: roomId, facing: facing, areaSqTiles: area);
    }

    // -- AT-09: Empty room -----------------------------------------------------

    [Fact]
    public void EmptyRoom_AmbientLevel_IsZero()
    {
        var (em, sys, statesSys, beamSys, _) = Setup();
        var room = SpawnRoom(em);

        statesSys.Update(em, 1f);
        beamSys.Update(em, 1f);
        sys.Update(em, 1f);

        var illum = room.Get<RoomComponent>().Illumination;
        Assert.Equal(0, illum.AmbientLevel);
    }

    // -- AT-10: On source at center --------------------------------------------

    [Fact]
    public void RoomWithOnSource_AtCenter_AmbientLevelNear80()
    {
        var (em, sys, statesSys, beamSys, _) = Setup();
        // 10×10 room; center at (5, 5). Source at center → dist=0 → falloff=1.0
        SpawnRoom(em, x: 0, y: 0, w: 10, h: 10);
        SpawnSource(em, roomId: "r1", tileX: 5, tileY: 5, intensity: 80);

        statesSys.Update(em, 1f);
        beamSys.Update(em, 1f);
        sys.Update(em, 1f);

        // Room entity is the first RoomTag query result
        var room  = em.Query<RoomTag>().First().Get<RoomComponent>();
        Assert.InRange(room.Illumination.AmbientLevel, 70, 100);
    }

    [Fact]
    public void RoomWithOffSource_AmbientLevel_IsZero()
    {
        var (em, sys, statesSys, beamSys, _) = Setup();
        SpawnRoom(em);
        SpawnSource(em, roomId: "r1", tileX: 5, tileY: 5, state: LightState.Off, intensity: 0);

        statesSys.Update(em, 1f);
        beamSys.Update(em, 1f);
        sys.Update(em, 1f);

        var illum = em.Query<RoomTag>().First().Get<RoomComponent>().Illumination;
        Assert.Equal(0, illum.AmbientLevel);
    }

    [Fact]
    public void RoomWithFlickeringSource_AmbientLevel_Varies()
    {
        var (em, sys, statesSys, beamSys, _) = Setup(seed: 11);
        SpawnRoom(em);
        SpawnSource(em, roomId: "r1", tileX: 5, tileY: 5, state: LightState.Flickering, intensity: 80);

        bool sawOn  = false;
        bool sawOff = false;
        const int ticks = 500;

        for (int i = 0; i < ticks; i++)
        {
            statesSys.Update(em, 1f);
            beamSys.Update(em, 1f);
            sys.Update(em, 1f);

            int amb = em.Query<RoomTag>().First().Get<RoomComponent>().Illumination.AmbientLevel;
            if (amb > 0) sawOn  = true;
            else         sawOff = true;
        }

        Assert.True(sawOn,  "Flickering source never produced ambient > 0 in 500 ticks");
        Assert.True(sawOff, "Flickering source never produced ambient == 0 in 500 ticks");
    }

    // -- AT-11: DominantSourceId -----------------------------------------------

    [Fact]
    public void MultiSourceRoom_DominantSourceId_IsHighestContributor()
    {
        var (em, sys, statesSys, beamSys, _) = Setup();
        SpawnRoom(em, x: 0, y: 0, w: 10, h: 10);

        // Place two sources: bright one at center, dim one far from center
        SpawnSource(em, "r1", tileX: 5, tileY: 5, intensity: 80, id: "bright-src");
        SpawnSource(em, "r1", tileX: 0, tileY: 0, intensity: 10, id: "dim-src");

        statesSys.Update(em, 1f);
        beamSys.Update(em, 1f);
        sys.Update(em, 1f);

        var illum = em.Query<RoomTag>().First().Get<RoomComponent>().Illumination;
        Assert.Equal("bright-src", illum.DominantSourceId);
    }

    // -- AT-12: Color temperature is intensity-weighted -------------------------

    [Fact]
    public void MultiSourceRoom_ColorTemperature_IsIntensityWeighted()
    {
        var (em, sys, statesSys, beamSys, _) = Setup();
        SpawnRoom(em, x: 0, y: 0, w: 10, h: 10);

        // Both at center (tileX=5, tileY=5) for full falloff contribution
        SpawnSource(em, "r1", tileX: 5, tileY: 5, intensity: 80, colorK: 4000, id: "s1");
        SpawnSource(em, "r1", tileX: 5, tileY: 5, intensity: 40, colorK: 6000, id: "s2");

        statesSys.Update(em, 1f);
        beamSys.Update(em, 1f);
        sys.Update(em, 1f);

        // Weighted avg = (80*4000 + 40*6000) / (80+40) = (320000 + 240000) / 120 ≈ 4666K
        var illum = em.Query<RoomTag>().First().Get<RoomComponent>().Illumination;
        int expected = (80 * 4000 + 40 * 6000) / (80 + 40);
        Assert.InRange(illum.ColorTemperatureK, expected - 50, expected + 50);
    }

    // -- Aperture contribution -------------------------------------------------

    [Fact]
    public void RoomWithSouthApertureAndNoonSun_AmbientElevated()
    {
        var (em, sys, statesSys, beamSys, sunSvc) = Setup();
        SpawnRoom(em, x: 0, y: 0, w: 10, h: 10);
        // Window on south wall at y=0
        SpawnAperture(em, "r1", tileX: 5, tileY: 0, facing: ApertureFacing.South, area: 4.0);

        // Inject noon sun state
        sunSvc.UpdateSunState(new SunStateRecord(AzimuthDeg: 180.0, ElevationDeg: 90.0, DayPhase.Afternoon));

        statesSys.Update(em, 1f);
        beamSys.Update(em, 1f);
        sys.Update(em, 1f);

        var illum = em.Query<RoomTag>().First().Get<RoomComponent>().Illumination;
        Assert.True(illum.AmbientLevel > 0,
            $"Expected aperture to elevate ambient above 0 at noon; got {illum.AmbientLevel}");
    }

    [Fact]
    public void RoomWithApertureAndNightSun_AmbientRemains0()
    {
        var (em, sys, statesSys, beamSys, sunSvc) = Setup();
        SpawnRoom(em);
        SpawnAperture(em, "r1", tileX: 5, tileY: 0, facing: ApertureFacing.South);

        // Inject night sun (elevation < 0 → no beam)
        sunSvc.UpdateSunState(new SunStateRecord(AzimuthDeg: 0.0, ElevationDeg: -45.0, DayPhase.Night));

        statesSys.Update(em, 1f);
        beamSys.Update(em, 1f);
        sys.Update(em, 1f);

        var illum = em.Query<RoomTag>().First().Get<RoomComponent>().Illumination;
        Assert.Equal(0, illum.AmbientLevel);
    }

    // -- Source outside range has zero contribution ----------------------------

    [Fact]
    public void SourceFarFromRoomCenter_ZeroContribution()
    {
        var (em, sys, statesSys, beamSys, _) = Setup();
        // Room from (0,0) to (10,10); center at (5,5). sourceRangeBase=3.
        // Source at (0,0) → dist≈7.07 > 3 → falloff=0
        SpawnRoom(em, x: 0, y: 0, w: 10, h: 10);
        SpawnSource(em, "r1", tileX: 0, tileY: 0, intensity: 80);

        statesSys.Update(em, 1f);
        beamSys.Update(em, 1f);
        sys.Update(em, 1f);

        var illum = em.Query<RoomTag>().First().Get<RoomComponent>().Illumination;
        Assert.Equal(0, illum.AmbientLevel);
        Assert.Null(illum.DominantSourceId);
    }
}
