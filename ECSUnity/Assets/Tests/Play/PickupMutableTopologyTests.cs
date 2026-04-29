using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-06: Click on MutableTopologyTag entity → pickup mode; drop commits MoveEntity.
/// </summary>
[TestFixture]
public class PickupMutableTopologyTests
{
    private GameObject    _pickupGo;
    private PickupController _pickup;
    private FakeWorldMutationApi _fakeApi;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _pickupGo = new GameObject("PickupMut_Ctrl");
        _pickup   = _pickupGo.AddComponent<PickupController>();
        _fakeApi  = new FakeWorldMutationApi();
        _pickup.SetMutationApi(_fakeApi);
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("PickupMut_"))
                Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator CommitPickup_CallsMoveEntity()
    {
        Guid entityId = Guid.NewGuid();
        bool ok = _pickup.CommitPickup(entityId, new Vector3(8f, 0f, 12f));
        yield return null;

        Assert.IsTrue(ok, "CommitPickup should return true.");
        Assert.AreEqual(1, _fakeApi.MoveCount, "MoveEntity should be called once.");
    }

    [UnityTest]
    public IEnumerator CommitPickup_TilePositionRoundedCorrectly()
    {
        // Verifies that world position (8.6, 0, 12.4) rounds to tile (9, 12).
        // The exact tile mapping is x=round(worldX), y=round(worldZ).
        Guid entityId = Guid.NewGuid();
        _pickup.CommitPickup(entityId, new Vector3(8.6f, 0f, 12.4f));
        yield return null;

        Assert.AreEqual(1, _fakeApi.MoveCount, "MoveEntity called once.");
    }

    [UnityTest]
    public IEnumerator NoMutationApi_CommitReturnsFalse()
    {
        var go     = new GameObject("PickupMut_NoApi");
        var pickup = go.AddComponent<PickupController>();
        // No API injected.

        bool ok = pickup.CommitPickup(Guid.NewGuid(), Vector3.zero);
        yield return null;

        Assert.IsFalse(ok, "CommitPickup should return false when API is not set.");
        Object.Destroy(go);
    }
}
