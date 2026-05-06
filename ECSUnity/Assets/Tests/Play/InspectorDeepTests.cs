using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-06: Deep tier shows full memory log, personality matrix, timeline.
///
/// SetTier(Deep) switches to the most detailed inspector view.
/// </summary>
[TestFixture]
public class InspectorDeepTests
{
    private GameObject          _ctrlGo;
    private SelectionController _ctrl;
    private InspectorPanel      _inspector;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _ctrlGo    = new GameObject("InspDeep_Root");
        _ctrl      = _ctrlGo.AddComponent<SelectionController>();
        _inspector = _ctrlGo.AddComponent<InspectorPanel>();
        _ctrl.SelectionChanged += _inspector.OnSelectionChanged;
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("InspDeep_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator DeepTier_SetManually_TierIsDeep()
    {
        _inspector.SetTier(InspectorTier.Deep);
        yield return null;

        Assert.AreEqual(InspectorTier.Deep, _inspector.CurrentTier,
            "SetTier(Deep) should set current tier to Deep.");
    }

    [UnityTest]
    public IEnumerator DeepTier_InspectorVisible_WithNpc()
    {
        _inspector.SetTier(InspectorTier.Deep);

        var npcGo  = new GameObject("InspDeep_NPC1");
        var selTag = npcGo.AddComponent<SelectableTag>();
        selTag.Kind = SelectableKind.Npc;

        _ctrl.SetSelection(selTag);
        yield return null;

        Assert.IsTrue(_inspector.IsVisible,
            "Inspector should be visible with an NPC selected in Deep tier.");
    }

    [UnityTest]
    public IEnumerator DeepTier_ClearSelection_HidesInspector()
    {
        _inspector.SetTier(InspectorTier.Deep);

        var npcGo  = new GameObject("InspDeep_NPC2");
        var selTag = npcGo.AddComponent<SelectableTag>();
        selTag.Kind = SelectableKind.Npc;

        _ctrl.SetSelection(selTag);
        yield return null;
        Assert.IsTrue(_inspector.IsVisible);

        _ctrl.ClearSelection();
        yield return null;

        Assert.IsFalse(_inspector.IsVisible,
            "Inspector should hide when selection is cleared in Deep tier.");
    }

    [UnityTest]
    public IEnumerator CycleTiers_AllThreeValid()
    {
        // Cycling through all three tiers should produce no exceptions
        // and each tier should be correctly reported.
        _inspector.SetTier(InspectorTier.Glance);
        yield return null;
        Assert.AreEqual(InspectorTier.Glance, _inspector.CurrentTier);

        _inspector.SetTier(InspectorTier.Drill);
        yield return null;
        Assert.AreEqual(InspectorTier.Drill, _inspector.CurrentTier);

        _inspector.SetTier(InspectorTier.Deep);
        yield return null;
        Assert.AreEqual(InspectorTier.Deep, _inspector.CurrentTier,
            "All three tier transitions should succeed without error.");
    }
}
