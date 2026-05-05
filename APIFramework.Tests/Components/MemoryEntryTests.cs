using System.Collections.Generic;
using System.Text.Json;
using APIFramework.Components;
using APIFramework.Systems.Narrative;
using APIFramework.Systems;
using Xunit;

namespace APIFramework.Tests.Components;

/// <summary>
/// AT-01 — All new components compile, instantiate, equality round-trip.
///         MemoryEntry.Id is deterministic for the same (Tick, Kind, ParticipantIds, RoomId).
/// </summary>
public class MemoryEntryTests
{
    // -- Id determinism --------------------------------------------------------

    [Fact]
    public void BuildId_SameInputs_ProducesSameId()
    {
        var ids1 = new[] { 1, 2 };
        var ids2 = new[] { 1, 2 };
        var id1 = MemoryRecordingSystem.BuildId(42L, NarrativeEventKind.WillpowerCollapse, ids1);
        var id2 = MemoryRecordingSystem.BuildId(42L, NarrativeEventKind.WillpowerCollapse, ids2);
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void BuildId_DifferentTick_ProducesDifferentId()
    {
        var ids = new[] { 1, 2 };
        var id1 = MemoryRecordingSystem.BuildId(1L, NarrativeEventKind.DriveSpike, ids);
        var id2 = MemoryRecordingSystem.BuildId(2L, NarrativeEventKind.DriveSpike, ids);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void BuildId_DifferentKind_ProducesDifferentId()
    {
        var ids = new[] { 1 };
        var id1 = MemoryRecordingSystem.BuildId(10L, NarrativeEventKind.DriveSpike, ids);
        var id2 = MemoryRecordingSystem.BuildId(10L, NarrativeEventKind.WillpowerCollapse, ids);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void BuildId_Format_ContainsMemPrefix()
    {
        var id = MemoryRecordingSystem.BuildId(5L, NarrativeEventKind.LeftRoomAbruptly, new[] { 3 });
        Assert.StartsWith("mem-", id);
    }

    // -- Construction ----------------------------------------------------------

    [Fact]
    public void MemoryEntry_Construct_FieldsAccessible()
    {
        var entry = new MemoryEntry(
            Id:             "mem-00000001-DriveSpike-00000007-1",
            Tick:           1L,
            Kind:           NarrativeEventKind.DriveSpike,
            ParticipantIds: new[] { 7 },
            RoomId:         "room-a",
            Detail:         "irritation: 30 → 50 (+20)",
            Persistent:     false
        );

        Assert.Equal("mem-00000001-DriveSpike-00000007-1", entry.Id);
        Assert.Equal(1L, entry.Tick);
        Assert.Equal(NarrativeEventKind.DriveSpike, entry.Kind);
        Assert.Equal(new[] { 7 }, entry.ParticipantIds);
        Assert.Equal("room-a", entry.RoomId);
        Assert.Equal("irritation: 30 → 50 (+20)", entry.Detail);
        Assert.False(entry.Persistent);
    }

    // -- JSON round-trip -------------------------------------------------------

    [Fact]
    public void MemoryEntry_JsonRoundTrip_FieldsPreserved()
    {
        var entry = new MemoryEntry(
            Id:             "mem-00000042-WillpowerCollapse-00000001-2",
            Tick:           42L,
            Kind:           NarrativeEventKind.WillpowerCollapse,
            ParticipantIds: new[] { 1, 2 },
            RoomId:         null,
            Detail:         "willpower collapsed",
            Persistent:     true
        );

        var json        = JsonSerializer.Serialize(entry);
        var deserialized = JsonSerializer.Deserialize<MemoryEntry>(json);

        Assert.Equal(entry.Id,         deserialized.Id);
        Assert.Equal(entry.Tick,       deserialized.Tick);
        Assert.Equal(entry.Kind,       deserialized.Kind);
        Assert.Equal(entry.RoomId,     deserialized.RoomId);
        Assert.Equal(entry.Detail,     deserialized.Detail);
        Assert.Equal(entry.Persistent, deserialized.Persistent);
        Assert.Equal(
            entry.ParticipantIds,
            deserialized.ParticipantIds);
    }
}
