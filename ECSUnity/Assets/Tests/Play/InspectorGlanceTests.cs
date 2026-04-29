using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-04: Selected NPC; glance tier shows name, activity, mood, contextual fact.
///
/// InspectorPanel defaults to Glance tier. These tests verify the panel is visible
/// when an NPC is selected and hidden when selection is cleared.
/// </summary>
[TestFixture]
public class InspectorGlanceTests
{
    private GameObject          _ctrlGo;
    private SelectionController _ctrl;
    private InspectorPanel      _inspector;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _ctrlGo    = new GameObject("InspGlance_Root");
        _ctrl      = _ctrlGo.AddComponent<SelectionController>();
        _inspector = _ctrlGo.AddComponent<InspectorPanel>();
        _ctrl.SelectionChanged += _inspector.OnSelectionChanged;
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("InspGlance_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator GlanceTier_IsDefault()
    {
        // InspectorPanel should default to Glance tier on creation.
        yield return null;
        Assert.AreEqual(InspectorTier.Glance, _inspector.CurrentTier,
            "Inspector should default to Glance tier.");
    }

    [UnityTest]
    public IEnumerator GlanceTier_InspectorVisible_AfterNpcSelected()
    {
        var npcGo  = new GameObject("InspGlance_NPC1");
        var selTag = npcGo.AddComponent<SelectableTag>();
        selTag.Kind        = SelectableKind.Npc;
        selTag.DisplayName = "Donna";

        _ctrl.SetSelection(selTag);
        yield return null;

        Assert.IsTrue(_inspector.IsVisible,
            "Inspector should be visible after selecting an NPC.");
    }

    [UnityTest]
    public IEnumerator GlanceTier_SetExplicitly_RemainsGlance()
    {
        _inspector.SetTier(InspectorTier.Glance);

        var npcGo  = new GameObject("InspGlance_NPC2");
        var selTag = npcGo.AddComponent<SelectableTag>();
        selTag.Kind = SelectableKind.Npc;

        _ctrl.SetSelection(selTag);
        yield return null;

        Assert.AreEqual(InspectorTier.Glance, _inspector.CurrentTier,
            "Explicitly setting Glance tier should keep the tier as Glance.");
    }

    [UnityTest]
    public IEnumerator GlanceTier_ClearSelection_HidesInspector()
    {
        var npcGo  = new GameObject("InspGlance_NPC3");
        var selTag = npcGo.AddComponent<SelectableTag>();
        selTag.Kind = SelectableKind.Npc;

        _ctrl.SetSelection(selTag);
        yield return null;
        Assert.IsTrue(_inspector.IsVisible);

        _ctrl.ClearSelection();
        yield return null;

        Assert.IsFalse(_inspector.IsVisible,
            "Inspector should hide when selection is cleared.");
    }
}
