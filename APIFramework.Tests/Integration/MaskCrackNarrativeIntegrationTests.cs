using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Integration;

/// <summary>
/// AT-05 integration: MaskCrackSystem fires a MaskSlip NarrativeEventCandidate on the bus
/// and writes IntendedActionComponent(Dialog, MaskSlip) on the cracking NPC in the same tick.
/// </summary>
public class MaskCrackNarrativeIntegrationTests
{
    private static SocialMaskConfig LowThresholdCfg() => new()
    {
        CrackThreshold        = 0.5,
        LowWillpowerThreshold = 30,
        SlipCooldownTicks     = 1800,
    };

    private static List<NarrativeEventCandidate> Collect(NarrativeEventBus bus, Action tick)
    {
        var list = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += list.Add;
        tick();
        bus.OnCandidateEmitted -= list.Add;
        return list;
    }

    [Fact]
    public void AT05_Crack_EmitsMaskSlipCandidate_OnBus()
    {
        var em         = new EntityManager();
        var membership = new EntityRoomMembership();
        var bus        = new NarrativeEventBus();

        // NPC alone, willpower=0 → pressureWillpower=1.0 >= CrackThreshold(0.5)
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialMaskComponent { IrritationMask = 60, CurrentLoad = 60 });
        npc.Add(new WillpowerComponent(0, 80));
        npc.Add(new StressComponent());

        var sys        = new MaskCrackSystem(membership, bus, LowThresholdCfg());
        var candidates = Collect(bus, () => sys.Update(em, 1f));

        Assert.Single(candidates);
        Assert.Equal(NarrativeEventKind.MaskSlip, candidates[0].Kind);
        Assert.NotEmpty(candidates[0].ParticipantIds);
        Assert.NotNull(candidates[0].Detail);
    }

    [Fact]
    public void AT05_Crack_SetsIntendedAction_DialogMaskSlip_SameTick()
    {
        var em         = new EntityManager();
        var membership = new EntityRoomMembership();
        var bus        = new NarrativeEventBus();

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialMaskComponent { IrritationMask = 60, CurrentLoad = 60 });
        npc.Add(new WillpowerComponent(0, 80));
        npc.Add(new StressComponent());

        new MaskCrackSystem(membership, bus, LowThresholdCfg()).Update(em, 1f);

        Assert.True(npc.Has<IntendedActionComponent>());
        var intent = npc.Get<IntendedActionComponent>();
        Assert.Equal(IntendedActionKind.Dialog,       intent.Kind);
        Assert.Equal(DialogContextValue.MaskSlip, intent.Context);
        Assert.True(intent.IntensityHint > 0);
    }

    [Fact]
    public void AT05_Crack_WithObserver_ObserverInParticipantList()
    {
        var em         = new EntityManager();
        var membership = new EntityRoomMembership();
        var bus        = new NarrativeEventBus();

        var room = em.CreateEntity();
        room.Add(new RoomComponent { Id = "r1", Name = "test" });

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialMaskComponent { IrritationMask = 60, CurrentLoad = 60 });
        npc.Add(new WillpowerComponent(0, 80));
        npc.Add(new StressComponent());
        membership.SetRoom(npc, room);

        var observer = em.CreateEntity();
        observer.Add(new NpcTag());
        observer.Add(new SocialMaskComponent());
        observer.Add(new WillpowerComponent(80, 80)); // high willpower → does not crack
        observer.Add(new StressComponent());
        membership.SetRoom(observer, room);

        var candidates = Collect(bus, () =>
            new MaskCrackSystem(membership, bus, LowThresholdCfg()).Update(em, 1f));

        Assert.Single(candidates);
        Assert.Equal(2, candidates[0].ParticipantIds.Count);
        Assert.Equal(WillpowerSystem.EntityIntId(npc),      candidates[0].ParticipantIds[0]);
        Assert.Equal(WillpowerSystem.EntityIntId(observer), candidates[0].ParticipantIds[1]);
    }

    [Fact]
    public void AT05_Crack_IntendedAction_TargetsFirstObserver()
    {
        var em         = new EntityManager();
        var membership = new EntityRoomMembership();
        var bus        = new NarrativeEventBus();

        var room = em.CreateEntity();
        room.Add(new RoomComponent { Id = "r1", Name = "test" });

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialMaskComponent { IrritationMask = 60, CurrentLoad = 60 });
        npc.Add(new WillpowerComponent(0, 80));
        npc.Add(new StressComponent());
        membership.SetRoom(npc, room);

        var observer = em.CreateEntity();
        observer.Add(new NpcTag());
        observer.Add(new SocialMaskComponent());
        observer.Add(new WillpowerComponent(80, 80));
        observer.Add(new StressComponent());
        membership.SetRoom(observer, room);

        new MaskCrackSystem(membership, bus, LowThresholdCfg()).Update(em, 1f);

        var intent = npc.Get<IntendedActionComponent>();
        Assert.Equal(WillpowerSystem.EntityIntId(observer), intent.TargetEntityId);
    }
}
