using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-03: Engine ticks deterministically.
/// Over 100 FixedUpdate calls the clock advances exactly 100 ticks.
/// NPC positions change for at least one NPC.
/// </summary>
[TestFixture]
public class EngineHostTickTests
{
    private GameObject _hostGo;
    private EngineHost _host;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _hostGo = new GameObject("TestEngineHost_Tick");
        _host   = _hostGo.AddComponent<EngineHost>();

        var configAsset = ScriptableObject.CreateInstance<SimConfigAsset>();
        var configField = typeof(EngineHost).GetField("_configAsset",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        configField?.SetValue(_host, configAsset);

        var pathField = typeof(EngineHost).GetField("_worldDefinitionPath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        pathField?.SetValue(_host, "");

        yield return null;   // let Start() run
    }

    [TearDown]
    public void TearDown()
    {
        if (_hostGo != null)
            Object.Destroy(_hostGo);
    }

    [UnityTest]
    public IEnumerator After100FixedFrames_TickCountEquals100()
    {
        Assert.IsNotNull(_host.Engine, "Engine must boot before tick tests.");

        // Wait for 100 fixed frames.
        // WaitForFixedUpdate() yields until after the next FixedUpdate, so we loop 100 times.
        for (int i = 0; i < 100; i++)
            yield return new WaitForFixedUpdate();

        Assert.AreEqual(100L, _host.TickCount,
            $"Expected exactly 100 ticks after 100 fixed frames, got {_host.TickCount}.");
    }

    [UnityTest]
    public IEnumerator After100FixedFrames_ClockAdvanced()
    {
        Assert.IsNotNull(_host.Engine, "Engine must boot.");

        double clockBefore = _host.Clock.TotalTime;

        for (int i = 0; i < 100; i++)
            yield return new WaitForFixedUpdate();

        Assert.Greater(_host.Clock.TotalTime, clockBefore,
            "SimulationClock.TotalTime must advance after 100 ticks.");
    }

    [UnityTest]
    public IEnumerator After100FixedFrames_AtLeastOneNpcPositionChanged()
    {
        Assert.IsNotNull(_host.Engine, "Engine must boot.");

        // Snapshot NPC positions before ticking.
        var positionsBefore = CaptureNpcPositions();
        Assert.Greater(positionsBefore.Count, 0, "There must be at least one NPC in the simulation.");

        for (int i = 0; i < 100; i++)
            yield return new WaitForFixedUpdate();

        // Give Update() a chance to refresh WorldState.
        yield return null;

        var positionsAfter = CaptureNpcPositions();
        bool anyChanged = false;
        foreach (var pair in positionsBefore)
        {
            if (!positionsAfter.TryGetValue(pair.Key, out var afterPos)) continue;
            if (Vector3.Distance(pair.Value, afterPos) > 0.001f)
            {
                anyChanged = true;
                break;
            }
        }

        Assert.IsTrue(anyChanged,
            "At least one NPC should have moved position over 100 engine ticks.");
    }

    private System.Collections.Generic.Dictionary<string, UnityEngine.Vector3> CaptureNpcPositions()
    {
        var result = new System.Collections.Generic.Dictionary<string, UnityEngine.Vector3>();
        var ws = _host.WorldState;
        if (ws?.Entities == null) return result;

        foreach (var entity in ws.Entities)
        {
            if (!entity.Position.HasPosition) continue;
            result[entity.Id] = new UnityEngine.Vector3(
                entity.Position.X, entity.Position.Y, entity.Position.Z);
        }
        return result;
    }
}
