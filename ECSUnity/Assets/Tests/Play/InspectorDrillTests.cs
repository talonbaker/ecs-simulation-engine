using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-05: Drill tier shows relationships, current task, desire stack.
///
/// SetTier(Drill) switches to the detailed view.
/// </summary>
[TestFixture]
public class InspectorDrillTests
{
    private GameObject          _ctrlGo;
    private SelectionController _ctrl;
    private InspectorPanel      _inspector;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _ctrlGo    = new GameObject("InspDrill_Root");
        _ctrl      = _ctrlGo.AddComponent<SelectionController>();
        _inspector = _ctrlGo.AddComponent<InspectorPanel>();
        _ctrl.SelectionChanged += _inspector.OnSelectionChanged;
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("InspDrill_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator DrillTier_SetManually_TierIsDrill()
    {
        _inspector.SetTier(InspectorTier.Drill);
        yield return null;

        Assert.AreEqual(InspectorTier.Drill, _inspector.CurrentTier,
            "SetTier(Drill) should set the current tier to Drill.");
    }

    [UnityTest]
    public IEnumerator DrillTier_InspectorVisible_WithNpc()
    {
        _inspector.SetTier(InspectorTier.Drill);

        var npcGo  = new GameObject("InspDrill_NPC1");
        var selTag = npcGo.AddComponent<SelectableTag>();
        selTag.Kind = SelectableKind.Npc;

        _ctrl.SetSelection(selTag);
        yield return null;

        Assert.IsTrue(_inspector.IsVisible,
            "Inspector should be visible with an NPC selected in Drill tier.");
    }

    [UnityTest]
    public IEnumerator DrillTier_ClearSelection_HidesInspector()
    {
        _inspector.SetTier(InspectorTier.Drill);

        var npcGo  = new GameObject("InspDrill_NPC2");
        var selTag = npcGo.AddComponent<SelectableTag>();
        selTag.Kind = SelectableKind.Npc;

        _ctrl.SetSelection(selTag);
        yield return null;
        Assert.IsTrue(_inspector.IsVisible);

        _ctrl.ClearSelection();
        yield return null;

        Assert.IsFalse(_inspector.IsVisible,
            "Inspector should hide when selection is cleared in Drill tier.");
    }

    [UnityTest]
    public IEnumerator DrillTier_SwitchFromGlanceToDrill()
    {
        // Default is Glance; switching to Drill should take effect immediately.
        Assert.AreEqual(InspectorTier.Glance, _inspector.CurrentTier);

        _inspector.SetTier(InspectorTier.Drill);
        yield return null;

        Assert.AreEqual(InspectorTier.Drill, _inspector.CurrentTier,
            "Should successfully switch from Glance to Drill tier.");
    }
}
