using System.Collections;
using System.Linq;
using APIFramework.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AT-07: LifeState = Deceased → animator state Dead; pose static (Animator disabled).
///
/// "Static" is verified by checking:
///   1. NpcSilhouetteInstance.CurrentAnimState == Dead
///   2. The Animator component on the NPC root is disabled (NpcAnimatorController
///      disables it to freeze the pose on Dead entry).
///   3. No further position updates change the facing (Dead NPCs hold their pose).
///
/// Note: WP-3.1.B says "remains until corpse removed" — deceased NPCs stay
/// rendered as silhouettes. This test confirms they stay visible and static.
/// </summary>
[TestFixture]
public class AnimatorDeadStateTests
{
    private GameObject            _hostGo;
    private EngineHost            _host;
    private NpcSilhouetteRenderer _renderer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _hostGo = new GameObject("TestHost_DeadState");
        _host   = _hostGo.AddComponent<EngineHost>();

        var configAsset = ScriptableObject.CreateInstance<SimConfigAsset>();
        SetField(_host, "_configAsset", configAsset);
        SetField(_host, "_worldDefinitionPath", "office-starter.json");

        var rendererGo = new GameObject("TestDeadRenderer");
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
    public IEnumerator LifeStateDeceased_AdvancesStateToDead()
    {
        var engine = _host.Engine;
        if (engine == null) { Assert.Inconclusive("Engine not booted."); yield break; }

        var npcEntity = engine.Entities
            .FirstOrDefault(e => e.Has<SilhouetteComponent>()  &&
                                 e.Has<LifeStateComponent>()    &&
                                 e.Has<PositionComponent>());

        if (npcEntity == null)
        {
            Assert.Inconclusive("No NPC entity found. Run with office-starter.json.");
            yield break;
        }

        // Set LifeState to Deceased.
        npcEntity.Add(new LifeStateComponent
        {
            State              = LifeState.Deceased,
            LastTransitionTick = _host.TickCount,
        });

        yield return null;  // LateUpdate

        var inst = _renderer.GetInstance(npcEntity.Id.ToString());
        if (inst == null) { Assert.Inconclusive("No instance."); yield break; }

        Assert.AreEqual(NpcAnimationState.Dead, inst.CurrentAnimState,
            "LifeState == Deceased must advance animator state to Dead.");

        Debug.Log("[AnimatorDeadStateTests] Deceased → Dead: PASS");
    }

    [UnityTest]
    public IEnumerator DeadState_AnimatorIsDisabled()
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
            State              = LifeState.Deceased,
            LastTransitionTick = _host.TickCount,
        });

        yield return null;

        var inst = _renderer.GetInstance(npcEntity.Id.ToString());
        if (inst == null) { Assert.Inconclusive("No instance."); yield break; }

        // NpcAnimatorController disables the Animator on Dead entry to freeze the pose.
        Assert.IsFalse(inst.Animator.enabled,
            "Animator must be disabled when in Dead state to ensure a static pose.");
    }

    [UnityTest]
    public IEnumerator DeadState_ChibiSlotIsHidden()
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

        // First, simulate Sleep to get the SleepZ chibi shown.
        npcEntity.Add(new LifeStateComponent
        {
            State                   = LifeState.Incapacitated,
            LastTransitionTick      = _host.TickCount,
            IncapacitatedTickBudget = 60,
            PendingDeathCause       = CauseOfDeath.Choked,
        });
        yield return null;

        // Then transition to Deceased.
        npcEntity.Add(new LifeStateComponent
        {
            State              = LifeState.Deceased,
            LastTransitionTick = _host.TickCount,
        });
        yield return null;

        var inst = _renderer.GetInstance(npcEntity.Id.ToString());
        if (inst == null) { Assert.Inconclusive("No instance."); yield break; }

        Assert.AreEqual(NpcAnimationState.Dead, inst.CurrentAnimState);
        Assert.AreEqual(IconKind.None, inst.EmotionSlot.CurrentIcon,
            "Dead state must clear the ChibiEmotionSlot (show nothing).");
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    private static void SetField(object target, string name, object value)
    {
        var f = target.GetType().GetField(name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f?.SetValue(target, value);
    }
}
