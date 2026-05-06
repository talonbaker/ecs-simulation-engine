using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-02: Silhouette transform tracks NPC PositionComponent across 100 frames.
/// Maximum delta ≤ 0.5 world-units (same tolerance as NpcDotRendererTests).
///
/// Position mapping: Engine X → Unity X, Engine Z → Unity Z, Y = 0 (floor plane).
/// The silhouette renderer places the root at Y=0, unlike the dot renderer's
/// Y=0.5 elevation. Tests use the XZ plane distance to match spec AT-02.
/// </summary>
[TestFixture]
public class NpcSilhouetteRendererTrackTests
{
    private const float MaxDelta = 0.5f;

    private GameObject            _hostGo;
    private EngineHost            _host;
    private NpcSilhouetteRenderer _renderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _hostGo = new GameObject("TestHost_SilhouetteTrack");
        _host   = _hostGo.AddComponent<EngineHost>();

        var configAsset = ScriptableObject.CreateInstance<SimConfigAsset>();
        SetField(_host, "_configAsset", configAsset);
        SetField(_host, "_worldDefinitionPath", "office-starter.json");

        var rendererGo = new GameObject("TestSilhouetteTrackRenderer");
        _renderer = rendererGo.AddComponent<NpcSilhouetteRenderer>();
        SetField(_renderer, "_engineHost", _host);

        yield return null;  // Start()
    }

    [TearDown]
    public void TearDown()
    {
        if (_hostGo != null)               Object.Destroy(_hostGo);
        if (_renderer?.gameObject != null) Object.Destroy(_renderer.gameObject);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator SilhouetteTransform_TracksEnginePosition_Over100Frames()
    {
        Assert.IsNotNull(_host.Engine, "Engine must boot for tracking test.");

        // Run 100 fixed-update steps.
        for (int i = 0; i < 100; i++)
            yield return new WaitForFixedUpdate();

        // One render frame to let LateUpdate sync positions.
        yield return null;

        var entities = _host.WorldState?.Entities;
        if (entities == null || entities.Count == 0)
        {
            Assert.Inconclusive("No entities to track — run with office-starter.json.");
            yield break;
        }

        int checked_ = 0;
        foreach (var entity in entities)
        {
            if (!entity.Position.HasPosition) continue;

            var go = _renderer.GetNpcGameObject(entity.Id);
            if (go == null) continue;

            // Expected XZ position from WorldStateDto
            var expected = new Vector3(entity.Position.X, 0f, entity.Position.Z);
            var actual   = new Vector3(go.transform.position.x, 0f, go.transform.position.z);
            float delta  = Vector3.Distance(expected, actual);

            Assert.LessOrEqual(delta, MaxDelta,
                $"NPC '{entity.Name}' silhouette XZ delta {delta:F3} exceeds tolerance {MaxDelta}. " +
                $"Expected ({expected.x:F2}, {expected.z:F2}), " +
                $"Actual ({actual.x:F2}, {actual.z:F2}).");

            checked_++;
        }

        Assert.Greater(checked_, 0,
            "At least one NPC must have been tracked. Ensure office-starter.json loads.");
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    private static void SetField(object target, string name, object value)
    {
        var f = target.GetType().GetField(name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f?.SetValue(target, value);
    }
}
