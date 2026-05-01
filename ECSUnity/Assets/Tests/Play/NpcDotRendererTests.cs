using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-06: One NpcDotRenderer per NPC.
/// Renderer position tracks NPC PositionComponent over 100 frames (max delta 0.5).
/// </summary>
[TestFixture]
public class NpcDotRendererTests
{
    private GameObject    _hostGo;
    private EngineHost    _host;
    private NpcDotRenderer _renderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _hostGo = new GameObject("TestHost_NpcRenderer");
        _host   = _hostGo.AddComponent<EngineHost>();

        var configAsset = ScriptableObject.CreateInstance<SimConfigAsset>();
        var configField = typeof(EngineHost).GetField("_configAsset",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        configField?.SetValue(_host, configAsset);

        var pathField = typeof(EngineHost).GetField("_worldDefinitionPath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        pathField?.SetValue(_host, "");

        var rendererGo = new GameObject("TestNpcRenderer");
        _renderer = rendererGo.AddComponent<NpcDotRenderer>();
        var hostField = typeof(NpcDotRenderer).GetField("_engineHost",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        hostField?.SetValue(_renderer, _host);

        yield return null;
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        if (_hostGo != null)              Object.Destroy(_hostGo);
        if (_renderer?.gameObject != null) Object.Destroy(_renderer.gameObject);
    }

    [UnityTest]
    public IEnumerator NpcCount_MatchesWorldState()
    {
        yield return null;

        var entities = _host.WorldState?.Entities;
        if (entities == null)
        {
            Assert.Inconclusive("WorldState.Entities is null.");
            yield break;
        }

        int withPos = 0;
        foreach (var e in entities)
            if (e.Position.HasPosition) withPos++;

        Assert.AreEqual(withPos, _renderer.ActiveNpcCount,
            $"Expected {withPos} NPC dot views, got {_renderer.ActiveNpcCount}.");
    }

    [UnityTest]
    public IEnumerator NpcDot_PositionTracksEngine_Over100Frames()
    {
        Assert.IsNotNull(_host.Engine, "Engine must boot.");

        // Run 100 fixed frames.
        for (int i = 0; i < 100; i++)
            yield return new WaitForFixedUpdate();

        yield return null;   // let Update() refresh WorldState

        var entities = _host.WorldState?.Entities;
        if (entities == null || entities.Count == 0)
        {
            Assert.Inconclusive("No entities to track.");
            yield break;
        }

        const float maxDelta = 0.5f;   // lerp tolerance from spec (AT-06)
        int         checked_ = 0;

        foreach (var entity in entities)
        {
            if (!entity.Position.HasPosition) continue;

            var go = _renderer.GetNpcGameObject(entity.Id);
            if (go == null) continue;

            Vector3 expected = new Vector3(entity.Position.X, 0f, entity.Position.Z);
            Vector3 actual   = new Vector3(go.transform.position.x, 0f, go.transform.position.z);
            float   delta    = Vector3.Distance(expected, actual);

            Assert.LessOrEqual(delta, maxDelta,
                $"NPC '{entity.Name}' dot position delta {delta:F3} exceeds tolerance {maxDelta}.");

            checked_++;
        }

        Assert.Greater(checked_, 0, "At least one NPC must have been tracked.");
    }
}
