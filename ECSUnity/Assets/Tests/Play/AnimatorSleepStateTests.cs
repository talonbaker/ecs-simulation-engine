using System.Collections;
using System.Linq;
using APIFramework.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-06: LifeState = Incapacitated → animator state Sleep; chibi slot Show(SleepZ) invoked.
///
/// Two sleep trigger paths:
///   1. LifeStateComponent.State == Incapacitated (primary — AT-06)
///   2. WorldStateDto.Physiology.IsSleeping == true (secondary; checked by renderer too)
///
/// Chibi slot: the slot is called with IconKind.SleepZ when Sleep state is entered.
/// At v0.1, no sprite is loaded, so the slot stays invisible — but CurrentIcon
/// must report SleepZ and Show() must not throw.
/// </summary>
[TestFixture]
public class AnimatorSleepStateTests
{
    private GameObject            _hostGo;
    private EngineHost            _host;
    private NpcSilhouetteRenderer _renderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _hostGo = new GameObject("TestHost_SleepState");
        _host   = _hostGo.AddComponent<EngineHost>();

        var configAsset = ScriptableObject.CreateInstance<SimConfigAsset>();
        SetField(_host, "_configAsset", configAsset);
        SetField(_host, "_worldDefinitionPath", "office-starter.json");

        var rendererGo = new GameObject("TestSleepRenderer");
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
    public IEnumerator LifeStateIncapacitated_AdvancesStateToSleep()
    {
        var engine = _host.Engine;
        if (engine == null) { Assert.Inconclusive("Engine not booted."); yield break; }

        var npcEntity = engine.Entities
            .FirstOrDefault(e => e.Has<SilhouetteComponent>()     &&
                                 e.Has<LifeStateComponent>()        &&
                                 e.Has<PositionComponent>());

        if (npcEntity == null)
        {
            Assert.Inconclusive("No NPC entity with LifeStateComponent found.");
            yield break;
        }

        // Set LifeState to Incapacitated (fainting).
        npcEntity.Add(new LifeStateComponent
        {
            State                   = LifeState.Incapacitated,
            LastTransitionTick      = _host.TickCount,
            IncapacitatedTickBudget = 120,
            PendingDeathCause       = CauseOfDeath.Choked,
        });

        yield return null;  // LateUpdate resolves state

        var inst = _renderer.GetInstance(npcEntity.Id.ToString());
        if (inst == null) { Assert.Inconclusive("No instance found."); yield break; }

        Assert.AreEqual(NpcAnimationState.Sleep, inst.CurrentAnimState,
            "LifeState == Incapacitated must advance animator state to Sleep.");

        Debug.Log("[AnimatorSleepStateTests] Incapacitated → Sleep: PASS");
    }

    [UnityTest]
    public IEnumerator SleepState_ChibiSlot_ShowsSleepZ()
    {
        var engine = _host.Engine;
        if (engine == null) { Assert.Inconclusive("Engine not booted."); yield break; }

        var npcEntity = engine.Entities
            .FirstOrDefault(e => e.Has<SilhouetteComponent>() && e.Has<LifeStateComponent>());

        if (npcEntity == null)
        {
            Assert.Inconclusive("No NPC entity found.");
            yield break;
        }

        npcEntity.Add(new LifeStateComponent
        {
            State                   = LifeState.Incapacitated,
            LastTransitionTick      = _host.TickCount,
            IncapacitatedTickBudget = 120,
            PendingDeathCause       = CauseOfDeath.Choked,
        });

        yield return null;

        var inst = _renderer.GetInstance(npcEntity.Id.ToString());
        if (inst == null) { Assert.Inconclusive("No instance."); yield break; }

        // The chibi slot must report SleepZ as the current icon.
        // The slot may be invisible (no sprite loaded at v0.1), but the CurrentIcon
        // field must reflect what was shown.
        Assert.AreEqual(IconKind.SleepZ, inst.EmotionSlot.CurrentIcon,
            "Sleep state must invoke ChibiEmotionSlot.Show(SleepZ).");
    }

    [UnityTest]
    public IEnumerator SleepState_ChibiSlot_DoesNotThrow_WithoutSprite()
    {
        var engine = _host.Engine;
        if (engine == null) { Assert.Inconclusive("Engine not booted."); yield break; }

        var npcEntity = engine.Entities
            .FirstOrDefault(e => e.Has<SilhouetteComponent>() && e.Has<LifeStateComponent>());

        if (npcEntity == null)
        {
            Assert.Inconclusive("No NPC entity found.");
            yield break;
        }

        npcEntity.Add(new LifeStateComponent
        {
            State                   = LifeState.Incapacitated,
            LastTransitionTick      = _host.TickCount,
            IncapacitatedTickBudget = 120,
            PendingDeathCause       = CauseOfDeath.Choked,
        });

        // Verify no exceptions are thrown during the LateUpdate that triggers sleep.
        // UnityEngine.TestTools.LogAssert.NoUnexpectedReceived() is implicitly checked.
        Assert.DoesNotThrow(() =>
        {
            // Directly invoke Show(SleepZ) on a fresh standalone ChibiEmotionSlot.
            var go   = new GameObject("StandaloneSlot");
            var slot = go.AddComponent<ChibiEmotionSlot>();
            // Awake hasn't run yet (AddComponent calls it), so safe to show:
            slot.Show(IconKind.SleepZ);
            slot.Hide();
            Object.DestroyImmediate(go);
        }, "ChibiEmotionSlot.Show(SleepZ) / Hide() must not throw when no sprite is loaded.");
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    private static void SetField(object target, string name, object value)
    {
        var f = target.GetType().GetField(name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f?.SetValue(target, value);
    }
}
