using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-08: Click-through — when the player clicks an event row in the log,
/// the camera should glide to a relevant position and the inspector panel
/// should pin the first named participant.
///
/// Implementation bridge:
///   EventLogClickThroughHandler.HandleRowClicked(ChronicleEntryDto)
///     → fires GlideTriggered(Vector3)   (camera glide)
///     → calls SelectionController.SetSelection(...)  (inspector pin)
///
/// Requirements covered:
///   - Clicking a valid entry fires GlideTriggered.
///   - Clicking null (defensive path) does not throw.
///   - Clicking a valid entry with a participant sets HasSelection on the controller.
/// </summary>
[TestFixture]
public class EventLogClickThroughTests
{
    private GameObject                  _go;
    private EventLogClickThroughHandler _handler;
    private SelectionController         _ctrl;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go      = new GameObject("EvLogClick_Root");
        _handler = _go.AddComponent<EventLogClickThroughHandler>();
        _ctrl    = _go.AddComponent<SelectionController>();

        // Wire the selection controller so the handler can set a selection.
        _handler.SetSelectionController(_ctrl);

        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        // "EventLog_Pin_" prefix covers any inspector pin objects the handler spawns.
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("EvLogClick_") || go.name.StartsWith("EventLog_Pin_"))
                Object.Destroy(go);
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// The primary user-visible outcome of a row click is the camera glide.
    /// GlideTriggered must fire within the same frame as HandleRowClicked.
    /// </summary>
    [UnityTest]
    public IEnumerator HandleRowClicked_FiresGlideTriggered()
    {
        bool glided = false;
        _handler.GlideTriggered += _ => glided = true;

        var entry = new ChronicleEntryDto
        {
            Id           = "ev-click-01",
            Kind         = ChronicleEventKind.PublicArgument,
            Tick         = 1000,
            Participants = new List<string> { "npc-click-01" },
            Description  = "Test click",
        };

        _handler.HandleRowClicked(entry);
        yield return null;

        Assert.IsTrue(glided,
            "HandleRowClicked should fire GlideTriggered.");
    }

    /// <summary>
    /// The UI row-click handler is called from user-generated events; a null
    /// entry could arrive if something races with a list rebuild. Must not throw.
    /// </summary>
    [UnityTest]
    public IEnumerator HandleRowClicked_Null_DoesNotThrow()
    {
        bool threw = false;
        try { _handler.HandleRowClicked(null); }
        catch { threw = true; }

        yield return null;

        Assert.IsFalse(threw,
            "HandleRowClicked(null) must not throw.");
    }

    /// <summary>
    /// After clicking a row, the inspector panel should show the first
    /// participant. We verify this indirectly through SelectionController.HasSelection.
    /// </summary>
    [UnityTest]
    public IEnumerator HandleRowClicked_SetsSelection()
    {
        var entry = new ChronicleEntryDto
        {
            Id           = "ev-click-02",
            Kind         = ChronicleEventKind.Betrayal,
            Tick         = 2000,
            Participants = new List<string> { "npc-sel-01" },
            Description  = "Betrayal",
        };

        _handler.HandleRowClicked(entry);
        yield return null;

        Assert.IsTrue(_ctrl.HasSelection,
            "HandleRowClicked should pin the inspector (set a selection).");
    }
}
