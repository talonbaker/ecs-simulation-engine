using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Spatial;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.LifeState;

/// <summary>
/// AT-01: Alive NPC in same room as corpse, relationship above threshold → AcuteLevel increased.
/// AT-02: Second tick in the same configuration → no re-trigger (EncounteredCorpseIds guard).
/// AT-03: NPC in a different room from the corpse → no effect.
/// AT-04: Relationship Intensity below ProximityBereavementMinIntensity → no effect.
/// AT-05: NPC with no relationship to the deceased → no effect.
/// AT-06: Deceased NPC in the same room as a corpse → not processed (LifeStateGuard.IsAlive).
/// </summary>
public class BereavementByProximitySystemTests
{
    // -- Helpers ---------------------------------------------------------------

    private static BereavementConfig DefaultCfg() => new()
    {
        WitnessedDeathStressGain           = 20.0,
        BereavementStressGain              = 5.0,
        WitnessGriefIntensity             = 80f,
        ColleagueBereavementGriefIntensity = 40f,
        BereavementMinIntensity           = 20,
        ProximityBereavementMinIntensity   = 30,
        ProximityBereavementStressGain     = 8.0,
    };

    private static int EntityIntId(Entity e)
    {
        var b = e.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }

    /// <summary>
    /// Builds a world with:
    ///   - Two rooms (r1, r2)
    ///   - One corpse in r1
    ///   - One alive NPC in npcRoom
    ///   - An optional relationship between NPC and deceased
    /// Returns the EntityManager, membership, system, NPC entity, and corpse entity.
    /// </summary>
    private static (
        EntityManager em,
        EntityRoomMembership membership,
        BereavementByProximitySystem sys,
        Entity npc,
        Entity corpse)
    Build(
        bool   npcInSameRoom    = true,
        int    intensity        = 50,   // above ProximityBereavementMinIntensity (30) by default
        bool   hasRelationship  = true,
        LS npcState      = LS.Alive)
    {
        var em         = new EntityManager();
        var membership = new EntityRoomMembership();

        // Rooms
        var room1 = em.CreateEntity();
        room1.Add(new RoomComponent { Id = "r1", Name = "office" });

        var room2 = em.CreateEntity();
        room2.Add(new RoomComponent { Id = "r2", Name = "hallway" });

        // Corpse (deceased NPC in room1)
        var corpse = em.CreateEntity();
        corpse.Add(new NpcTag());
        corpse.Add(new LifeStateComponent { State = LS.Deceased });
        corpse.Add(new CorpseTag());
        corpse.Add(new CorpseComponent
        {
            DeathTick           = 1,
            OriginalNpcEntityId = corpse.Id,
            LocationRoomId      = "r1",
        });
        membership.SetRoom(corpse, room1);

        // Alive NPC (room depends on npcInSameRoom flag)
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = npcState });
        npc.Add(new StressComponent { AcuteLevel = 10 });
        membership.SetRoom(npc, npcInSameRoom ? room1 : room2);

        // Relationship entity
        if (hasRelationship)
        {
            var rel = em.CreateEntity();
            rel.Add(new RelationshipTag());
            rel.Add(new RelationshipComponent(EntityIntId(npc), EntityIntId(corpse), intensity: intensity));
        }

        var sys = new BereavementByProximitySystem(membership, DefaultCfg());
        return (em, membership, sys, npc, corpse);
    }

    // -- AT-01: Same room + relationship → AcuteLevel increased ---------------

    [Fact]
    public void AT01_NpcInSameRoomAsCorpse_RelationshipAboveThreshold_AcuteLevelIncreased()
    {
        var (em, _, sys, npc, _) = Build();
        int before = npc.Get<StressComponent>().AcuteLevel;

        sys.Update(em, 1f);

        int after = npc.Get<StressComponent>().AcuteLevel;
        Assert.True(after > before, $"Expected AcuteLevel to increase; was {before}, got {after}.");
    }

    [Fact]
    public void AT01_AcuteLevelIncrease_EqualToProximityBereavementStressGain()
    {
        var (em, _, sys, npc, _) = Build();
        int before = npc.Get<StressComponent>().AcuteLevel;

        sys.Update(em, 1f);

        int after    = npc.Get<StressComponent>().AcuteLevel;
        int expected = before + (int)DefaultCfg().ProximityBereavementStressGain;
        Assert.Equal(expected, after);
    }

    // -- AT-02: Second tick → no re-trigger -----------------------------------

    [Fact]
    public void AT02_SecondTick_SameRoom_NoReTriger_AcuteLevelUnchanged()
    {
        var (em, _, sys, npc, _) = Build();

        sys.Update(em, 1f); // first encounter
        int afterFirstTick = npc.Get<StressComponent>().AcuteLevel;

        sys.Update(em, 1f); // second tick — should be a no-op
        int afterSecondTick = npc.Get<StressComponent>().AcuteLevel;

        Assert.Equal(afterFirstTick, afterSecondTick);
    }

    [Fact]
    public void AT02_BereavementHistoryComponent_EncounterIdRecorded()
    {
        var (em, _, sys, npc, corpse) = Build();

        sys.Update(em, 1f);

        Assert.True(npc.Has<BereavementHistoryComponent>());
        Assert.Contains(corpse.Id, npc.Get<BereavementHistoryComponent>().EncounteredCorpseIds);
    }

    // -- AT-03: Different room → no effect ------------------------------------

    [Fact]
    public void AT03_NpcInDifferentRoom_NoEffect()
    {
        var (em, _, sys, npc, _) = Build(npcInSameRoom: false);
        int before = npc.Get<StressComponent>().AcuteLevel;

        sys.Update(em, 1f);

        int after = npc.Get<StressComponent>().AcuteLevel;
        Assert.Equal(before, after);
    }

    // -- AT-04: Relationship below threshold → no effect ----------------------

    [Fact]
    public void AT04_RelationshipBelowProximityThreshold_NoEffect()
    {
        // ProximityBereavementMinIntensity = 30; using intensity = 15
        var (em, _, sys, npc, _) = Build(intensity: 15);
        int before = npc.Get<StressComponent>().AcuteLevel;

        sys.Update(em, 1f);

        int after = npc.Get<StressComponent>().AcuteLevel;
        Assert.Equal(before, after);
    }

    // -- AT-05: No relationship → no effect -----------------------------------

    [Fact]
    public void AT05_NoRelationship_NoEffect()
    {
        var (em, _, sys, npc, _) = Build(hasRelationship: false);
        int before = npc.Get<StressComponent>().AcuteLevel;

        sys.Update(em, 1f);

        int after = npc.Get<StressComponent>().AcuteLevel;
        Assert.Equal(before, after);
    }

    // -- AT-06: Deceased NPC in same room → not processed ---------------------

    [Fact]
    public void AT06_DeceasedNpcInSameRoom_NotProcessed()
    {
        var (em, _, sys, npc, _) = Build(npcState: LS.Deceased);

        sys.Update(em, 1f);

        // Deceased NPC should not receive BereavementHistoryComponent
        Assert.False(npc.Has<BereavementHistoryComponent>());
    }
}
