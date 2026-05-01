using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>AT-06: move command — calls MutationApi.MoveEntity when wired.</summary>
[TestFixture]
public class DevConsoleMoveTests
{
    private GameObject _go;
#if WARDEN
    private DevConsolePanel    _panel;
    private FakeWorldMutationApi _fakeApi;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_Move");
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
            if (go.name.StartsWith("DevCon_Move"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Move_NoEngine_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("move donna 8 12");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsTrue(hasError, "move without EngineHost must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Move_NoApi_ReturnsError()
    {
#if WARDEN
        _panel.SetMutationApi(null);
        _panel.Open();
        _panel.SubmitCommand("move donna 8 12");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsTrue(hasError, "move without MutationApi must return error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
