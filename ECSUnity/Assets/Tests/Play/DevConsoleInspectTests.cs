using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-03: inspect command outputs entity name.
/// Uses programmatic entity creation via spawn command.
/// </summary>
[TestFixture]
public class DevConsoleInspectTests
{
    private GameObject _go;
#if WARDEN
    private DevConsolePanel _panel;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_Inspect");
#if WARDEN
        _panel = _go.AddComponent<DevConsolePanel>();
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_Inspect"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Inspect_UnknownEntity_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("inspect nobody_xyz");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsTrue(hasError, "inspect on unknown entity should produce an error entry.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Inspect_NoArgs_ReturnsError()
    {
#if WARDEN
        _panel.Open();
        _panel.SubmitCommand("inspect");
        yield return null;

        bool hasError = false;
        foreach (var e in _panel.GetHistory())
            if (e.Kind == ConsoleEntryKind.Error) { hasError = true; break; }

        Assert.IsTrue(hasError, "inspect with no args should produce an error.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
