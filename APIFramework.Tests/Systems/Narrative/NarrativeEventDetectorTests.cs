using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Narrative;

/// <summary>
/// Unit tests for NarrativeEventDetector: AT-01 through AT-07.
/// </summary>
public class NarrativeEventDetectorTests
{
    // -- Helpers ---------------------------------------------------------------

    private static NarrativeConfig DefaultCfg() => new()
    {
        DriveSpikeThreshold       = 15,
        WillpowerDropThreshold    = 10,
        WillpowerLowThreshold     = 20,
        AbruptDepartureWindowTicks = 3,
        CandidateDetailMaxLength   = 280,
    };

    private static (EntityManager em,
                    NarrativeEventBus bus,
                    ProximityEventBus proxBus,
                    EntityRoomMembership membership,
                    NarrativeEventDetector detector)
        Build(NarrativeConfig? cfg = null)
    {
        var em         = new EntityManager();
        var bus        = new NarrativeEventBus();
        var proxBus    = new ProximityEventBus();
        var membership = new EntityRoomMembership();
        var detector   = new NarrativeEventDetector(bus, proxBus, membership, cfg ?? DefaultCfg());
        return (em, bus, proxBus, membership, detector);
    }

    private static Entity SpawnNpc(EntityManager em,
                                   SocialDrivesComponent? drives    = null,
                                   WillpowerComponent?    willpower = null)
    {
        var e = em.CreateEntity();
        e.Add(new NpcTag());
        e.Add(drives    ?? new SocialDrivesComponent());
        e.Add(willpower ?? new WillpowerComponent(50, 50));
        return e;
    }

    private static List<NarrativeEventCandidate> Collect(
        NarrativeEventBus bus, System.Action tick)
    {
        var list = new List<NarrativeEventCandidate>();
        bus.OnCandidateEmitted += list.Add;
        tick();
        bus.OnCandidateEmitted -= list.Add;
        return list;
    }

    // -- AT-01: DriveSpike emitted when drive changes >= threshold -------------

