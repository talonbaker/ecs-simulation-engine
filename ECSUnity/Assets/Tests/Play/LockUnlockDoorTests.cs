using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-08: Right-click door → Lock attaches LockedTag; Unlock removes it.
/// </summary>
[TestFixture]
public class LockUnlockDoorTests
{
    private GameObject         _go;
    private DoorLockContextMenu _menu;
    private FakeWorldMutationApi _fakeApi;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go      = new GameObject("LockUnlock_Menu");
        _menu    = _go.AddComponent<DoorLockContextMenu>();
        _fakeApi = new FakeWorldMutationApi();
        _menu.SetDependencies(null, _fakeApi);
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("LockUnlock_"))
                UnityEngine.Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator DirectLock_CallsAttachObstacle()
    {
        Guid doorId = Guid.NewGuid();
        bool ok = _menu.DirectLock(doorId);
        yield return null;

        Assert.IsTrue(ok, "DirectLock should return true.");
        Assert.AreEqual(1, _fakeApi.AttachObstacleCount,
            "AttachObstacle should be called once.");
    }

    [UnityTest]
    public IEnumerator DirectUnlock_CallsDetachObstacle()
    {
        Guid doorId = Guid.NewGuid();
        _menu.DirectLock(doorId);
        yield return null;

        bool ok = _menu.DirectUnlock(doorId);
        yield return null;

        Assert.IsTrue(ok, "DirectUnlock should return true.");
        Assert.AreEqual(1, _fakeApi.DetachObstacleCount,
            "DetachObstacle should be called once after unlock.");
    }

    [UnityTest]
    public IEnumerator NoApi_LockReturnsFalse()
    {
        var go   = new GameObject("LockUnlock_NoApi");
        var menu = go.AddComponent<DoorLockContextMenu>();
        // No API injected.

        bool ok = menu.DirectLock(Guid.NewGuid());
        yield return null;

        Assert.IsFalse(ok, "Lock should return false without API.");
        UnityEngine.Object.Destroy(go);
    }
}
