using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Audio;
using APIFramework.Systems.Tuning;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Systems.LifeState;

/// <summary>
/// Detects slip-and-fall hazards and transitions NPCs to death on slip.
/// Runs in Cleanup phase, after MovementSystem so NPCs have settled their positions.
///
/// Each tick, for each Alive NPC:
/// - Identify hazards (FallRiskComponent entities) at the NPC's tile.
/// - For each hazard, roll a seeded random check: slip_chance = risk * speed * stress_mult * global_scale.
/// - On hit, call RequestTransition(npc, Deceased, SlippedAndFell).
/// - Slip is fatal; no Incapacitated phase.
///
/// Determinism contract: identical (npc.Id, hazard.Id, tick) produce identical outcome.
/// </summary>
public class SlipAndFallSystem : ISystem
{
    private readonly EntityManager _entityManager;
    private readonly SimulationClock _clock;
    private readonly SlipAndFallConfig _config;
    private readonly LifeStateTransitionSystem _lifeStateTransitionSystem;
    private readonly SeededRandom _rng;
    private readonly SoundTriggerBus? _soundBus;
    private readonly TuningCatalog _tuning;

    public SlipAndFallSystem(
        EntityManager entityManager,
        SimulationClock clock,
        SimConfig config,
        LifeStateTransitionSystem lifeStateTransitionSystem,
        SeededRandom rng,
        SoundTriggerBus? soundBus = null,
        TuningCatalog? tuning = null)
    {
        _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _config = config?.SlipAndFall ?? throw new ArgumentNullException(nameof(config));
        _lifeStateTransitionSystem = lifeStateTransitionSystem ?? throw new ArgumentNullException(nameof(lifeStateTransitionSystem));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _soundBus = soundBus;
        _tuning = tuning ?? TuningCatalog.Empty();
    }

    public void Update(EntityManager em, float deltaTime)
    {
        // Iterate all Alive NPCs in deterministic (ascending entity ID) order
        var npcs = em.Query<NpcTag>()
            .Where(e => LifeStateGuard.IsAlive(e))
            .OrderBy(e => e.Id)
            .ToList();

        foreach (var npc in npcs)
        {
            if (!npc.Has<PositionComponent>()) continue;

            // Per-archetype slip bias.
            var archetypeId = npc.Has<NpcArchetypeComponent>() ? npc.Get<NpcArchetypeComponent>().ArchetypeId : null;
            var slipBias = _tuning.GetSlipBias(archetypeId);

            var pos = npc.Get<PositionComponent>();
            int tileX = (int)MathF.Round(pos.X);
            int tileY = (int)MathF.Round(pos.Z);

            // Find all hazards (entities with FallRiskComponent) at this tile
            var hazardsHere = em.Query<FallRiskComponent>()
                .Where(hazard =>
                {
                    if (!hazard.Has<PositionComponent>()) return false;
                    var hp = hazard.Get<PositionComponent>();
                    int hx = (int)MathF.Round(hp.X);
                    int hy = (int)MathF.Round(hp.Z);
                    return hx == tileX && hy == tileY;
                })
                .OrderBy(h => h.Id)
                .ToList();

            // Check slip against each hazard on this tile
            foreach (var hazard in hazardsHere)
            {
                float risk = hazard.Get<FallRiskComponent>().RiskLevel;

                // Get NPC's speed modifier, scaled by the archetype's movement-speed factor.
                float speed = npc.Has<MovementComponent>()
                    ? npc.Get<MovementComponent>().SpeedModifier * slipBias.MovementSpeedFactor
                    : slipBias.MovementSpeedFactor;

                // Apply stress multiplier if acute stress is high
                float stressMult = 1.0f;
                if (npc.Has<StressComponent>())
                {
                    var stress = npc.Get<StressComponent>();
                    if (stress.AcuteLevel >= _config.StressDangerThreshold)
                        stressMult = _config.StressSlipMultiplier;
                }

                // Apply per-archetype slip chance multiplier.
                float slipChance = risk * speed * stressMult * _config.GlobalSlipChanceScale * slipBias.SlipChanceMult;

                // Deterministic seeded roll: hash (npc.Id, hazard.Id, current time)
                // Uses a stateless hash-based RNG to produce deterministic results
                int seed = HashTuple(npc.Id, hazard.Id, (long)_clock.TotalTime);
                float roll = StatelessRandom(seed);

                if (roll < slipChance)
                {
                    // Fatal slip — transition directly to Deceased
                    _lifeStateTransitionSystem.RequestTransition(
                        npc.Id,
                        LS.Deceased,
                        CauseOfDeath.SlippedAndFell);

                    // Emit slip and thud sounds
                    if (_soundBus != null)
                    {
                        var slipPos = npc.Get<PositionComponent>();
                        _soundBus.Emit(SoundTriggerKind.Slip, npc.Id, slipPos.X, slipPos.Z, 0.8f, (long)_clock.TotalTime);
                        _soundBus.Emit(SoundTriggerKind.Thud, npc.Id, slipPos.X, slipPos.Z, 0.9f, (long)_clock.TotalTime);
                    }

                    // Only one slip per NPC per tick
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Deterministic hash combining NPC ID, hazard ID, and game time.
    /// Used as the seed for the slip roll to ensure reproducibility.
    /// </summary>
    private static int HashTuple(Guid npcId, Guid hazardId, long gameTime)
    {
        int hash1 = npcId.GetHashCode();
        int hash2 = hazardId.GetHashCode();
        int hash3 = (int)(gameTime ^ (gameTime >> 32));
        return hash1 ^ (hash2 << 5) ^ (hash3 >> 7);
    }

    /// <summary>
    /// Stateless pseudo-random float in [0, 1) from a seed value.
    /// Uses a simple hash-based approach for deterministic results.
    /// </summary>
    private static float StatelessRandom(int seed)
    {
        int hash = ((seed * 73856093) ^ 19349663) ^ 83492791;
        return ((hash & 0x7FFFFFFF) % 65536) / 65536f;
    }
}
