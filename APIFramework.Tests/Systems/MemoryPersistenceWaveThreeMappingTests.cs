using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-01..AT-04 — Wave 3 NarrativeEventKind → IsPersistent mapping,
/// plus regression on the Phase-1 persistent kinds.
/// </summary>
public class MemoryPersistenceWaveThreeMappingTests
{
    [Theory]
    [InlineData(NarrativeEventKind.MaskSlip,          true)]   // AT-01
    [InlineData(NarrativeEventKind.OverdueTask,       true)]   // AT-02
    [InlineData(NarrativeEventKind.TaskCompleted,     false)]  // AT-03
    [InlineData(NarrativeEventKind.WillpowerCollapse, true)]   // AT-04 regression
    [InlineData(NarrativeEventKind.LeftRoomAbruptly,  true)]   // AT-04 regression
    public void IsPersistent_Wave3Kinds_ReturnExpectedValue(NarrativeEventKind kind, bool expected)
    {
        Assert.Equal(expected, MemoryRecordingSystem.IsPersistent(kind));
    }
}
