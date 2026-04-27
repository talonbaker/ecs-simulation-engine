using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Integration;

/// <summary>
/// AT-09: MaskCrackSystem emits onto the bus; MemoryRecordingSystem routes to the correct
/// memory surface with Persistent=true (patching the WP-2.3.A gap where MaskSlip didn't exist).
///
/// Solo crack (no observers) → PersonalMemoryComponent on the cracking NPC.
/// Pair crack (one observer)  → RelationshipMemoryComponent on an auto-created relationship entity.
/// </summary>
public class MaskCrackMemoryIntegrationTests
{
    private static (EntityManager em, EntityRoomMembership membership,
                    MemoryRecordingSystem memorySys, MaskCrackSystem crackSys)
        Build()
    {
        var em         = new EntityManager();
        var membership = new EntityRoomMembership();
        var bus        = new NarrativeEventBus();
        var memorySys  = new MemoryRecordingSystem(bus, em, new MemoryConfig());
        var crackSys   = new MaskCrackSystem(membership, bus, new SocialMaskConfig
        {
            CrackThreshold        = 0.5,   // low so crack fires easily
            LowWillpowerThreshold = 30,
            SlipCooldownTicks     = 1800,
        });
        return (em, membership, memorySys, crackSys);
    }

    // ── Solo crack → PersonalMemoryComponent ─────────────────────────────────

    [Fact]
    public void AT09_SoloCrack_NoObservers_WritesPersonalMemory()
    {
        var (em, membership, _, crackSys) = Build();

        // No room set → observers = [] → 1 participant → personal memory
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialMaskComponent { IrritationMask = 60, CurrentLoad = 60 });
        npc.Add(new WillpowerComponent(0, 80));
        npc.Add(new StressComponent());

        crackSys.Update(em, 1f);

        Assert.True(npc.Has<PersonalMemoryComponent>());
        var recent = npc.Get<PersonalMemoryComponent>().Recent;
        Assert.Single(recent);
        Assert.Equal(NarrativeEventKind.MaskSlip, recent[0].Kind);
    }

    [Fact]
    public void AT09_SoloCrack_MemoryEntry_IsPersistentTrue()
    {
        var (em, membership, _, crackSys) = Build();

        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new SocialMaskComponent { IrritationMask = 60, CurrentLoad = 60 });
        npc.Add(new WillpowerComponent(0, 80));
        npc.Add(new StressComponent());

        crackSys.Update(em, 1f);

        var entry = npc.Get<PersonalMemoryComponent>().Recent[0];
        Assert.True(entry.Persistent,
            "MaskSlip memory entry must carry Persistent=true (IsPersistent patch from WP-2.3.A)");
    }

    // ── Pair crack → RelationshipMemoryComponent ──────────────────────────────

    [Fact]
    public void AT09_PairCrack_OneObserver_WritesRelationshipMemory()
    {
        var (em, membership, _, crackSys) = Build();

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
        observer.Add(new WillpowerComponent(80, 80)); // high WP → does not crack
        observer.Add(new StressComponent());
        membership.SetRoom(observer, room);

        crackSys.Update(em, 1f);

        var rels = em.Query<RelationshipTag>()
            .Where(e => e.Has<RelationshipMemoryComponent>())
            .ToList();
        Assert.Single(rels);

        var recent = rels[0].Get<RelationshipMemoryComponent>().Recent;
        Assert.Single(recent);
        Assert.Equal(NarrativeEventKind.MaskSlip, recent[0].Kind);
        Assert.True(recent[0].Persistent);
    }

    [Fact]
    public void AT09_PairCrack_AutoCreatesRelationship_Intensity50()
    {
        var (em, membership, _, crackSys) = Build();

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

        crackSys.Update(em, 1f);

        var rel = em.Query<RelationshipTag>().First(e => e.Has<RelationshipComponent>());
        Assert.Equal(50, rel.Get<RelationshipComponent>().Intensity);
    }
}
