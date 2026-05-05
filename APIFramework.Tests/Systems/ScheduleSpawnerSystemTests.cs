using System;
using System.Collections.Generic;
using System.IO;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-04: ScheduleSpawnerSystem attaches ScheduleComponent matching the archetype's blocks
///         to an NPC with NpcArchetypeComponent("the-vent").
/// AT-05: Running the spawner twice is idempotent — blocks are not duplicated.
/// </summary>
public class ScheduleSpawnerSystemTests
{
    private const string TestScheduleJson = """
        {
          "schemaVersion": "0.1.0",
          "archetypeSchedules": [
            {
              "archetypeId": "the-vent",
              "blocks": [
                { "startHour":  6.0,  "endHour":  8.0,  "anchorId": "the-parking-lot",  "activity": "outside"  },
                { "startHour":  8.0,  "endHour": 12.0,  "anchorId": "the-window",        "activity": "atDesk"   },
                { "startHour": 12.0,  "endHour": 13.0,  "anchorId": "the-microwave",     "activity": "lunch"    },
                { "startHour": 13.0,  "endHour": 17.0,  "anchorId": "the-window",        "activity": "atDesk"   },
                { "startHour": 17.0,  "endHour":  6.0,  "anchorId": "the-parking-lot",   "activity": "sleeping" }
              ]
            }
          ]
        }
        """;

    private static string WriteTestScheduleFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test-schedules-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, TestScheduleJson);
        return path;
    }

    // -- AT-04 -----------------------------------------------------------------

    [Fact]
    public void AT04_Spawner_AttachesSchedule_ToMatchingArchetype()
    {
        var path = WriteTestScheduleFile();
        try
        {
            var em  = new EntityManager();
            var sys = new ScheduleSpawnerSystem(path);

            var npc = em.CreateEntity();
            npc.Add(new NpcTag());
            npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-vent" });

            sys.Update(em, 1f);

            Assert.True(npc.Has<ScheduleComponent>(), "NPC should have ScheduleComponent after spawner runs.");

            var schedule = npc.Get<ScheduleComponent>();
            Assert.NotNull(schedule.Blocks);
            Assert.Equal(5, schedule.Blocks.Count);

            Assert.Equal(ScheduleActivityKind.Outside,  schedule.Blocks[0].Activity);
            Assert.Equal(ScheduleActivityKind.AtDesk,   schedule.Blocks[1].Activity);
            Assert.Equal(ScheduleActivityKind.Lunch,    schedule.Blocks[2].Activity);
            Assert.Equal(ScheduleActivityKind.AtDesk,   schedule.Blocks[3].Activity);
            Assert.Equal(ScheduleActivityKind.Sleeping, schedule.Blocks[4].Activity);

            // Check anchor IDs
            Assert.Equal("the-parking-lot", schedule.Blocks[0].AnchorId);
            Assert.Equal("the-window",      schedule.Blocks[1].AnchorId);
            Assert.Equal("the-microwave",   schedule.Blocks[2].AnchorId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AT04_Spawner_AttachesEmptyCurrentBlock()
    {
        var path = WriteTestScheduleFile();
        try
        {
            var em  = new EntityManager();
            var sys = new ScheduleSpawnerSystem(path);

            var npc = em.CreateEntity();
            npc.Add(new NpcTag());
            npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-vent" });

            sys.Update(em, 1f);

            Assert.True(npc.Has<CurrentScheduleBlockComponent>());
            var curr = npc.Get<CurrentScheduleBlockComponent>();
            Assert.Equal(-1,         curr.ActiveBlockIndex);
            Assert.Equal(Guid.Empty, curr.AnchorEntityId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AT04_Spawner_SkipsNpc_WithUnknownArchetype()
    {
        var path = WriteTestScheduleFile();
        try
        {
            var em  = new EntityManager();
            var sys = new ScheduleSpawnerSystem(path);

            var npc = em.CreateEntity();
            npc.Add(new NpcTag());
            npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-unknown" });

            sys.Update(em, 1f);

            Assert.False(npc.Has<ScheduleComponent>(), "NPC with unknown archetype should not get a schedule.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    // -- AT-05: Idempotency ----------------------------------------------------

    [Fact]
    public void AT05_Spawner_IsIdempotent_RunningTwiceDoesNotDuplicate()
    {
        var path = WriteTestScheduleFile();
        try
        {
            var em  = new EntityManager();
            var sys = new ScheduleSpawnerSystem(path);

            var npc = em.CreateEntity();
            npc.Add(new NpcTag());
            npc.Add(new NpcArchetypeComponent { ArchetypeId = "the-vent" });

            sys.Update(em, 1f);
            var blockCountAfterFirst = npc.Get<ScheduleComponent>().Blocks.Count;

            sys.Update(em, 1f);
            var blockCountAfterSecond = npc.Get<ScheduleComponent>().Blocks.Count;

            Assert.Equal(blockCountAfterFirst, blockCountAfterSecond);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // -- LoadSchedules unit test -----------------------------------------------

    [Fact]
    public void LoadSchedules_ParsesBlocksCorrectly()
    {
        var path = WriteTestScheduleFile();
        try
        {
            var result = ScheduleSpawnerSystem.LoadSchedules(path);

            Assert.True(result.ContainsKey("the-vent"));
            var blocks = result["the-vent"];
            Assert.Equal(5, blocks.Count);
            Assert.Equal(6.0f,  blocks[0].StartHour);
            Assert.Equal(8.0f,  blocks[0].EndHour);
            Assert.Equal(17.0f, blocks[4].StartHour);
            Assert.Equal(6.0f,  blocks[4].EndHour);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadSchedules_ReturnsEmpty_WhenPathMissing()
    {
        var result = ScheduleSpawnerSystem.LoadSchedules("/nonexistent/path.json");
        Assert.Empty(result);
    }
}
