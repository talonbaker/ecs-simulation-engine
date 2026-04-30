using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-09: Click-through for deceased or already-departed NPCs.
///
/// When the player clicks a DeathOrLeaving event, the NPC named in Participants
/// is no longer in the world state (no active entity to glide to). The handler
/// must still fire GlideTriggered — directing the camera to a reasonable
/// fallback position — rather than silently failing or throwing.
///
/// Fallback position contract:
///   When EngineHost is null or the participant cannot be resolved to a live
///   entity position, EventLogClickThroughHandler falls back to (5, 0, 5).
///   This puts the camera near the centre of a typical office floor layout.
///   Y must be 0 (the simulation world lives on the XZ plane; Y is height).
///
/// Design note:
///   These tests deliberately leave _handler.SetHost() uncalled (host == null).
///   That simulates the deceased-NPC path without needing a full EngineHost.
///   In production the code path is: entity lookup fails → fallback position used.
///
/// Requirements covered:
///   - GlideTriggered fires even when the participant no longer exists.
///   - The glide target is a valid world position (not Vector3.negativeInfinity).
///   - The glide target is on the Y=0 plane.
/// </summary>
[TestFixture]
public class EventLogClickThroughDeceasedTests
{
    private GameObject                  _go;
    private EventLogClickThroughHandler _handler;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go      = new GameObject("EvLogDead_Root");
        _handler = _go.AddComponent<EventLogClickThroughHandler>();
        // Intentionally do NOT call SetHost — simulates deceased NPC absent
        // from the current world state.
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("EvLogDead_") || go.name.StartsWith("EventLog_Pin_"))
                Object.Destroy(go);
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Clicking a DeathOrLeaving entry with a null EngineHost must still result
    /// in GlideTriggered firing. The player should always get a camera response.
    /// </summary>
    [UnityTest]
    public IEnumerator DeceasedParticipant_GlideFires()
    {
        bool glided = false;
        _handler.GlideTriggered += _ => glided = true;

        var entry = new ChronicleEntryDto
        {
            Id           = "death-ev-01",
            Kind         = ChronicleEventKind.DeathOrLeaving,
            Tick         = 5000,
            Participants = new List<string> { "deceased-npc-99" },
            Description  = "Passed away",
        };

        _handler.HandleRowClicked(entry);
        yield return null;

        Assert.IsTrue(glided,
            "Clicking a death event should still fire GlideTriggered (to fallback position).");
    }

    /// <summary>
    /// With no EngineHost, the handler falls back to (5, 0, 5).
    /// We do not hard-code X/Z (they are a design detail that may shift),
    /// but we do assert:
    ///   (a) the position is not the sentinel negativeInfinity (i.e. it was set)
    ///   (b) Y == 0 (camera glides on the ground plane)
    /// </summary>
    [UnityTest]
    public IEnumerator NullHost_GlideFallbackPosition()
    {
        Vector3 receivedPos = Vector3.negativeInfinity;
        _handler.GlideTriggered += pos => receivedPos = pos;

        var entry = new ChronicleEntryDto
        {
            Id           = "death-ev-02",
            Kind         = ChronicleEventKind.DeathOrLeaving,
            Tick         = 6000,
            Participants = new List<string> { "ghost-npc" },
            Description  = "Ghost",
        };

        _handler.HandleRowClicked(entry);
        yield return null;

        // Verify the position was set to something (not the sentinel).
        Assert.AreNotEqual(Vector3.negativeInfinity, receivedPos,
            "Glide position must not be negativeInfinity even when host is null.");

        // Verify it is on the ground plane — the simulation world uses Y=0.
        Assert.AreEqual(0f, receivedPos.y, 0.001f,
            "Glide position Y must be 0 (top-down view, Y=0 plane).");
    }
}
