using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-03: F2 cycles modes correctly.
/// Uses <see cref="NpcIntrospectionToggle.SimulateF2"/> to trigger cycles without
/// a real keyboard event so these run headlessly.
/// </summary>
[TestFixture]
public class NpcIntrospectionToggleTests
{
    private GameObject _root;
#if WARDEN
    private NpcIntrospectionOverlay _overlay;
    private NpcIntrospectionToggle  _toggle;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _root = new GameObject("IntrospectionToggleTest");
#if WARDEN
        _overlay = _root.AddComponent<NpcIntrospectionOverlay>();
        _toggle  = _root.AddComponent<NpcIntrospectionToggle>();

        // Wire toggle → overlay via reflection (Inspector wiring substitute).
        var field = typeof(NpcIntrospectionToggle).GetField(
            "_overlay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_toggle, _overlay);
#endif
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("IntrospectionToggleTest"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator InitialMode_IsOff()
    {
#if WARDEN
        yield return null;
        Assert.AreEqual(NpcIntrospectionMode.Off, _overlay.Mode,
            "Overlay mode must start as Off.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator F2_Once_OffToSelected()
    {
#if WARDEN
        yield return null;
        Assert.AreEqual(NpcIntrospectionMode.Off, _overlay.Mode);

        _toggle.SimulateF2();
        yield return null;

        Assert.AreEqual(NpcIntrospectionMode.Selected, _overlay.Mode,
            "First F2 from Off should enter Selected mode.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator F2_Twice_SelectedToAll()
    {
#if WARDEN
        yield return null;

        _toggle.SimulateF2(); // Off → Selected
        yield return null;
        _toggle.SimulateF2(); // Selected → All
        yield return null;

        Assert.AreEqual(NpcIntrospectionMode.All, _overlay.Mode,
            "Second F2 should enter All mode.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator F2_ThreeTimes_AllToOff()
    {
#if WARDEN
        yield return null;

        _toggle.SimulateF2(); // Off → Selected
        yield return null;
        _toggle.SimulateF2(); // Selected → All
        yield return null;
        _toggle.SimulateF2(); // All → Off
        yield return null;

        Assert.AreEqual(NpcIntrospectionMode.Off, _overlay.Mode,
            "Third F2 should return to Off mode.");
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }

    [UnityTest]
    public IEnumerator CycleMode_FullRoundTrip()
    {
#if WARDEN
        yield return null;

        Assert.AreEqual(NpcIntrospectionMode.Off, _overlay.Mode);

        _overlay.CycleMode();
        Assert.AreEqual(NpcIntrospectionMode.Selected, _overlay.Mode);

        _overlay.CycleMode();
        Assert.AreEqual(NpcIntrospectionMode.All, _overlay.Mode);

        _overlay.CycleMode();
        Assert.AreEqual(NpcIntrospectionMode.Off, _overlay.Mode);

        yield return null;
#else
        yield return null;
        Assert.Pass("RETAIL — skipped.");
#endif
    }
}
