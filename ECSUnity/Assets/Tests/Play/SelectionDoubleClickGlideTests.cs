using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-02: Double-click NPC → camera glide event fires with NPC world position.
///
/// SelectionController exposes a SimulateDoubleClick(SelectableTag) test hook that
/// triggers GlideRequested without requiring real mouse input timing.
/// </summary>
[TestFixture]
public class SelectionDoubleClickGlideTests
{
    private GameObject          _ctrlGo;
    private SelectionController _ctrl;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _ctrlGo = new GameObject("SelDbl_Ctrl");
        _ctrl   = _ctrlGo.AddComponent<SelectionController>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("SelDbl_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator SingleClick_GlideNotFired()
    {
        bool glideEventFired = false;
        _ctrl.GlideRequested += _ => glideEventFired = true;

        var npcGo  = new GameObject("SelDbl_NPC1");
        npcGo.transform.position = new Vector3(3f, 0f, 5f);
        var selTag = npcGo.AddComponent<SelectableTag>();
        selTag.Kind = SelectableKind.Npc;

        // Single selection only — no double-click simulation.
        _ctrl.SetSelection(selTag);
        yield return null;

        Assert.IsFalse(glideEventFired,
            "A single SetSelection should not fire GlideRequested.");
    }

    [UnityTest]
    public IEnumerator DoubleClick_GlideEventFires()
    {
        bool    glideEventFired = false;
        Vector3 glidePosition   = Vector3.zero;
        _ctrl.GlideRequested += pos => { glideEventFired = true; glidePosition = pos; };

        var npcGo  = new GameObject("SelDbl_NPC2");
        npcGo.transform.position = new Vector3(3f, 0f, 5f);
        var selTag = npcGo.AddComponent<SelectableTag>();
        selTag.Kind = SelectableKind.Npc;

        // SimulateDoubleClick is the test hook — sets selection AND fires GlideRequested.
        _ctrl.SimulateDoubleClick(selTag);
        yield return null;

        Assert.IsTrue(glideEventFired,
            "SimulateDoubleClick should fire GlideRequested.");
        Assert.AreEqual(npcGo.transform.position.x, glidePosition.x, 0.1f,
            "Glide position X should match NPC world position X.");
        Assert.AreEqual(npcGo.transform.position.z, glidePosition.z, 0.1f,
            "Glide position Z should match NPC world position Z.");
    }

    [UnityTest]
    public IEnumerator GlideEvent_CarriesCorrectPosition_SecondNpc()
    {
        // Verify that the glide position matches the SELECTED npc, not some other object.
        Vector3 glidePosition = Vector3.zero;
        _ctrl.GlideRequested += pos => glidePosition = pos;

        var npc1Go  = new GameObject("SelDbl_NPC3a");
        npc1Go.transform.position = new Vector3(1f, 0f, 1f);
        var sel1    = npc1Go.AddComponent<SelectableTag>();
        sel1.Kind = SelectableKind.Npc;

        var npc2Go  = new GameObject("SelDbl_NPC3b");
        npc2Go.transform.position = new Vector3(9f, 0f, 7f);
        var sel2    = npc2Go.AddComponent<SelectableTag>();
        sel2.Kind = SelectableKind.Npc;

        // Double-click the SECOND NPC.
        _ctrl.SimulateDoubleClick(sel2);
        yield return null;

        Assert.AreEqual(npc2Go.transform.position.x, glidePosition.x, 0.1f,
            "Glide position should correspond to the double-clicked NPC (npc2).");
        Assert.AreEqual(npc2Go.transform.position.z, glidePosition.z, 0.1f,
            "Glide position Z should correspond to the double-clicked NPC (npc2).");
    }
}
