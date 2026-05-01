using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Mutation;
using APIFramework.Systems.Audio;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Physics;
using APIFramework.Systems.Spatial;
using Xunit;

using LS = global::APIFramework.Components.LifeState;

namespace APIFramework.Tests.Systems.Physics;

/// <summary>
/// AT-08: Broken liquid stain spawned by physics breakage → NPC walks over it →
/// SlipAndFallSystem rolls correctly.
/// </summary>
public class PhysicsSlipFallIntegrationTests
{
    [Fact]
    public void AT08_LiquidStain_SpawnedByBreakage_EnablesSlipRoll()
    {
        var em       = new EntityManager();
        var strBus   = new StructuralChangeBus();
        var api      = new WorldMutationApi(em, strBus);
        var sound    = new SoundTriggerBus();
        var clock    = new SimulationClock();
        var narrBus  = new NarrativeEventBus();

        var physicsCfg = new PhysicsConfig { GravityPerTick = 0f, MinVelocity = 0.001f, WallHitClampMargin = 0.01f };
        var col        = new CollisionDetector(physicsCfg, 10, 10);
        var physicsSys = new PhysicsTickSystem(physicsCfg, col, api, sound, clock);

        var simCfg = new SimConfig
        {
            LifeState   = new LifeStateConfig { DefaultIncapacitatedTicks = 180 },
            SlipAndFall = new SlipAndFallConfig
            {
                GlobalSlipChanceScale = 10f,   // guaranteed slip
                StressDangerThreshold = 60,
                StressSlipMultiplier  = 1f,
            }
        };
        var transitions = new LifeStateTransitionSystem(narrBus, em, clock, simCfg);
        var slipSys     = new SlipAndFallSystem(em, clock, simCfg, transitions, new SeededRandom(0));

        // Mug thrown at wall — spawns water puddle stain at ~(9, 5)
        var mug = em.CreateEntity();
        mug.Add(new PositionComponent { X = 8f, Y = 0f, Z = 5f });
        mug.Add(new MassComponent { MassKilograms = 0.4f });
        mug.Add(new BreakableComponent { HitEnergyThreshold = 8f, OnBreak = BreakageBehavior.SpawnLiquidStain });
        mug.Add(new ThrownVelocityComponent { VelocityX = 10f, DecayPerTick = 0f });

        physicsSys.Update(em, 1f);

        // Verify stain spawned
        var stain = em.GetAllEntities().FirstOrDefault(e => e.Has<StainTag>() && e.Has<FallRiskComponent>());
        Assert.NotEqual(default, stain);

        // Place NPC on the stain tile
        var stainPos = stain!.Get<PositionComponent>();
        var npc = em.CreateEntity();
        npc.Add(new NpcTag());
        npc.Add(new LifeStateComponent { State = LS.Alive });
        npc.Add(new PositionComponent { X = stainPos.X, Y = 0f, Z = stainPos.Z });
        npc.Add(new MovementComponent { SpeedModifier = 2.0f });
        npc.Add(new StressComponent { AcuteLevel = 0 });

        // Slip chance = fallRisk * speed * globalScale = 0.4 * 2.0 * 10 = 8 → guaranteed
        slipSys.Update(em, 1f);
        transitions.Update(em, 1f);

        Assert.Equal(LS.Deceased, npc.Get<LifeStateComponent>().State);
    }
}
