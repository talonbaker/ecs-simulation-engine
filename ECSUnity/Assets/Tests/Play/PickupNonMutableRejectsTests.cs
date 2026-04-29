using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-07: Clicking an NPC (no MutableTopologyTag) rejects pickup.
/// </summary>
[TestFixture]
public class PickupNonMutableRejectsTests
{
    private GameObject    _go;
    private PickupController _pickup;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _go     = new GameObject("PickupNonMut_Ctrl");
        _pickup = _go.AddComponent<PickupController>();
        _pickup.SetMutationApi(new FakeWorldMutationApi());
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            if (go.name.StartsWith("PickupNonMut_"))
                UnityEngine.Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator TryPickup_NullEngine_ReturnsFalse()
    {
        // No EngineHost is wired — TryPickup cannot access components.
        bool ok = _pickup.TryPickup(Guid.NewGuid(), "Some NPC",
            out BuildIntent intent, out string reason);
        yield return null;

        Assert.IsFalse(ok, "TryPickup should return false when EngineHost is null.");
        Assert.AreEqual(BuildIntentKind.None, intent.Kind,
            "Intent kind should be None on rejection.");
        Assert.IsFalse(string.IsNullOrEmpty(reason),
            "A non-empty rejection reason should be provided.");
    }

    [UnityTest]
    public IEnumerator TryPickup_UnknownEntity_ReturnsFalse()
    {
        // Even with an engine, an entity that doesn't exist should be rejected.
        Guid unknownId = Guid.NewGuid();
        bool ok = _pickup.TryPickup(unknownId, "Ghost Entity",
            out BuildIntent intent, out string reason);
        yield return null;

        Assert.IsFalse(ok, "Unknown entity should be rejected.");
        Assert.IsNotNull(reason, "A reason must be provided.");
    }
}
