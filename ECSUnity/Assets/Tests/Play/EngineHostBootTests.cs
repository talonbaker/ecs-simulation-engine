using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-02: EngineHost.Start boots the engine without exceptions.
/// Asserts entity count >= 30 and clock starts at tick 0.
/// </summary>
[TestFixture]
public class EngineHostBootTests
{
    private GameObject _hostGo;
    private EngineHost _host;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Create an EngineHost with a default SimConfigAsset.
        _hostGo = new GameObject("TestEngineHost");
        _host   = _hostGo.AddComponent<EngineHost>();

        // We must inject a SimConfigAsset via reflection since it is serialised private.
        // In tests, we create a ScriptableObject instance directly (no asset file needed).
        var configAsset = ScriptableObject.CreateInstance<SimConfigAsset>();
        var configField = typeof(EngineHost).GetField("_configAsset",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        configField?.SetValue(_host, configAsset);

        // Use an empty world definition path — engine will use SpawnWorld() defaults.
        var pathField = typeof(EngineHost).GetField("_worldDefinitionPath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        pathField?.SetValue(_host, "");

        // Wait one frame for Start() to run.
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        if (_hostGo != null)
            Object.Destroy(_hostGo);
    }

    [UnityTest]
    public IEnumerator Boot_DoesNotThrow()
    {
        // If Start() threw, the engine would not be alive. Check it survived.
        Assert.IsNotNull(_host.Engine,
            "EngineHost.Engine should be non-null after successful boot.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Boot_EntityCount_IsAtLeastThirty()
    {
        Assert.IsNotNull(_host.Engine, "Engine must boot successfully.");
        int entityCount = _host.Engine.Entities.Count;
        Assert.GreaterOrEqual(entityCount, 30,
            $"Expected at least 30 entities after boot, got {entityCount}.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Boot_TickCount_StartsAtZero()
    {
        // Tick count is 0 before any FixedUpdate fires.
        // After Start() but before the first FixedUpdate, count should still be 0.
        Assert.AreEqual(0L, _host.TickCount,
            "EngineHost.TickCount should start at 0 on boot (before first FixedUpdate).");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Boot_WorldState_IsNonNull()
    {
        Assert.IsNotNull(_host.WorldState,
            "WorldState should be a non-null WorldStateDto immediately after boot.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Boot_WorldState_HasNonNullClock()
    {
        Assert.IsNotNull(_host.WorldState?.Clock,
            "WorldState.Clock should be non-null after boot.");
        yield return null;
    }
}
