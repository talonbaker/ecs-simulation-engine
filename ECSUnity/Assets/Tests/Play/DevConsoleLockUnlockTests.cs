using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>AT-10: lock/unlock commands call AttachObstacle/DetachObstacle on the mutation API.</summary>
[TestFixture]
public class DevConsoleLockUnlockTests
{
    private GameObject _go;
#if WARDEN
    private DevConsolePanel      _panel;
    private FakeWorldMutationApi _fakeApi;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_LockUnlock");
#if WARDEN
        _panel   = _go.AddComponent<DevConsolePanel>();
        _fakeApi = new FakeWorldMutationApi();
        yield return null;
        _panel.SetMutationApi(_fakeApi);
#else
        yield return null;
#endif
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_LockUnlock"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Lock_WithGuid_CallsAttachObstacle()
    {
#if WARDEN
        string doorId = Guid.NewGuid().ToString();
        _panel.Open();
        _panel.SubmitCommand($"lock {doorId}");
        yield return null;

        Assert.AreEqual(1, _fakeApi.AttachObstacleCount,
            "lock <doorId> should call AttachObstacle once.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Unlock_WithGuid_CallsDetachObstacle()
    {
#if WARDEN
        string doorId = Guid.NewGuid().ToString();
        _panel.Open();
        _panel.SubmitCommand($"unlock {doorId}");
        yield return null;

        Assert.AreEqual(1, _fakeApi.DetachObstacleCount,
            "unlock <doorId> should call DetachObstacle once.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Lock_InvalidGuid_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("lock not-a-guid");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsTrue(hasError, "lock with invalid GUID must return an error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
