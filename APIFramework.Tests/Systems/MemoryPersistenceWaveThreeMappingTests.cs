using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using Xunit;

namespace APIFramework.Tests.Systems;

/// <summary>
/// AT-01 through AT-04 — Wave 3 NarrativeEventKind → IsPersistent mapping,
/// plus regression for existing persistent kinds.
/// </summary>
public class MemoryPersistenceWaveThreeMappingTests
{
    // AT-01
    [Fact]
    public void MaskSlip_IsPersistent() =>
        Assert.True(MemoryRecordingSystem.IsPersistent(NarrativeEventKind.MaskSlip));

    // AT-02
    [Fact]
    public void OverdueTask_IsPersistent() =>
        Assert.True(MemoryRecordingSystem.IsPersistent(NarrativeEventKind.OverdueTask));

    // AT-03
    [Fact]
    public void TaskCompleted_IsNotPersistent() =>
        Assert.False(MemoryRecordingSystem.IsPersistent(NarrativeEventKind.TaskCompleted));

    // AT-04 — regression: pre-Wave-3 persistent kinds still return true
    [Theory]
    [InlineData(NarrativeEventKind.WillpowerCollapse)]
    [InlineData(NarrativeEventKind.LeftRoomAbruptly)]
    public void ExistingPersistentKinds_StillPersistent(NarrativeEventKind kind) =>
        Assert.True(MemoryRecordingSystem.IsPersistent(kind));
}