    [Fact]
    public void DriveSpike_AboveThreshold_EmitsCandidate()
    {
        var (em, bus, _, _, detector) = Build();
        var npc = SpawnNpc(em, drives: new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = 30, Baseline = 30 }
        });

        // Tick 1 — prime the previous-value cache
        detector.Update(em, 1f);

        // Simulate a drive spike: jump irritation by 20 points
        var drives = npc.Get<SocialDrivesComponent>();
        drives.Irritation.Current = 50;
        npc.Add(drives);

        // Tick 2 — should detect the spike
        var candidates = Collect(bus, () => detector.Update(em, 1f));

        Assert.Single(candidates);
        var c = candidates[0];
        Assert.Equal(NarrativeEventKind.DriveSpike, c.Kind);
        Assert.Contains(NarrativeEventDetector.EntityIntId(npc), c.ParticipantIds);
        Assert.Contains("30", c.Detail);   // before value
        Assert.Contains("50", c.Detail);   // after value
        Assert.Contains("+20", c.Detail);  // delta
    }

    // -- AT-02: No candidate when drive changes < threshold --------------------

    [Fact]
    public void DriveSpike_BelowThreshold_NoCandidate()
    {
        var (em, bus, _, _, detector) = Build();
        var npc = SpawnNpc(em, drives: new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = 30, Baseline = 30 }
        });

        detector.Update(em, 1f);  // prime

        var drives = npc.Get<SocialDrivesComponent>();
        drives.Irritation.Current = 35;  // delta = +5, below 15-point threshold
        npc.Add(drives);

        var candidates = Collect(bus, () => detector.Update(em, 1f));
        Assert.Empty(candidates);
    }

    // -- AT-03: WillpowerCollapse when willpower drops >= threshold ------------

    [Fact]
    public void WillpowerCollapse_DropAboveThreshold_EmitsCandidate()
    {
        var (em, bus, _, _, detector) = Build();
        var npc = SpawnNpc(em, willpower: new WillpowerComponent(45, 50));

        detector.Update(em, 1f);  // prime: prev willpower = 45

        var wp = npc.Get<WillpowerComponent>();
        wp.Current = 12;  // drop of 33 >= 10
        npc.Add(wp);

        var candidates = Collect(bus, () => detector.Update(em, 1f));

        Assert.Single(candidates, c => c.Kind == NarrativeEventKind.WillpowerCollapse);
        var c = candidates.First(c => c.Kind == NarrativeEventKind.WillpowerCollapse);
        Assert.Contains("45", c.Detail);
        Assert.Contains("12", c.Detail);
    }

    // -- AT-04: WillpowerLow fires on first crossing; not on subsequent ticks --

    [Fact]
    public void WillpowerLow_FirstCrossing_EmitsThenSuppresses()
    {
        var cfg = DefaultCfg();  // threshold = 20
        var (em, bus, _, _, detector) = Build(cfg);
        var npc = SpawnNpc(em, willpower: new WillpowerComponent(25, 50));

        detector.Update(em, 1f);  // prime: prev = 25 (above 20)

        // Cross below threshold
        var wp = npc.Get<WillpowerComponent>();
        wp.Current = 18;
        npc.Add(wp);

        var tick2 = Collect(bus, () => detector.Update(em, 1f));
        Assert.Contains(tick2, c => c.Kind == NarrativeEventKind.WillpowerLow);

        // Subsequent tick still below — no re-emit
        var tick3 = Collect(bus, () => detector.Update(em, 1f));
        Assert.DoesNotContain(tick3, c => c.Kind == NarrativeEventKind.WillpowerLow);
    }

    // -- AT-05: WillpowerLow re-emits after rising back above threshold --------

    [Fact]
    public void WillpowerLow_ReEmits_AfterRisingAboveThreshold()
    {
        var (em, bus, _, _, detector) = Build();
        var npc = SpawnNpc(em, willpower: new WillpowerComponent(25, 50));

        detector.Update(em, 1f);  // prime: prev = 25

        // First crossing below 20
        var wp = npc.Get<WillpowerComponent>();
        wp.Current = 18;
        npc.Add(wp);
        var tick2 = Collect(bus, () => detector.Update(em, 1f));
        Assert.Contains(tick2, c => c.Kind == NarrativeEventKind.WillpowerLow);

        // Rise back above threshold
        wp = npc.Get<WillpowerComponent>();
        wp.Current = 30;
        npc.Add(wp);
        detector.Update(em, 1f);  // no WillpowerLow (crossed from below to above)

        // Cross below again
        wp = npc.Get<WillpowerComponent>();
        wp.Current = 15;
        npc.Add(wp);
        var tick4 = Collect(bus, () => detector.Update(em, 1f));
        Assert.Contains(tick4, c => c.Kind == NarrativeEventKind.WillpowerLow);
    }

    // -- AT-06: ConversationStarted on EnteredConversationRange ---------------

    [Fact]
    public void ConversationStarted_OnEnteredConversationRange()
    {
        var (em, bus, proxBus, _, detector) = Build();

        var obs    = em.CreateEntity();
        var target = em.CreateEntity();

        // Fire the proximity event (Spatial phase would do this; we do it manually)
        proxBus.RaiseEnteredConversationRange(
            new ProximityEnteredConversationRange(obs, target, Tick: 1));

        var candidates = Collect(bus, () => detector.Update(em, 1f));

        Assert.Single(candidates);
        Assert.Equal(NarrativeEventKind.ConversationStarted, candidates[0].Kind);
        Assert.Equal(2, candidates[0].ParticipantIds.Count);
    }

    // -- AT-07: LeftRoomAbruptly within window of DriveSpike ------------------

    [Fact]
    public void LeftRoomAbruptly_WithinWindow_EmitsCandidate()
    {
        var (em, bus, proxBus, _, detector) = Build();
        var npc = SpawnNpc(em, drives: new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = 30, Baseline = 30 }
        });

        detector.Update(em, 1f);  // prime

        // Tick 2: drive spike (irritation +20) AND room membership change
        var drives = npc.Get<SocialDrivesComponent>();
        drives.Irritation.Current = 50;
        npc.Add(drives);

        var room = em.CreateEntity();
        room.Add(new IdentityComponent("breakroom"));
        room.Add(new RoomTag());

        // Fire room membership changed: NPC left the breakroom
        proxBus.RaiseRoomMembershipChanged(
            new RoomMembershipChanged(npc, OldRoom: room, NewRoom: null, Tick: 2));

        var candidates = Collect(bus, () => detector.Update(em, 1f));

        Assert.Contains(candidates, c => c.Kind == NarrativeEventKind.DriveSpike);
        Assert.Contains(candidates, c => c.Kind == NarrativeEventKind.LeftRoomAbruptly);

        var abrupt = candidates.First(c => c.Kind == NarrativeEventKind.LeftRoomAbruptly);
        Assert.Contains(NarrativeEventDetector.EntityIntId(npc), abrupt.ParticipantIds);
    }

    [Fact]
    public void LeftRoomAbruptly_OutsideWindow_NoCandidate()
    {
        var (em, bus, proxBus, _, detector) = Build();
        var npc = SpawnNpc(em, drives: new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = 30, Baseline = 30 }
        });

        detector.Update(em, 1f);  // tick 1: prime

        // Tick 2: drive spike
        var drives = npc.Get<SocialDrivesComponent>();
        drives.Irritation.Current = 50;
        npc.Add(drives);
        detector.Update(em, 1f);  // tick 2: spike emitted, lastSpike = 2

        // Ticks 3, 4, 5: no spikes (drives stable)
        drives = npc.Get<SocialDrivesComponent>();
        drives.Irritation.Current = 50;  // unchanged
        npc.Add(drives);
        detector.Update(em, 1f);
        detector.Update(em, 1f);
        detector.Update(em, 1f);
        // Now at tick 5; lastSpike was tick 2; tick5 - 2 = 3 → still within window

        // Tick 6: 6 - 2 = 4 > 3 (window), so LeftRoomAbruptly should NOT fire
        detector.Update(em, 1f);

        var room = em.CreateEntity();
        room.Add(new IdentityComponent("hallway"));
        room.Add(new RoomTag());
        proxBus.RaiseRoomMembershipChanged(
            new RoomMembershipChanged(npc, OldRoom: room, NewRoom: null, Tick: 7));

        var candidates = Collect(bus, () => detector.Update(em, 1f));  // tick 7
        Assert.DoesNotContain(candidates, c => c.Kind == NarrativeEventKind.LeftRoomAbruptly);
    }
}
