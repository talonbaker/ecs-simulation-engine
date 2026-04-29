using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-04: Drag wall from palette → ghost appears; valid placement → white tint → click commits via SpawnStructural.
/// </summary>
[TestFixture]
public class GhostPreviewValidPlacementTests
{
    private GameObject          _ctrlGo;
    private BuildModeController _ctrl;
    private GhostPreview        _ghost;
    private FakeWorldMutationApi _fakeApi;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _ctrlGo = new GameObject("GhostValid_Ctrl");
        _ctrl   = _ctrlGo.AddComponent<BuildModeController>();

        var ghostGo = new GameObject("GhostValid_Ghost");
        _ghost      = ghostGo.AddComponent<GhostPreview>();

        var field = typeof(BuildModeController).GetField("_ghost",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_ctrl, _ghost);

        _fakeApi = new FakeWorldMutationApi();
        _ctrl.InjectMutationApi(_fakeApi);
        _ctrl.SetBuildMode(true);
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("GhostValid_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator SelectPalette_GhostBecomesActive()
    {
        var entry = MakeEntry("00000010-0000-0000-0000-000000000001");
        _ctrl.TestSelectPaletteEntry(entry);
        yield return null;

        Assert.IsTrue(_ghost.IsActive, "Ghost should be active after palette item selected.");
    }

    [UnityTest]
    public IEnumerator ValidPlacement_GhostIsWhite()
    {
        var entry = MakeEntry("00000010-0000-0000-0000-000000000001");
        _ctrl.TestSelectPaletteEntry(entry);
        yield return null;

        // Ghost defaults to valid (no validator installed).
        Assert.IsTrue(_ghost.IsShowingValid, "Ghost should show valid (white) tint when no obstacle.");
    }

    [UnityTest]
    public IEnumerator CommitPlacement_CallsSpawnStructural()
    {
        var templateId = Guid.NewGuid();
        var entry      = MakeEntry(templateId.ToString());
        _ctrl.TestSelectPaletteEntry(entry);
        yield return null;

        _ctrl.TestCommitAt(new Vector3(5f, 0f, 5f));
        yield return null;

        Assert.AreEqual(1, _fakeApi.SpawnCount,
            "Committing a valid placement should call SpawnStructural once.");
        Assert.AreEqual(templateId, _fakeApi.LastSpawnedTemplateId,
            "SpawnStructural should be called with the correct template ID.");
    }

    [UnityTest]
    public IEnumerator AfterCommit_IntentIsCleared()
    {
        var entry = MakeEntry("00000010-0000-0000-0000-000000000001");
        _ctrl.TestSelectPaletteEntry(entry);
        yield return null;
        _ctrl.TestCommitAt(new Vector3(5f, 0f, 5f));
        yield return null;

        Assert.AreEqual(BuildIntentKind.None, _ctrl.CurrentIntent.Kind,
            "Intent should be None after a placement commits.");
        Assert.IsFalse(_ghost.IsActive, "Ghost should be deactivated after commit.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PaletteEntry MakeEntry(string templateId) => new PaletteEntry
    {
        Label            = "Test Wall",
        TemplateIdString = templateId,
        Category         = PaletteCategory.Structural,
    };
}

/// <summary>Test double for IWorldMutationApi — records calls without touching the engine.</summary>
public sealed class FakeWorldMutationApi : IWorldMutationApi
{
    public int  SpawnCount          { get; private set; }
    public Guid LastSpawnedTemplateId { get; private set; }
    public int  MoveCount           { get; private set; }
    public int  AttachObstacleCount { get; private set; }
    public int  DetachObstacleCount { get; private set; }

    public Guid SpawnStructural(Guid templateId, int tileX, int tileY)
    {
        SpawnCount++;
        LastSpawnedTemplateId = templateId;
        return Guid.NewGuid();
    }

    public bool MoveEntity(Guid entityId, int newTileX, int newTileY) { MoveCount++; return true; }
    public bool DespawnStructural(Guid entityId) => true;
    public bool AttachObstacle(Guid entityId)   { AttachObstacleCount++; return true; }
    public bool DetachObstacle(Guid entityId)   { DetachObstacleCount++; return true; }
    public bool ChangeRoomBounds(Guid roomId, APIFramework.Components.BoundsRect newBounds) => true;
}
