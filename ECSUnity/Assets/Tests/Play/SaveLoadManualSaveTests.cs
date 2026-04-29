using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-11: Manual save writes a slot that appears in GetSlotNames().
/// </summary>
[TestFixture]
public class SaveLoadManualSaveTests
{
    private GameObject     _go;
    private SaveLoadPanel  _panel;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go    = new GameObject("SLSave_Panel");
        _panel = _go.AddComponent<SaveLoadPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("SLSave_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Save_SlotAppearsInGetSlotNames()
    {
        _panel.Save("TestSlot1");
        yield return null;

        var slots = _panel.GetSlotNames();
        Assert.IsTrue(System.Array.Exists(slots, s => s == "TestSlot1"),
            "Saved slot 'TestSlot1' should appear in GetSlotNames().");
    }

    [UnityTest]
    public IEnumerator SaveTwice_TwoSlotsPresent()
    {
        _panel.Save("SlotA");
        _panel.Save("SlotB");
        yield return null;

        var slots = _panel.GetSlotNames();
        Assert.IsTrue(System.Array.Exists(slots, s => s == "SlotA"),
            "SlotA should be present after two saves.");
        Assert.IsTrue(System.Array.Exists(slots, s => s == "SlotB"),
            "SlotB should be present after two saves.");
    }

    [UnityTest]
    public IEnumerator SaveWithSameName_NotDuplicated()
    {
        // Saving the same slot name twice should not create duplicate entries.
        _panel.Save("Dup");
        _panel.Save("Dup");
        yield return null;

        var slots = _panel.GetSlotNames();
        int count = slots.Count(s => s == "Dup");
        Assert.LessOrEqual(count, 1,
            "Saving the same slot name twice must not produce duplicate entries.");
    }

    [UnityTest]
    public IEnumerator SaveSlot_IsNotEmpty()
    {
        _panel.Save("NonEmpty");
        yield return null;

        var slots = _panel.GetSlotNames();
        Assert.IsNotEmpty(slots,
            "GetSlotNames() should return at least one entry after a save.");
    }
}
