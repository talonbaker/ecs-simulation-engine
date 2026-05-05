using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-02: Mask grows when a drive is elevated and the NPC is in a high-exposure context.
/// AT-03: Mask decays when the NPC is alone in a dark room (low-exposure).
/// AT-04: High Conscientiousness amplifies growth; high Extraversion suppresses it.
/// </summary>
public class SocialMaskSystemTests
{
    private static SocialMaskConfig FastGrowthCfg() => new()
    {
        MaskGainPerTick      = 1.0,
        MaskDecayPerTick     = 1.0,
        LowExposureThreshold = 0.30,
    };

    private static (EntityManager em, EntityRoomMembership membership, Entity room)
        BuildRoom(int illumination = 100)
    {
        var em   = new EntityManager();
        var room = em.CreateEntity();
        room.Add(new RoomComponent
        {
            Id           = "test-room",
            Name         = "test-room",
            Illumination = new RoomIllumination(illumination, 5000, null),
        });
        return (em, new EntityRoomMembership(), room);
    }

    private static Entity AddNpc(EntityManager em, EntityRoomMembership membership,
        Entity room, int irritation = 0, int conscientiousness = 0, int extraversion = 0)
    {
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialMaskComponent { Baseline = 30 });
        npc.Add(new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = irritation, Baseline = 50 },
        });
        npc.Add(new PersonalityComponent(0, conscientiousness, extraversion, 0, 0));
        membership.SetRoom(npc, room);
        return npc;
    }

    // -- AT-02: elevated drive + high exposure → mask grows --------------------

    [Fact]
    public void AT02_ElevatedDrive_HighExposure_IrritationMaskGrows()
    {
        var (em, mem, room) = BuildRoom(illumination: 100);
        var npc = AddNpc(em, mem, room, irritation: 100);
        for (int i = 0; i < 4; i++) AddNpc(em, mem, room); // 4 peers for full nearbyCount

        var sys = new SocialMaskSystem(mem, FastGrowthCfg());
        sys.Update(em, 1f);
        sys.Update(em, 1f);

        Assert.True(npc.Get<SocialMaskComponent>().IrritationMask > 0,
            "IrritationMask should grow when drive=100 and exposure is maximal");
    }

    [Fact]
    public void AT02_DriveAtBaseline_MaskDoesNotGrow()
    {
        var (em, mem, room) = BuildRoom(illumination: 100);
        var npc = AddNpc(em, mem, room, irritation: 50); // driveLoad = max(0, 50-50)/50 = 0
        AddNpc(em, mem, room); // peer for exposure

        var before = npc.Get<SocialMaskComponent>().IrritationMask;
        new SocialMaskSystem(mem, FastGrowthCfg()).Update(em, 1f);

        Assert.Equal(before, npc.Get<SocialMaskComponent>().IrritationMask);
    }

    // -- AT-03: low exposure → mask decays -------------------------------------

    [Fact]
    public void AT03_LowExposure_Alone_DarkRoom_MaskDecays()
    {
        var (em, mem, room) = BuildRoom(illumination: 0); // pitch dark, no peers
        var npc = AddNpc(em, mem, room);

        var mask = npc.Get<SocialMaskComponent>();
        mask.IrritationMask = 10;
        npc.Add(mask);

        new SocialMaskSystem(mem, FastGrowthCfg()).Update(em, 1f);

        Assert.True(npc.Get<SocialMaskComponent>().IrritationMask < 10,
            "Mask should decay when alone in a dark room (exposure below LowExposureThreshold)");
    }

    [Fact]
    public void AT03_HighExposure_MaskDoesNotDecay()
    {
        var (em, mem, room) = BuildRoom(illumination: 100);
        var npc = AddNpc(em, mem, room);
        var mask = npc.Get<SocialMaskComponent>();
        mask.IrritationMask = 10;
        npc.Add(mask);

        AddNpc(em, mem, room); // peer pushes exposure above threshold

        new SocialMaskSystem(mem, FastGrowthCfg()).Update(em, 1f);

        Assert.Equal(10, npc.Get<SocialMaskComponent>().IrritationMask);
    }

    // -- AT-04: personality bias scales growth ---------------------------------

    [Fact]
    public void AT04_HighConscientiousness_BuildsMaskFasterThanLow()
    {
        var (em, mem, room) = BuildRoom(illumination: 100);
        var highC = AddNpc(em, mem, room, irritation: 100, conscientiousness:  2);
        var lowC  = AddNpc(em, mem, room, irritation: 100, conscientiousness: -2);
        AddNpc(em, mem, room);
        AddNpc(em, mem, room);

        var sys = new SocialMaskSystem(mem, FastGrowthCfg());
        for (int i = 0; i < 10; i++) sys.Update(em, 1f);

        int highCMask = highC.Get<SocialMaskComponent>().IrritationMask;
        int lowCMask  = lowC.Get<SocialMaskComponent>().IrritationMask;

        Assert.True(highCMask > lowCMask,
            $"High-C NPC should accumulate more mask (got {highCMask} vs {lowCMask})");
    }

    [Fact]
    public void AT04_HighExtraversion_SuppressesMaskGrowth()
    {
        var (em, mem, room) = BuildRoom(illumination: 100);
        var highE = AddNpc(em, mem, room, irritation: 100, extraversion:  2);
        var lowE  = AddNpc(em, mem, room, irritation: 100, extraversion: -2);
        AddNpc(em, mem, room);
        AddNpc(em, mem, room);

        var sys = new SocialMaskSystem(mem, FastGrowthCfg());
        for (int i = 0; i < 10; i++) sys.Update(em, 1f);

        int highEMask = highE.Get<SocialMaskComponent>().IrritationMask;
        int lowEMask  = lowE.Get<SocialMaskComponent>().IrritationMask;

        Assert.True(highEMask < lowEMask,
            $"High-E NPC should accumulate less mask (got {highEMask} vs {lowEMask})");
    }
}
