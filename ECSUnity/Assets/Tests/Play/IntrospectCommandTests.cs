using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-04: `introspect` console verb sets the overlay mode correctly.
/// </summary>
[TestFixture]
public class IntrospectCommandTests
{
    private GameObject _root;
#if WARDEN
    private NpcIntrospectionOverlay    _overlay;
    private DevConsoleCommandDispatcher _dispatcher;
    private DevCommandContext           _ctx;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _root = new GameObject("IntrospectCmdTest");
#if WARDEN
        _overlay    = _root.AddComponent<NpcIntrospectionOverlay>();
        _dispatcher = new DevConsoleCommandDispatcher();
        _dispatcher.RegisterCommand(new IntrospectCommand());

        _ctx = new DevCommandContext { Overlay = _overlay };
        _dispatcher.SetContext(_ctx);
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("IntrospectCmdTest"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Introspect_On_SetsAllMode()
    {
#if WARDEN
        _dispatcher.Execute("introspect on", out string output);
        yield return null;

        Assert.AreEqual(NpcIntrospectionMode.All, _overlay.Mode,
            "`introspect on` must set All mode.");
        Assert.IsFalse(output?.StartsWith("ERROR:") ?? true,
            "introspect on must not return an error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Introspect_All_SetsAllMode()
    {
#if WARDEN
        _dispatcher.Execute("introspect all", out _);
        yield return null;

        Assert.AreEqual(NpcIntrospectionMode.All, _overlay.Mode,
            "`introspect all` must set All mode.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Introspect_Selected_SetsSelectedMode()
    {
#if WARDEN
        _dispatcher.Execute("introspect selected", out _);
        yield return null;

        Assert.AreEqual(NpcIntrospectionMode.Selected, _overlay.Mode,
            "`introspect selected` must set Selected mode.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Introspect_Off_SetsOffMode()
    {
#if WARDEN
        // First turn it on.
        _dispatcher.Execute("introspect on", out _);
        yield return null;

        _dispatcher.Execute("introspect off", out string output);
        yield return null;

        Assert.AreEqual(NpcIntrospectionMode.Off, _overlay.Mode,
            "`introspect off` must set Off mode.");
        Assert.IsFalse(output?.StartsWith("ERROR:") ?? true,
            "introspect off must not return an error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Introspect_NoArgs_ReturnsError()
    {
#if WARDEN
        _dispatcher.Execute("introspect", out string output);
        yield return null;

        Assert.IsTrue(output?.StartsWith("ERROR:") ?? false,
            "introspect with no args must return an error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Introspect_UnknownArg_ReturnsError()
    {
#if WARDEN
        _dispatcher.Execute("introspect banana", out string output);
        yield return null;

        Assert.IsTrue(output?.StartsWith("ERROR:") ?? false,
            "introspect with unknown arg must return an error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Introspect_NullOverlay_ReturnsError()
    {
#if WARDEN
        _ctx.Overlay = null;

        _dispatcher.Execute("introspect on", out string output);
        yield return null;

        Assert.IsTrue(output?.StartsWith("ERROR:") ?? false,
            "introspect with null overlay must return an error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
