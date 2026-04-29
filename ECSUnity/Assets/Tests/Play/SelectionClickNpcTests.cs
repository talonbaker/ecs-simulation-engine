using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-01: Click NPC → selection set; halo+outline appears; inspector slides in.
/// </summary>
[TestFixture]
public class SelectionClickNpcTests
{
    private GameObject          _ctrlGo;
    private SelectionController _ctrl;
    private SelectionHaloRenderer _halo;
    private InspectorPanel      _inspector;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _ctrlGo    = new GameObject("SelClick_Ctrl");
        _ctrl      = _ctrlGo.AddComponent<SelectionController>();
        _halo      = _ctrlGo.AddComponent<SelectionHaloRenderer>();
        _inspector = _ctrlGo.AddComponent<InspectorPanel>();

        // Wire inspector to selection events.
        _ctrl.SelectionChanged += _inspector.OnSelectionChanged;

        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("SelClick_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator NoSelection_InspectorHidden()
    {
        yield return null;
        Assert.IsFalse(_inspector.IsVisible, "Inspector should be hidden with no selection.");
    }

    [UnityTest]
    public IEnumerator ProgrammaticSelect_SelectionSet()
    {
        var npcGo  = new GameObject("SelClick_NPC");
        var selTag = npcGo.AddComponent<SelectableTag>();
        selTag.Kind        = SelectableKind.Npc;
        selTag.EntityId    = "00000001-0000-0000-0000-000000000001";
        selTag.DisplayName = "Donna";

        _ctrl.SetSelection(selTag);
        yield return null;

        Assert.IsNotNull(_ctrl.Current, "Selection should be set.");
        Assert.AreEqual("Donna", _ctrl.Current.DisplayName, "Selected NPC name should match.");
        Assert.IsTrue(_ctrl.HasSelection, "HasSelection should be true.");
    }

    [UnityTest]
    public IEnumerator ProgrammaticSelect_InspectorVisible()
    {
        var npcGo  = new GameObject("SelClick_NPC2");
        var selTag = npcGo.AddComponent<SelectableTag>();
        selTag.Kind     = SelectableKind.Npc;
        selTag.EntityId = "00000001-0000-0000-0000-000000000002";

        _ctrl.SetSelection(selTag);
        yield return null;

        Assert.IsTrue(_inspector.IsVisible, "Inspector should be visible after NPC selection.");
    }

    [UnityTest]
    public IEnumerator ClearSelection_InspectorHides()
    {
        var npcGo  = new GameObject("SelClick_NPC3");
        var selTag = npcGo.AddComponent<SelectableTag>();
        selTag.Kind = SelectableKind.Npc;

        _ctrl.SetSelection(selTag);
        yield return null;
        Assert.IsTrue(_inspector.IsVisible);

        _ctrl.ClearSelection();
        yield return null;
        Assert.IsFalse(_inspector.IsVisible, "Inspector should hide after selection cleared.");
    }

    [UnityTest]
    public IEnumerator SelectionChanged_EventFires()
    {
        bool eventFired = false;
        _ctrl.SelectionChanged += _ => eventFired = true;

        var npcGo  = new GameObject("SelClick_NPC4");
        var selTag = npcGo.AddComponent<SelectableTag>();

        _ctrl.SetSelection(selTag);
        yield return null;

        Assert.IsTrue(eventFired, "SelectionChanged event should fire on selection.");
    }
}
