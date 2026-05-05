using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Computes the per-NPC physiology-action veto set each tick and writes it to
/// <see cref="BlockedActionsComponent"/>.
///
/// DESIGN
/// ------
/// "Drives are necessary but not sufficient." A social inhibition can override a
/// biological drive: Sally's hunger at 120% with <c>bodyImageEating: 90</c> does
/// not eat. This system is the gate that makes that true.
///
/// For each NPC with <see cref="InhibitionsComponent"/>, and for each physiology
/// action class (Eat, Sleep, Urinate, Defecate), it computes:
///
///   effectiveStrength = inhibitionStrength
///                       × (1 − LowWillpowerLeakage)
///                       × StressLeakageMult
///
/// where:
///   inhibitionStrength    = inhibition.Strength / 100
///   LowWillpowerLeakage   = how much the gate breaks open as willpower drops below
///                           <see cref="PhysiologyGateConfig.LowWillpowerLeakageStart"/>
///   StressLeakageMult     = how much acute stress relaxes the gate
///
/// If effectiveStrength >= <see cref="PhysiologyGateConfig.VetoStrengthThreshold"/>,
/// the action class is added to the blocked set.
///
/// When the resulting set is non-empty, a <see cref="BlockedActionsComponent"/> is
/// written to the entity. When it is empty, any existing component is removed —
/// consumers check <c>entity.Has&lt;BlockedActionsComponent&gt;()</c>.
///
/// PIPELINE POSITION
/// -----------------
/// Cognition (30) — after BrainSystem (which sets DriveComponent.Dominant) and
/// after WillpowerSystem (which drains the queue). Physiology action systems run
/// at Behavior (40) and therefore see the veto set written this tick.
///
/// INHIBITION → PHYSIOLOGY MAPPING (v0.1)
/// ----------------------------------------
///   Eat      ← BodyImageEating
///   Sleep    ← Vulnerability   ("can't be the person who couldn't make the deadline")
///   Urinate  ← PublicEmotion   (holding it through a public-facing scenario)
///   Defecate ← PublicEmotion
/// </summary>
/// <remarks>
/// Reads: <see cref="InhibitionsComponent"/>, <see cref="WillpowerComponent"/>,
/// <see cref="StressComponent"/>, <see cref="LifeStateComponent"/>.<br/>
/// Writes: <see cref="BlockedActionsComponent"/> (single writer; added when veto set
/// is non-empty, removed otherwise).<br/>
/// Phase: Cognition, after <see cref="BrainSystem"/> and before the Behavior phase
/// systems consult the veto set.
/// </remarks>
public class PhysiologyGateSystem : ISystem
{
    private readonly PhysiologyGateConfig _cfg;

    // v0.1 mapping: physiology class → inhibition class
    private static readonly (BlockedActionClass Action, InhibitionClass Inhibition)[] Mappings =
    {
        (BlockedActionClass.Eat,      InhibitionClass.BodyImageEating),
        (BlockedActionClass.Sleep,    InhibitionClass.Vulnerability),
        (BlockedActionClass.Urinate,  InhibitionClass.PublicEmotion),
        (BlockedActionClass.Defecate, InhibitionClass.PublicEmotion),
    };

    /// <summary>Constructs the physiology gate with its tuning.</summary>
    /// <param name="cfg">Physiology-gate tuning (veto threshold, willpower-leakage start, stress relaxation).</param>
    public PhysiologyGateSystem(PhysiologyGateConfig cfg) => _cfg = cfg;

    /// <summary>Per-tick gate computation; writes or removes <see cref="BlockedActionsComponent"/> per NPC.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds, unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        // Only NPCs with InhibitionsComponent are candidates for vetoes.
        foreach (var entity in em.Query<InhibitionsComponent>().ToList())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip non-Alive NPCs
            var inhibitions = entity.Get<InhibitionsComponent>();

            // Read willpower — default to maximum if component absent (no leakage).
            int willpower = entity.Has<WillpowerComponent>()
                ? entity.Get<WillpowerComponent>().Current
                : 100;

            // Read acute stress — default to zero if component absent (no relaxation).
            int acuteStress = entity.Has<StressComponent>()
                ? entity.Get<StressComponent>().AcuteLevel
                : 0;

            double leakage    = LowWillpowerLeakage(willpower, _cfg.LowWillpowerLeakageStart);
            double stressMult = StressLeakageMult(acuteStress, _cfg.StressMaxRelaxation);

            var blocked = new HashSet<BlockedActionClass>();

            foreach (var (actionClass, inhibClass) in Mappings)
            {
                // Find the strongest matching inhibition (there is typically only one per class,
                // but the component allows duplicates; take the highest strength).
                int bestStrength = 0;
                foreach (var inh in inhibitions.Inhibitions)
                {
                    if (inh.Class == inhibClass && inh.Strength > bestStrength)
                        bestStrength = inh.Strength;
                }

                if (bestStrength == 0) continue; // no matching inhibition

                double inhibitionStrength = bestStrength / 100.0;
                double effectiveStrength  = inhibitionStrength
                                           * (1.0 - leakage)
                                           * stressMult;

                if (effectiveStrength >= _cfg.VetoStrengthThreshold)
                    blocked.Add(actionClass);
            }

            // Write or remove BlockedActionsComponent.
            if (blocked.Count > 0)
            {
                entity.Add(new BlockedActionsComponent(blocked));
            }
            else if (entity.Has<BlockedActionsComponent>())
            {
                entity.Remove<BlockedActionsComponent>();
            }
        }

        // Entities without InhibitionsComponent can never be vetoed.
        // Clean up any stale BlockedActionsComponent from entities that lost their
        // InhibitionsComponent between ticks (edge case, but maintain consistency).
        foreach (var entity in em.Query<BlockedActionsComponent>().ToList())
        {
            if (!entity.Has<InhibitionsComponent>())
                entity.Remove<BlockedActionsComponent>();
        }
    }

    // -- Leakage helpers (internal for testability) ----------------------------

    /// <summary>
    /// Fraction [0, 1] by which the veto weakens as willpower falls below the
    /// low-willpower threshold. At or above the threshold: 0 (full veto). At 0: 1 (no veto).
    /// </summary>
    internal static double LowWillpowerLeakage(int willpowerCurrent, int lowThreshold)
    {
        if (willpowerCurrent >= lowThreshold) return 0.0;
        return (lowThreshold - willpowerCurrent) / (double)lowThreshold;
    }

    /// <summary>
    /// Stress-driven relaxation multiplier [0, 1]. At 0 stress: 1.0 (full veto).
    /// At 100 stress: (1 - maxRelaxation) = 0.3 at the default setting.
    /// </summary>
    internal static double StressLeakageMult(int acuteLevel, double maxRelaxation)
    {
        double normalized = Math.Clamp(acuteLevel / 100.0, 0.0, 1.0);
        return 1.0 - normalized * maxRelaxation;
    }
}
