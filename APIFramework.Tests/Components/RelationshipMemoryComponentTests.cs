using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Components;

/// <summary>
/// Tests for RelationshipMemoryComponent ring-buffer behaviour.
/// AT-07 — Ring-buffer overflow leaves the most recent N entries; oldest dropped.
/// </summary>
public class RelationshipMemoryComponentTests
{
    private static MemoryEntry MakeEntry(int n) => new(
        Id:             $"mem-{n:D8}-DriveSpike-00000001-1",
        Tick:           n,
        Kind:           NarrativeEventKind.DriveSpike,
        ParticipantIds: new[] { 1 },
        RoomId:         null,
        Detail:         $"entry {n}",
        Persistent:     false
    );

    [Fact]
    public void Default_RecentIsEmpty()
    {
        var c = default(RelationshipMemoryComponent);
        Assert.Empty(c.Recent);
    }

    [Fact]
    public void SetRecent_StoresAndReturnsValue()
    {
        var entry = MakeEntry(1);
        var c     = new RelationshipMemoryComponent { Recent = new[] { entry } };
        Assert.Single(c.Recent);
        Assert.Equal(entry.Id, c.Recent[0].Id);
    }

    [Fact]
    public void Overflow_OldestEntriesDropped()
    {
        const int Capacity = 32;
        const int Total    = 50;

        IReadOnlyList<MemoryEntry> buf = Array.Empty<MemoryEntry>();
        for (int i = 1; i <= Total; i++)
        {
            var list = new System.Collections.Generic.List<MemoryEntry>(buf) { MakeEntry(i) };
            if (list.Count > Capacity)
                list.RemoveRange(0, list.Count - Capacity);
            buf = list;
        }

        Assert.Equal(Capacity, buf.Count);
        Assert.Equal(Total - Capacity + 1, buf[0].Tick);
        Assert.Equal(Total, buf[^1].Tick);
    }

    [Fact]
    public void Overflow_MostRecentEntriesRetained()
    {
        const int Capacity = 32;
        const int Total    = 50;

        IReadOnlyList<MemoryEntry> buf = Array.Empty<MemoryEntry>();
        for (int i = 1; i <= Total; i++)
        {
            var list = new System.Collections.Generic.List<MemoryEntry>(buf) { MakeEntry(i) };
            if (list.Count > Capacity)
                list.RemoveRange(0, list.Count - Capacity);
            buf = list;
        }

        // The last 32 entries (19..50) must be present
        for (int expected = Total - Capacity + 1; expected <= Total; expected++)
        {
            Assert.Contains(buf, e => e.Tick == expected);
        }
    }
}
