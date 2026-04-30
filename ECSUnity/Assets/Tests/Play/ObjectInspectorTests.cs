using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-07 (object): WorldObject selected -> ObjectInspectorPanel visible; other kinds -> hidden.
///
/// ObjectInspectorPanel only shows when Kind == SelectableKind.WorldObject.
/// </summary>
[TestFixture]
public class ObjectInspectorTests
{
    private GameObject           _root;
    private ObjectInspectorPanel _objPanel;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _root     = new GameObject("ObjInsp_Root");
        _objPanel = _root.AddComponent<ObjectInspectorPanel>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("ObjInsp_"))
                Object.Destroy(go);
    }

    // Helper: create a SelectableTag with the given kind.
    private SelectableTag MakeTag(SelectableKind kind, string goName)
    {
        var go  = new GameObject(goName);
        var tag = go.AddComponent<SelectableTag>();
        tag.Kind = kind;
        return tag;
    }

    [UnityTest]
    public IEnumerator ObjectSelected_PanelVisible()
    {
        var tag = MakeTag(SelectableKind.WorldObject, "ObjInsp_Desk");
        _objPanel.OnSelectionChanged(tag);
        yield return null;

        Assert.IsTrue(_objPanel.IsVisible,
            "ObjectInspectorPanel should be visible when a WorldObject is selected.");
    }

    [UnityTest]
    public IEnumerator NpcSelected_PanelHidden()
    {
        var tag = MakeTag(SelectableKind.Npc, "ObjInsp_Npc");
        _objPanel.OnSelectionChanged(tag);
        yield return null;

        Assert.IsFalse(_objPanel.IsVisible,
            "ObjectInspectorPanel should be hidden when an NPC is selected.");
    }

    [UnityTest]
    public IEnumerator RoomSelected_PanelHidden()
    {
        var tag = MakeTag(SelectableKind.Room, "ObjInsp_Room");
        _objPanel.OnSelectionChanged(tag);
        yield return null;

        Assert.IsFalse(_objPanel.IsVisible,
            "ObjectInspectorPanel should be hidden when a Room is selected.");
    }

    [UnityTest]
    public IEnumerator ClearSelection_PanelHides()
    {
        // Select a world object, then clear -- panel should hide.
        var tag = MakeTag(SelectableKind.WorldObject, "ObjInsp_Desk2");
        _objPanel.OnSelectionChanged(tag);
        yield return null;
        Assert.IsTrue(_objPanel.IsVisible);

        _objPanel.OnSelectionChanged(null);
        yield return null;

        Assert.IsFalse(_objPanel.IsVisible,
            "ObjectInspectorPanel should hide when selection is cleared (null tag).");
    }
}
