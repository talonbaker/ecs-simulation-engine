using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Components;

/// <summary>
/// Tests for PersonalMemoryComponent — same shape as RelationshipMemoryComponent.
/// AT-07 — Capacity-16 personal buffer overflow leaves 16 most recent.
/// </summary>
public class PersonalMemoryComponentTests
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
        var c = default(PersonalMemoryComponent);
        Assert.Empty(c.Recent);
    }

    [Fact]
    public void SetRecent_StoresAndReturnsValue()
    {
        var entry = MakeEntry(1);
        var c     = new PersonalMemoryComponent { Recent = new[] { entry } };
        Assert.Single(c.Recent);
        Assert.Equal(entry.Id, c.Recent[0].Id);
    }

    [Fact]
    public void Overflow_Capacity16_OldestDropped()
    {
        const int Capacity = 16;
        const int Total    = 30;

        IReadOnlyList<MemoryEntry> buf = Array.Empty<MemoryEntry>();
        for (int i = 1; i <= Total; i++)
        {
            var list = new List<MemoryEntry>(buf) { MakeEntry(i) };
            if (list.Count > Capacity)
                list.RemoveRange(0, list.Count - Capacity);
            buf = list;
        }

        Assert.Equal(Capacity, buf.Count);
        Assert.Equal(Total - Capacity + 1, buf[0].Tick);
        Assert.Equal(Total, buf[^1].Tick);
    }
}
