using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-07 (room): Room selected -> RoomInspectorPanel visible; other kinds -> hidden.
///
/// RoomInspectorPanel only shows when Kind == SelectableKind.Room.
/// </summary>
[TestFixture]
public class RoomInspectorTests
{
    private GameObject         _root;
    private RoomInspectorPanel _roomPanel;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _root      = new GameObject("RoomInsp_Root");
        _roomPanel = _root.AddComponent<RoomInspectorPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("RoomInsp_"))
                Object.Destroy(go);
    }

    private SelectableTag MakeTag(SelectableKind kind, string goName)
    {
        var go  = new GameObject(goName);
        var tag = go.AddComponent<SelectableTag>();
        tag.Kind = kind;
        return tag;
    }

    [UnityTest]
    public IEnumerator RoomSelected_PanelVisible()
    {
        var tag = MakeTag(SelectableKind.Room, "RoomInsp_Office");
        _roomPanel.OnSelectionChanged(tag);
        yield return null;

        Assert.IsTrue(_roomPanel.IsVisible,
            "RoomInspectorPanel should be visible when a Room is selected.");
    }

    [UnityTest]
    public IEnumerator NpcSelected_PanelHidden()
    {
        var tag = MakeTag(SelectableKind.Npc, "RoomInsp_Npc");
        _roomPanel.OnSelectionChanged(tag);
        yield return null;

        Assert.IsFalse(_roomPanel.IsVisible,
            "RoomInspectorPanel should be hidden when an NPC is selected.");
    }

    [UnityTest]
    public IEnumerator WorldObjectSelected_PanelHidden()
    {
        var tag = MakeTag(SelectableKind.WorldObject, "RoomInsp_Obj");
        _roomPanel.OnSelectionChanged(tag);
        yield return null;

        Assert.IsFalse(_roomPanel.IsVisible,
            "RoomInspectorPanel should be hidden when a WorldObject is selected.");
    }

    [UnityTest]
    public IEnumerator ClearSelection_PanelHides()
    {
        var tag = MakeTag(SelectableKind.Room, "RoomInsp_Office2");
        _roomPanel.OnSelectionChanged(tag);
        yield return null;
        Assert.IsTrue(_roomPanel.IsVisible);

        _roomPanel.OnSelectionChanged(null);
        yield return null;

        Assert.IsFalse(_roomPanel.IsVisible,
            "RoomInspectorPanel should hide when selection is cleared (null tag).");
    }
}
