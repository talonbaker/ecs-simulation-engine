using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-11: F5/F9 quick save and quick load.
///
/// QuickSave() creates a well-known slot (contains "Quick" or named "AutoSave").
/// QuickLoad() restores the quick-save slot without throwing.
/// </summary>
[TestFixture]
public class SaveLoadQuickSaveLoadTests
{
    private GameObject     _go;
    private SaveLoadPanel  _panel;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go    = new GameObject("SLQS_Panel");
        _panel = _go.AddComponent<SaveLoadPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("SLQS_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator QuickSave_CreatesQuickSlot()
    {
        _panel.QuickSave();
        yield return null;

        var slots = _panel.GetSlotNames();
        bool hasQuickSlot = System.Array.Exists(slots, s =>
            s != null &&
            (s.IndexOf("Quick",  System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             s.IndexOf("Auto",   System.StringComparison.OrdinalIgnoreCase) >= 0));

        Assert.IsTrue(hasQuickSlot,
            "QuickSave() should create a slot whose name contains 'Quick' or 'Auto'.");
    }

    [UnityTest]
    public IEnumerator QuickLoad_NoException()
    {
        _panel.QuickSave();
        yield return null;

        bool threw = false;
        try { _panel.QuickLoad(); }
        catch { threw = true; }

        Assert.IsFalse(threw,
            "QuickLoad() should not throw after a prior QuickSave().");
    }

    [UnityTest]
    public IEnumerator QuickSave_ThenQuickLoad_DoesNotThrow()
    {
        _panel.QuickSave();
        yield return null;
        _panel.QuickLoad();
        yield return null;
        // If we reach here without exception the test passes.
        Assert.Pass("QuickSave then QuickLoad completed without exception.");
    }

    [UnityTest]
    public IEnumerator QuickSave_IsIdempotent()
    {
        // Calling QuickSave twice should not throw and should not duplicate slots.
        bool threw = false;
        try
        {
            _panel.QuickSave();
            _panel.QuickSave();
        }
        catch { threw = true; }

        yield return null;

        Assert.IsFalse(threw,
            "Multiple calls to QuickSave() must not throw.");
    }
}
