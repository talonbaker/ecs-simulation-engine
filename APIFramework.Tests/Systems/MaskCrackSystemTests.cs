using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-05: Crack fires when crackPressure >= CrackThreshold; emits MaskSlip candidate and overrides intent.
/// AT-06: SlipCooldownTicks prevents re-cracking within the window; LastSlipTick=0 means no cooldown.
/// AT-07: The dominant masked drive is reset to 0 after a crack; other drives are unchanged.
/// </summary>
public class MaskCrackSystemTests
{
    private static SocialMaskConfig DefaultCfg() => new()
    {
        CrackThreshold          = 1.50,
        LowWillpowerThreshold   = 30,
        StressCrackContribution = 0.50,
        BurnoutCrackBonus       = 0.30,
        SlipCooldownTicks       = 1800,
    };

    private static (EntityManager em, EntityRoomMembership membership,
                    NarrativeEventBus bus, Entity npc)
        Build(int currentLoad = 100, int willpowerCurrent = 0, int acuteLevel = 0,
              bool burnout = false, long lastSlipTick = 0)
    {
        var em         = new EntityManager();
        var membership = new EntityRoomMembership();
        var bus        = new NarrativeEventBus();

        var room = em.CreateEntity();
        room.Add(new RoomComponent { Id = "r1", Name = "test" });

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialMaskComponent
        {
            IrritationMask = currentLoad,
            CurrentLoad    = currentLoad,
            LastSlipTick   = lastSlipTick,
        });
        npc.Add(new WillpowerComponent(willpowerCurrent, 80));
        npc.Add(new StressComponent { AcuteLevel = acuteLevel });
        if (burnout) npc.Add(new BurningOutTag());
        membership.SetRoom(npc, room);

        return (em, membership, bus, npc);
    }

    private static List<NarrativeEventCandidate> Collect(NarrativeEventBus bus, Action tick)
    {
        var list = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += list.Add;
        tick();
        bus.OnCandidateEmitted -= list.Add;
        return list;
    }

    // -- AT-05: crack fires above threshold ------------------------------------

    [Fact]
    public void AT05_CrackPressureAboveThreshold_EmitsMaskSlipCandidate()
    {
        // pressureMask=1.0 + pressureWillpower=1.0 = 2.0 >= 1.5
        var (em, membership, bus, _) = Build(currentLoad: 100, willpowerCurrent: 0);
        var candidates = Collect(bus, () =>
            new MaskCrackSystem(membership, bus, DefaultCfg()).Update(em, 1f));

        Assert.Single(candidates);
        Assert.Equal(NarrativeEventKind.MaskSlip, candidates[0].Kind);
    }

    [Fact]
    public void AT05_CrackFires_WritesIntendedAction_WithMaskSlipContext()
    {
        var (em, membership, bus, npc) = Build(currentLoad: 100, willpowerCurrent: 0);
        new MaskCrackSystem(membership, bus, DefaultCfg()).Update(em, 1f);

        Assert.True(npc.Has<IntendedActionComponent>());
        var intent = npc.Get<IntendedActionComponent>();
        Assert.Equal(IntendedActionKind.Dialog,       intent.Kind);
        Assert.Equal(DialogContextValue.MaskSlip, intent.Context);
    }

    [Fact]
    public void AT05_CrackPressureBelowThreshold_NoCandidate()
    {
        // pressureMask=0, willpower=80 > LowWillpowerThreshold(30) → pressureWillpower=0 → total=0
        var (em, membership, bus, _) = Build(currentLoad: 0, willpowerCurrent: 80);
        var candidates = Collect(bus, () =>
            new MaskCrackSystem(membership, bus, DefaultCfg()).Update(em, 1f));

        Assert.Empty(candidates);
    }

    // -- AT-06: cooldown blocks re-crack ---------------------------------------

    [Fact]
    public void AT06_Cooldown_BlocksRecrack_WithinCooldownWindow()
    {
        var (em, membership, bus, npc) = Build(currentLoad: 100, willpowerCurrent: 0);
        var sys = new MaskCrackSystem(membership, bus, DefaultCfg());

        var first = Collect(bus, () => sys.Update(em, 1f));
        Assert.Single(first); // crack fires on tick 1

        // Restore mask so pressure stays high without cooldown
        var mask = npc.Get<SocialMaskComponent>();
        mask.IrritationMask = 100;
        mask.CurrentLoad    = 100;
        npc.Add(mask);

        var second = Collect(bus, () => sys.Update(em, 1f));
        Assert.Empty(second); // tick 2 is inside the 1800-tick cooldown
    }

    [Fact]
    public void AT06_LastSlipTick_Zero_NoCooldown_AllowsFirstCrack()
    {
        // lastSlipTick=0 sentinel means "never cracked" → cooldown check is bypassed
        var (em, membership, bus, _) = Build(currentLoad: 100, willpowerCurrent: 0, lastSlipTick: 0);
        var candidates = Collect(bus, () =>
            new MaskCrackSystem(membership, bus, DefaultCfg()).Update(em, 1f));

        Assert.Single(candidates);
    }

    // -- AT-07: dominant drive reset after crack -------------------------------

    [Fact]
    public void AT07_IrritationDominant_IsReset_OthersUnchanged()
    {
        var (em, membership, bus, npc) = Build(currentLoad: 100, willpowerCurrent: 0);
        var mask = npc.Get<SocialMaskComponent>();
        mask.AffectionMask  = 30;
        mask.AttractionMask = 20;
        npc.Add(mask);

        new MaskCrackSystem(membership, bus, DefaultCfg()).Update(em, 1f);

        var after = npc.Get<SocialMaskComponent>();
        Assert.Equal(0, after.IrritationMask);    // dominant → reset
        Assert.Equal(30, after.AffectionMask);    // unchanged
        Assert.Equal(20, after.AttractionMask);   // unchanged
    }

    [Fact]
    public void AT07_AffectionDominant_WinsOverIrritation_IsReset()
    {
        // pressureMask=0.6 + pressureWillpower=1.0 = 1.6 >= 1.5
        var (em, membership, bus, npc) = Build(currentLoad: 0, willpowerCurrent: 0);

        var mask = npc.Get<SocialMaskComponent>();
        mask.IrritationMask = 40;
        mask.AffectionMask  = 80;  // affection is dominant
        mask.CurrentLoad    = 60;
        npc.Add(mask);

        new MaskCrackSystem(membership, bus, DefaultCfg()).Update(em, 1f);

        var after = npc.Get<SocialMaskComponent>();
        Assert.Equal(0, after.AffectionMask);   // dominant → reset
        Assert.Equal(40, after.IrritationMask); // unchanged
    }
}
