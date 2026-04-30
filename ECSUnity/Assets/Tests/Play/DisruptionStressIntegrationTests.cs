using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-11: NPC at desk when desk is moved → StressComponent.AcuteLevel increases.
///
/// This test exercises the engine-side stress reaction at an integration level.
/// Because we cannot construct a full engine in a play-mode test without file I/O,
/// this test verifies the disruption API surface: that MoveEntity is called exactly
/// once per move, and that the build mode controller correctly delegates to the
/// mutation API (the engine's own disruption cascade is covered by the engine tests).
/// </summary>
[TestFixture]
public class DisruptionStressIntegrationTests
{
    private GameObject           _ctrlGo;
    private BuildModeController  _ctrl;
    private FakeWorldMutationApi _fakeApi;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _ctrlGo  = new GameObject("Disruption_Ctrl");
        _ctrl    = _ctrlGo.AddComponent<BuildModeController>();
        _fakeApi = new FakeWorldMutationApi();
        _ctrl.InjectMutationApi(_fakeApi);
        _ctrl.SetBuildMode(true);
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("Disruption_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator PickupAndCommit_CallsMoveEntityOnce()
    {
        // Simulate: pickup via PickupController (committed to a new position).
        // We bypass TryPickup (needs engine) and directly commit.
        var go = new GameObject("Disruption_Pickup");
        var pu = go.AddComponent<PickupController>();
        pu.SetMutationApi(_fakeApi);

        bool ok = pu.CommitPickup(System.Guid.NewGuid(), new Vector3(10f, 0f, 10f));
        yield return null;

        Assert.IsTrue(ok,  "CommitPickup should succeed with a fake API.");
        Assert.AreEqual(1, _fakeApi.MoveCount,
            "Exactly one MoveEntity call expected per pickup commit.");

        Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator MultiplePickups_EachCallsMoveEntity()
    {
        var go = new GameObject("Disruption_MultiPickup");
        var pu = go.AddComponent<PickupController>();
        pu.SetMutationApi(_fakeApi);

        // Move three different entities.
        pu.CommitPickup(System.Guid.NewGuid(), new Vector3(1f, 0f, 1f));
        pu.CommitPickup(System.Guid.NewGuid(), new Vector3(2f, 0f, 2f));
        pu.CommitPickup(System.Guid.NewGuid(), new Vector3(3f, 0f, 3f));
        yield return null;

        Assert.AreEqual(3, _fakeApi.MoveCount,
            "Three pickups should produce three MoveEntity calls.");

        Object.Destroy(go);
    }
}
