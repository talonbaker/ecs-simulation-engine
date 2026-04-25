using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Lighting;
using Xunit;

namespace APIFramework.Tests.Systems.Lighting;

/// <summary>
/// AT-13: Two runs with the same seed over 5000 ticks produce byte-identical
/// per-room illumination trajectories. Different seeds produce different trajectories.
/// </summary>
public class LightingDeterminismTests
{
    [Fact]
    public void LightingPipeline_TwoRunsSameSeed_ProduceIdenticalIlluminationTrajectories()
    {
        const int seed  = 54321;
        const int ticks = 5000;

        var traj1 = RunTrajectory(seed, ticks);
        var traj2 = RunTrajectory(seed, ticks);

        Assert.Equal(traj1.Count, traj2.Count);
        for (int i = 0; i < traj1.Count; i++)
        {
            Assert.True(traj1[i] == traj2[i],
                $"Tick {i}: illumination diverged: '{traj1[i]}' vs '{traj2[i]}'");
        }
    }

    [Fact]
    public void LightingPipeline_DifferentSeeds_ProduceDifferentTrajectories()
    {
        const int ticks = 5000;
        var traj1 = RunTrajectory(seed: 1, ticks);
        var traj2 = RunTrajectory(seed: 2, ticks);

        bool anyDiff = false;
        int  check   = Math.Min(traj1.Count, traj2.Count);
        for (int i = 0; i < check; i++)
        {
            if (traj1[i] != traj2[i]) { anyDiff = true; break; }
        }

        Assert.True(anyDiff || traj1.Count != traj2.Count,
            "Different seeds should produce different illumination trajectories");
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the lighting pipeline for <paramref name="ticks"/> ticks and records
    /// a string snapshot of all room illumination values each tick.
    /// </summary>
    private static List<string> RunTrajectory(int seed, int ticks)
    {
        var em      = new EntityManager();
        var rng     = new SeededRandom(seed);
        var cfg     = new LightingConfig();
        var clock   = new SimulationClock();
        var sunSvc  = new SunStateService();
        var sunSys  = new SunSystem(clock, sunSvc, cfg);
        var states  = new LightSourceStateSystem(rng, cfg);
        var beams   = new ApertureBeamSystem(sunSvc, clock);
        var illum   = new IlluminationAccumulationSystem(states, beams, cfg);

        // One room
        EntityTemplates.Room(em, "r1", "Office", RoomCategory.Office, BuildingFloor.First,
            new BoundsRect(0, 0, 10, 10));

        // One flickering source at room center
        EntityTemplates.LightSource(em, "src-flicker", LightKind.OverheadFluorescent,
            LightState.Flickering, intensity: 60, colorTemperatureK: 4000,
            tileX: 5, tileY: 5, roomId: "r1");

        // One dying source
        EntityTemplates.LightSource(em, "src-dying", LightKind.DeskLamp,
            LightState.Dying, intensity: 80, colorTemperatureK: 3000,
            tileX: 5, tileY: 5, roomId: "r1");

        // South-facing aperture (beam varies with simulated daylight)
        EntityTemplates.LightAperture(em, "apt-south", tileX: 5, tileY: 0,
            roomId: "r1", facing: ApertureFacing.South, areaSqTiles: 4.0);

        var trajectory = new List<string>(ticks);

        for (int t = 0; t < ticks; t++)
        {
            // Advance clock by 1 real second (TimeScale=120 → 120 game-seconds per tick)
            clock.Tick(1f);

            sunSys.Update(em, 1f);
            states.Update(em, 1f);
            beams.Update(em, 1f);
            illum.Update(em, 1f);

            // Snapshot all room illumination as a string
            var snapshot = string.Join("|", em.Query<RoomTag>()
                .OrderBy(e => e.Get<RoomComponent>().Id)
                .Select(e =>
                {
                    var i = e.Get<RoomComponent>().Illumination;
                    return $"{i.AmbientLevel},{i.ColorTemperatureK},{i.DominantSourceId ?? "null"}";
                }));

            trajectory.Add(snapshot);
        }

        return trajectory;
    }
}
