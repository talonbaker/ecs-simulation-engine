using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-01: ~ toggle opens and closes the developer console.
/// </summary>
[TestFixture]
public class DevConsoleOpenCloseTests
{
    private GameObject _go;
#if WARDEN
    private DevConsolePanel _panel;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go = new GameObject("DevCon_OpenClose");
#if WARDEN
        _panel = _go.AddComponent<DevConsolePanel>();
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("DevCon_OpenClose"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Default_PanelHidden()
    {
#if WARDEN
        yield return null;
        Assert.IsFalse(_panel.IsVisible, "Console should be hidden by default.");
#else
        yield return null;
        Assert.Pass("RETAIL — DevConsolePanel not compiled.");
#endif
    }

    [UnityTest]
    public IEnumerator Toggle_OpensThenCloses()
    {
#if WARDEN
        _panel.Toggle();
        yield return null;
        Assert.IsTrue(_panel.IsVisible, "First toggle should open the console.");

        _panel.Toggle();
        yield return null;
        Assert.IsFalse(_panel.IsVisible, "Second toggle should close the console.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator Open_ThenClose_WorksExplicitly()
    {
#if WARDEN
        _panel.Open();
        yield return null;
        Assert.IsTrue(_panel.IsVisible);

        _panel.Close();
        yield return null;
        Assert.IsFalse(_panel.IsVisible);
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
