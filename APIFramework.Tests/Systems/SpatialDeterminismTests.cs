using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-10: Two runs with the same seed over 5000 ticks produce byte-identical
/// proximity event streams. Verifies the spatial determinism contract.
/// </summary>
public class SpatialDeterminismTests
{
    [Fact]
    public void SpatialSystems_TwoRunsSameSeed_ProduceIdenticalEventStreams()
    {
        const int seed  = 12345;
        const int ticks = 5000;
        const int npcs  = 8;

        var stream1 = RunStream(seed, ticks, npcs);
        var stream2 = RunStream(seed, ticks, npcs);

        Assert.Equal(stream1.Count, stream2.Count);
        for (int i = 0; i < stream1.Count; i++)
        {
            Assert.True(stream1[i] == stream2[i],
                $"Event {i} diverged: \"{stream1[i]}\" vs \"{stream2[i]}\"");
        }
    }

    [Fact]
    public void SpatialSystems_DifferentSeeds_ProduceDifferentEventStreams()
    {
        const int ticks = 5000;
        const int npcs  = 8;

        var s1 = RunStream(seed: 1, ticks, npcs);
        var s2 = RunStream(seed: 2, ticks, npcs);

        bool anyDiff = false;
        int  maxCheck = System.Math.Min(s1.Count, s2.Count);
        for (int i = 0; i < maxCheck; i++)
        {
            if (s1[i] != s2[i]) { anyDiff = true; break; }
        }

        Assert.True(anyDiff || s1.Count != s2.Count,
            "Different seeds should produce different event streams over 5000 ticks");
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static List<string> RunStream(int seed, int ticks, int npcCount)
    {
        var rng = new SeededRandom(seed);
        var em  = new EntityManager();

        var idx        = new GridSpatialIndex(4, 128, 128);
        var sync       = new SpatialIndexSyncSystem(idx);
        var membership = new EntityRoomMembership();
        var bus        = new ProximityEventBus();
        var roomSys    = new RoomMembershipSystem(membership, bus);
        var proxSys    = new ProximityEventSystem(idx, bus, membership);
        em.EntityDestroyed += sync.OnEntityDestroyed;

        // Spawn NPCs at random starting positions
        var npcs = new Entity[npcCount];
        for (int i = 0; i < npcCount; i++)
        {
            npcs[i] = em.CreateEntity();
            npcs[i].Add(new PositionComponent
            {
                X = rng.NextFloat() * 64f,
                Y = 0f,
                Z = rng.NextFloat() * 64f,
            });
            npcs[i].Add(ProximityComponent.Default);
            npcs[i].Add(new NpcTag());
        }

        // Capture all proximity events as strings for comparison
        var stream = new List<string>();

        bus.OnEnteredConversationRange += e =>
            stream.Add($"ECR {e.Observer.Id} {e.Target.Id} t{e.Tick}");
        bus.OnLeftConversationRange    += e =>
            stream.Add($"LCR {e.Observer.Id} {e.Target.Id} t{e.Tick}");
        bus.OnRoomMembershipChanged    += e =>
            stream.Add($"RMC {e.Subject.Id} t{e.Tick}");

        // Run ticks; each tick moves NPCs a small seeded step
        for (int t = 0; t < ticks; t++)
        {
            // Move NPCs deterministically using the seeded RNG
            foreach (var npc in npcs)
            {
                var pos = npc.Get<PositionComponent>();
                float dx = (rng.NextFloat() - 0.5f) * 2f;
                float dz = (rng.NextFloat() - 0.5f) * 2f;
                npc.Add(new PositionComponent
                {
                    X = System.Math.Clamp(pos.X + dx, 0f, 63f),
                    Y = 0f,
                    Z = System.Math.Clamp(pos.Z + dz, 0f, 63f),
                });
            }

            sync.Update(em, 1f);
            roomSys.Update(em, 1f);
            proxSys.Update(em, 1f);
        }

        return stream;
    }
}
