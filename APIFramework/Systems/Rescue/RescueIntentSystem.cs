using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Systems.Rescue;

/// <summary>
/// Emits Rescue intent for Alive NPCs that are within awareness range of an Incapacitated NPC
/// and whose archetype rescue-score exceeds the configured threshold.
///
/// Runs in Cleanup phase (90), after ActionSelectionSystem (Cognition 30), so it can
/// override the current-tick IntendedActionComponent. RescueExecutionSystem reads the
/// Rescue intents set here in the same phase.
///
/// Score formula:
///   rescueScore = archetypeBias
///                 - distance * 0.05
///                 + (willpower / 100) * 0.3
///                 - acuteStress * 0.005
/// Two fast gates prevent the score computation for NPCs who are clearly unable:
///   willpower &lt; config.MinRescueWillpower → skip
///   acuteStress &gt; config.MaxRescueStress  → skip
///
/// Determinism: iterates rescuers and victims in ascending entity-Id order; ties broken by
/// victim iteration order.
/// Single-rescuer per tick: when a rescuer matches multiple victims, the first (lowest Id)
/// victim wins and the rescuer receives exactly one Rescue intent.
/// </summary>
/// <seealso cref="RescueExecutionSystem"/>
public sealed class RescueIntentSystem : ISystem
{
    private readonly ArchetypeRescueBiasCatalog _catalog;
    private readonly RescueConfig _cfg;

    public RescueIntentSystem(ArchetypeRescueBiasCatalog catalog, RescueConfig cfg)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _cfg     = cfg     ?? throw new ArgumentNullException(nameof(cfg));
    }

    public void Update(EntityManager em, float deltaTime)
    {
        // Collect all Incapacitated NPCs once; sorted for determinism.
        var inNeed = em.Query<NpcTag>()
            .Where(e => e.Has<LifeStateComponent>() &&
                        e.Get<LifeStateComponent>().State == LS.Incapacitated)
            .OrderBy(e => e.Id)
            .ToList();

        if (inNeed.Count == 0) return;

        // Iterate alive potential rescuers in deterministic order.
        var rescuers = em.Query<NpcTag>()
            .Where(e => LifeStateGuard.IsAlive(e))
            .OrderBy(e => e.Id);

        foreach (var rescuer in rescuers)
        {
            if (!rescuer.Has<PositionComponent>()) continue;

            var archetype = rescuer.Has<NpcArchetypeComponent>()
                ? rescuer.Get<NpcArchetypeComponent>().ArchetypeId
                : string.Empty;

            float rescueBias = _catalog.GetBias(archetype);

            // Fast gates before any victim iteration.
            int willpower = rescuer.Has<WillpowerComponent>()
                ? rescuer.Get<WillpowerComponent>().Current
                : 0;
            if (willpower < _cfg.MinRescueWillpower) continue;

            float acuteStress = rescuer.Has<StressComponent>()
                ? rescuer.Get<StressComponent>().AcuteLevel
                : 0f;
            if (acuteStress > _cfg.MaxRescueStress) continue;

            var rescuerPos = rescuer.Get<PositionComponent>();

            // First victim above threshold wins; break after assignment.
            foreach (var victim in inNeed)
            {
                if (victim.Id == rescuer.Id) continue;
                if (!victim.Has<PositionComponent>()) continue;

                var victimPos = victim.Get<PositionComponent>();
                float distance = Distance(rescuerPos, victimPos);

                if (distance > _cfg.AwarenessRangeForRescue) continue;

                float rescueScore = rescueBias
                    - distance * 0.05f
                    + willpower / 100f * 0.3f
                    - acuteStress * 0.005f;

                if (rescueScore <= _cfg.RescueThreshold) continue;

                // Override intent and set movement target toward victim.
                int victimIntId = EntityIntId(victim);
                rescuer.Add(new IntendedActionComponent(
                    Kind:          IntendedActionKind.Rescue,
                    TargetEntityId: victimIntId,
                    Context:       DialogContextValue.None,
                    IntensityHint: 80
                ));
                rescuer.Add(new MovementTargetComponent
                {
                    TargetEntityId = victim.Id,
                    Label          = "rescue",
                });
                break; // One victim per rescuer per tick.
            }
        }
    }

    private static float Distance(PositionComponent a, PositionComponent b) =>
        MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Z - b.Z, 2));

    private static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return BitConverter.ToInt32(b, 0);
    }
}
