using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using APIFramework.Mutation;

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
        foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("GhostValid_"))
                UnityEngine.Object.Destroy(go);
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
    public int  MoveCount           { get; private set; }
    public int  AttachObstacleCount { get; private set; }
    public int  DetachObstacleCount { get; private set; }

    public Guid SpawnStructural(int tileX, int tileY)
    {
        SpawnCount++;
        return Guid.NewGuid();
    }

    public void MoveEntity(Guid entityId, int newTileX, int newTileY) { MoveCount++; }
    public void DespawnStructural(Guid entityId) { }
    public void AttachObstacle(Guid entityId)   { AttachObstacleCount++; }
    public void DetachObstacle(Guid entityId)   { DetachObstacleCount++; }
    public void ChangeRoomBounds(Guid roomId, APIFramework.Components.BoundsRect newBounds) { }
    public void ThrowEntity(Guid entityId, float velocityX, float velocityZ, float velocityY, float decayPerTick) { }
    public Guid SpawnStain(string templateId, int tileX, int tileY) => Guid.Empty;
}
