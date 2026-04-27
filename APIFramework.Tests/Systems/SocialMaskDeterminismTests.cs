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
/// AT-10: Two runs with identical entity setup produce a byte-identical MaskSlip candidate stream
/// over 5000 ticks. Verifies that SocialMaskSystem and MaskCrackSystem are fully deterministic.
/// </summary>
public class SocialMaskDeterminismTests
{
    [Fact]
    public void AT10_TwoRuns_SameSetup_ByteIdenticalMaskSlipStream()
    {
        var run1 = RunMaskSimulation(5000);
        var run2 = RunMaskSimulation(5000);

        Assert.True(run1.Count > 0, "Expected at least one MaskSlip crack in 5000 ticks");
        Assert.Equal(run1.Count, run2.Count);
        for (int i = 0; i < run1.Count; i++)
            Assert.True(run1[i] == run2[i],
                $"Candidate {i} diverged:\n  run1: {run1[i]}\n  run2: {run2[i]}");
    }

    private static List<string> RunMaskSimulation(int ticks)
    {
        var em         = new EntityManager();
        var membership = new EntityRoomMembership();
        var bus        = new NarrativeEventBus();

        var cfg = new SocialMaskConfig
        {
            MaskGainPerTick       = 5.0,    // fast buildup to guarantee cracks in 5000 ticks
            MaskDecayPerTick      = 0.1,
            LowExposureThreshold  = 0.10,
            CrackThreshold        = 0.5,    // low threshold to produce events for signal
            LowWillpowerThreshold = 30,
            SlipCooldownTicks     = 50,     // short cooldown so multiple cracks appear
        };

        var room = em.CreateEntity();
        room.Add(new RoomComponent
        {
            Id           = "main",
            Name         = "main",
            Illumination = new RoomIllumination(80, 5000, null),
        });

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialMaskComponent { Baseline = 30 });
        npc.Add(new SocialDrivesComponent
        {
            Irritation = new DriveValue { Current = 90, Baseline = 50 },
            Affection  = new DriveValue { Current = 75, Baseline = 50 },
        });
        npc.Add(new PersonalityComponent(0, 1, 0, 0, 0));
        npc.Add(new WillpowerComponent(10, 80)); // low willpower contributes to crack pressure
        npc.Add(new StressComponent { AcuteLevel = 60 });
        membership.SetRoom(npc, room);

        var peer = em.CreateEntity();
        peer.Add(new NpcTag());
        peer.Add(new SocialMaskComponent { Baseline = 30 });
        peer.Add(new SocialDrivesComponent());
        peer.Add(new PersonalityComponent(0, 0, 0, 0, 0));
        peer.Add(new WillpowerComponent(80, 80)); // high willpower → does not crack
        peer.Add(new StressComponent());
        membership.SetRoom(peer, room);

        var maskSys  = new SocialMaskSystem(membership, cfg);
        var crackSys = new MaskCrackSystem(membership, bus, cfg);

        var slipLog = new List<string>();
        bus.OnCandidateEmitted += c =>
        {
            if (c.Kind == NarrativeEventKind.MaskSlip)
                slipLog.Add(
                    $"tick={c.Tick}," +
                    $"participants={string.Join(",", c.ParticipantIds)}," +
                    $"detail={c.Detail}");
        };

        for (int i = 0; i < ticks; i++)
        {
            maskSys.Update(em, 1f);
            crackSys.Update(em, 1f);
        }

        return slipLog;
    }
}
