using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-03: CRT-blink visual mode vs halo/outline mode.
///
/// PlayerUIConfig.SelectionVisualMode drives which renderer is active.
/// The tests wire SelectionController → halo / blink renderers manually
/// via the SelectionChanged event, then verify the correct renderer activates.
/// </summary>
[TestFixture]
public class SelectionVisualToggleTests
{
    private GameObject               _go;
    private SelectionController      _ctrl;
    private SelectionHaloRenderer    _halo;
    private SelectionCrtBlinkRenderer _blink;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go    = new GameObject("SelVis_Ctrl");
        _ctrl  = _go.AddComponent<SelectionController>();
        _halo  = _go.AddComponent<SelectionHaloRenderer>();
        _blink = _go.AddComponent<SelectionCrtBlinkRenderer>();

        // Wire selection events to both renderers.
        _ctrl.SelectionChanged += _halo.OnSelectionChanged;
        _ctrl.SelectionChanged += _blink.OnSelectionChanged;

        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("SelVis_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Default_HaloNotVisible_WithNoSelection()
    {
        // Before any selection the halo should be invisible.
        yield return null;
        Assert.IsFalse(_halo.IsHaloVisible,
            "Halo should be invisible when nothing is selected.");
    }

    [UnityTest]
    public IEnumerator HaloMode_SelectNpc_HaloVisible()
    {
        var npcGo  = new GameObject("SelVis_NPC1");
        var selTag = npcGo.AddComponent<SelectableTag>();
        selTag.Kind = SelectableKind.Npc;

        _ctrl.SetSelection(selTag);
        yield return null;

        Assert.IsTrue(_halo.IsHaloVisible,
            "Halo should be visible after selecting an NPC in HaloAndOutline mode.");
    }

    [UnityTest]
    public IEnumerator CrtBlinkMode_SelectNpc_BlinkTracking()
    {
        // CrtBlinkRenderer tracks a selected entity when IsTracking is true.
        var npcGo  = new GameObject("SelVis_NPC2");
        var selTag = npcGo.AddComponent<SelectableTag>();
        selTag.Kind = SelectableKind.Npc;

        _ctrl.SetSelection(selTag);
        yield return null;

        Assert.IsTrue(_blink.IsTracking,
            "CrtBlinkRenderer should be tracking the selected NPC.");
    }

    [UnityTest]
    public IEnumerator CrtBlinkMode_ClearSelection_BlinkNotTracking()
    {
        var npcGo  = new GameObject("SelVis_NPC3");
        var selTag = npcGo.AddComponent<SelectableTag>();
        selTag.Kind = SelectableKind.Npc;

        _ctrl.SetSelection(selTag);
        yield return null;
        Assert.IsTrue(_blink.IsTracking);

        _ctrl.ClearSelection();
        yield return null;

        Assert.IsFalse(_blink.IsTracking,
            "CrtBlinkRenderer should stop tracking after selection is cleared.");
    }
}
