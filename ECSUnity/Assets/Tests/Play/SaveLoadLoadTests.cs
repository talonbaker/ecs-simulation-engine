using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-11: Load retrieves a previously saved slot without throwing.
/// </summary>
[TestFixture]
public class SaveLoadLoadTests
{
    private GameObject     _go;
    private SaveLoadPanel  _panel;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go    = new GameObject("SLLoad_Panel");
        _panel = _go.AddComponent<SaveLoadPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("SLLoad_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Save_ThenLoad_NoException()
    {
        _panel.Save("LoadTest");
        yield return null;

        bool threw = false;
        try { _panel.Load("LoadTest"); }
        catch { threw = true; }

        Assert.IsFalse(threw,
            "Load('LoadTest') should not throw after a successful save.");
    }

    [UnityTest]
    public IEnumerator Load_NonExistentSlot_NoException()
    {
        // Loading a slot that does not exist should silently fail, not throw.
        bool threw = false;
        try { _panel.Load("NoSuchSlot_XYZ_999"); }
        catch { threw = true; }

        yield return null;

        Assert.IsFalse(threw,
            "Load() with a non-existent slot name should not throw.");
    }

    [UnityTest]
    public IEnumerator GetSlotNames_AfterSave_ContainsSlot()
    {
        _panel.Save("FindMe");
        yield return null;

        Assert.IsTrue(System.Array.Exists(_panel.GetSlotNames(), s => s == "FindMe"),
            "GetSlotNames() should contain 'FindMe' after Save('FindMe').");
    }

    [UnityTest]
    public IEnumerator MultipleSlots_BothRetrievable()
    {
        _panel.Save("First");
        _panel.Save("Second");
        yield return null;

        var slots = _panel.GetSlotNames();
        Assert.IsTrue(System.Array.Exists(slots, s => s == "First"),
            "Slot 'First' should appear in GetSlotNames().");
        Assert.IsTrue(System.Array.Exists(slots, s => s == "Second"),
            "Slot 'Second' should appear in GetSlotNames().");
    }
}
