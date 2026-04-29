using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-09: Locking a door calls AttachObstacle which emits ObstacleAttached on the
/// StructuralChangeBus → pathfinding cache version increments.
/// We verify this by counting AttachObstacle calls (the bus-→-cache chain is tested
/// on the engine side in WP-3.0.4 tests).
/// </summary>
[TestFixture]
public class PathInvalidationOnLockTests
{
    private FakeWorldMutationApi _fakeApi;
    private DoorLockContextMenu  _menu;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        var go   = new GameObject("PathInval_Menu");
        _menu    = go.AddComponent<DoorLockContextMenu>();
        _fakeApi = new FakeWorldMutationApi();
        _menu.SetDependencies(null, _fakeApi);
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("PathInval_"))
                UnityEngine.Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Lock_CallsAttachObstacle_TriggersInvalidation()
    {
        Guid doorId = Guid.NewGuid();
        _menu.DirectLock(doorId);
        yield return null;

        // One AttachObstacle call per lock → the bus emits ObstacleAttached →
        // PathfindingService listener clears its cache. We verify the call count here;
        // the cache itself is tested in engine-side PathfindingServiceTests.
        Assert.AreEqual(1, _fakeApi.AttachObstacleCount,
            "Locking a door should call AttachObstacle exactly once.");
    }

    [UnityTest]
    public IEnumerator LockThenUnlock_AttachAndDetachCalled()
    {
        Guid doorId = Guid.NewGuid();
        _menu.DirectLock(doorId);
        _menu.DirectUnlock(doorId);
        yield return null;

        Assert.AreEqual(1, _fakeApi.AttachObstacleCount, "Attach called once.");
        Assert.AreEqual(1, _fakeApi.DetachObstacleCount, "Detach called once.");
    }
}
