using System.Collections;
using System.Linq;
using APIFramework.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-05: IsChokingTag → animator state Panic; facing locked forward.
///
/// Two paths to Panic:
///   1. ChokingComponent added to entity (primary path, AT-05)
///   2. MoodComponent.PanicLevel >= 0.5 (secondary path)
///
/// Facing lock: NpcAnimatorController captures the facing at Panic entry and
/// holds it for subsequent frames. We verify the root's rotation does not change
/// while in Panic state even if we write a new facing to the entity.
/// </summary>
[TestFixture]
public class AnimatorPanicStateTests
{
    private GameObject            _hostGo;
    private EngineHost            _host;
    private NpcSilhouetteRenderer _renderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _hostGo = new GameObject("TestHost_PanicState");
        _host   = _hostGo.AddComponent<EngineHost>();

        var configAsset = ScriptableObject.CreateInstance<SimConfigAsset>();
        SetField(_host, "_configAsset", configAsset);
        SetField(_host, "_worldDefinitionPath", "office-starter.json");

        var rendererGo = new GameObject("TestPanicRenderer");
        _renderer = rendererGo.AddComponent<NpcSilhouetteRenderer>();
        SetField(_renderer, "_engineHost", _host);

        yield return null;  // Start()
        yield return null;  // First LateUpdate
    }

    [TearDown]
    public void TearDown()
    {
        if (_hostGo != null)               Object.Destroy(_hostGo);
        if (_renderer?.gameObject != null) Object.Destroy(_renderer.gameObject);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ChokingComponent_AdvancesStateToPanic()
    {
        var engine = _host.Engine;
        if (engine == null) { Assert.Inconclusive("Engine not booted."); yield break; }

        var npcEntity = engine.Entities
            .FirstOrDefault(e => e.Has<SilhouetteComponent>() && e.Has<PositionComponent>());

        if (npcEntity == null)
        {
            Assert.Inconclusive("No NPC entity found. Run with office-starter.json.");
            yield break;
        }

        // Add ChokingComponent to the entity (simulates an active choke event).
        npcEntity.Add(new ChokingComponent
        {
            ChokeStartTick = _host.TickCount,
            RemainingTicks = 60,
            BolusSize      = 10f,
            PendingCause   = CauseOfDeath.Choked,
        });

        yield return null;  // LateUpdate

        var inst = _renderer.GetInstance(npcEntity.Id.ToString());
        if (inst == null) { Assert.Inconclusive("No instance found."); yield break; }

        Assert.AreEqual(NpcAnimationState.Panic, inst.CurrentAnimState,
            "ChokingComponent must advance animator state to Panic.");

        Debug.Log("[AnimatorPanicStateTests] ChokingComponent → Panic: PASS");
    }

    [UnityTest]
    public IEnumerator HighPanicLevel_AdvancesStateToPanic()
    {
        var engine = _host.Engine;
        if (engine == null) { Assert.Inconclusive("Engine not booted."); yield break; }

        var npcEntity = engine.Entities
            .FirstOrDefault(e => e.Has<SilhouetteComponent>() && e.Has<MoodComponent>());

        if (npcEntity == null)
        {
            Assert.Inconclusive("No NPC entity with MoodComponent found.");
            yield break;
        }

        // Set PanicLevel to 0.8 (above the 0.5 threshold).
        var mood = npcEntity.Get<MoodComponent>();
        mood.PanicLevel = 0.8f;
        npcEntity.Add(mood);

        yield return null;

        var inst = _renderer.GetInstance(npcEntity.Id.ToString());
        if (inst == null) { Assert.Inconclusive("No instance found."); yield break; }

        Assert.AreEqual(NpcAnimationState.Panic, inst.CurrentAnimState,
            "PanicLevel >= 0.5 must advance animator state to Panic.");
    }

    [UnityTest]
    public IEnumerator PanicState_FacingIsLocked_AcrossMultipleFrames()
    {
        var engine = _host.Engine;
        if (engine == null) { Assert.Inconclusive("Engine not booted."); yield break; }

        var npcEntity = engine.Entities
            .FirstOrDefault(e => e.Has<SilhouetteComponent>() && e.Has<PositionComponent>());

        if (npcEntity == null)
        {
            Assert.Inconclusive("No NPC entity found.");
            yield break;
        }

        // Trigger panic.
        npcEntity.Add(new ChokingComponent
        {
            ChokeStartTick = 0,
            RemainingTicks = 120,
            BolusSize      = 10f,
            PendingCause   = CauseOfDeath.Choked,
        });

        yield return null;  // Enter panic; NpcAnimatorController captures facing

        var inst = _renderer.GetInstance(npcEntity.Id.ToString());
        if (inst == null) { Assert.Inconclusive("No instance."); yield break; }
        Assert.AreEqual(NpcAnimationState.Panic, inst.CurrentAnimState);

        float lockedFacing = inst.transform.eulerAngles.y;

        // Update the ECS FacingComponent to a different direction.
        npcEntity.Add(new FacingComponent
        {
            DirectionDeg = (lockedFacing + 90f) % 360f,
            Source       = FacingSource.MovementVelocity,
        });

        // Wait 3 more frames — the facing should stay locked.
        for (int i = 0; i < 3; i++)
            yield return null;

        float actualFacing = inst.transform.eulerAngles.y;

        // Allow a tiny float drift; the lock should hold the original facing.
        Assert.AreEqual(lockedFacing, actualFacing, 1f,
            $"Panic facing should be locked. Expected ~{lockedFacing:F1}°, got {actualFacing:F1}°.");
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    private static void SetField(object target, string name, object value)
    {
        var f = target.GetType().GetField(name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f?.SetValue(target, value);
    }
}
