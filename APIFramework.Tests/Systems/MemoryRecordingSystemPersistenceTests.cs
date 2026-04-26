using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-08 — Persistent flag matches the documented NarrativeEventKind → bool mapping
///         for every kind in the current enum.
/// </summary>
public class MemoryRecordingSystemPersistenceTests
{
    [Theory]
    [InlineData(NarrativeEventKind.WillpowerCollapse,   true)]
    [InlineData(NarrativeEventKind.LeftRoomAbruptly,    true)]
    [InlineData(NarrativeEventKind.DriveSpike,          false)]
    [InlineData(NarrativeEventKind.WillpowerLow,        false)]
    [InlineData(NarrativeEventKind.ConversationStarted, false)]
    public void IsPersistent_MatchesDocumentedMapping(NarrativeEventKind kind, bool expected)
    {
        Assert.Equal(expected, MemoryRecordingSystem.IsPersistent(kind));
    }
}
