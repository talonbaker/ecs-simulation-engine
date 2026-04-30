using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;

namespace APIFramework.Systems.Lighting;

/// <summary>
/// Phase: Lighting (7) — sums per-room illumination from all light sources and apertures.
///
/// For each room entity (RoomTag + RoomComponent):
///   1. Sum contributions from all LightSourceTag entities where RoomId matches.
///      Contribution = effectiveIntensity × linear-falloff(distToRoomCenter, sourceRangeBase).
///   2. Sum contributions from all LightApertureTag entities where RoomId matches.
///      Contribution = beam.Intensity (no additional distance factor; aperture is already room-scoped).
///   3. AmbientLevel = clamp(sum, 0, 100).
///   4. ColorTemperatureK = intensity-weighted average across all contributors.
///   5. DominantSourceId = contributor with highest single contribution.
///
/// Writes the result back to RoomComponent.Illumination in place.
/// </summary>
/// <remarks>
/// Reads <c>RoomComponent</c>, <c>LightSourceComponent</c>, <c>LightApertureComponent</c>,
/// <see cref="LightSourceStateSystem"/> effective intensities, and
/// <see cref="ApertureBeamSystem"/> beam states. Writes <c>RoomComponent.Illumination</c>.
/// Must run AFTER <see cref="LightSourceStateSystem"/> and <see cref="ApertureBeamSystem"/>;
/// must run BEFORE <see cref="APIFramework.Systems.Coupling.LightingToDriveCouplingSystem"/>
/// so coupling sees fresh illumination.
/// </remarks>
public sealed class IlluminationAccumulationSystem : ISystem
{
    private readonly LightSourceStateSystem _sourceStates;
    private readonly ApertureBeamSystem     _beamSystem;
    private readonly LightingConfig         _cfg;

    /// <summary>
    /// Stores references to the upstream lighting systems and tuning config.
    /// </summary>
    /// <param name="sourceStates">Provides per-source effective intensity for the current tick.</param>
    /// <param name="beamSystem">Provides per-aperture beam state for the current tick.</param>
    /// <param name="cfg">Lighting tuning — supplies <c>SourceRangeBase</c> for falloff.</param>
    public IlluminationAccumulationSystem(
        LightSourceStateSystem sourceStates,
        ApertureBeamSystem     beamSystem,
        LightingConfig         cfg)
    {
        _sourceStates = sourceStates;
        _beamSystem   = beamSystem;
        _cfg          = cfg;
    }

    /// <summary>
    /// Per-tick entry point. Sums source and aperture contributions for every room
    /// and writes the result back to <c>RoomComponent.Illumination</c>.
    /// </summary>
    /// <param name="em">Entity manager — queried for rooms, light sources, and apertures.</param>
    /// <param name="deltaTime">Tick delta in seconds (unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        // Collect sources and apertures once per tick
        var sources   = new List<(Entity e, LightSourceComponent c)>();
        var apertures = new List<(Entity e, LightApertureComponent c)>();

        foreach (var e in em.Query<LightSourceTag>())
            sources.Add((e, e.Get<LightSourceComponent>()));

        foreach (var e in em.Query<LightApertureTag>())
            apertures.Add((e, e.Get<LightApertureComponent>()));

        // Accumulate illumination for each room
        foreach (var roomEntity in em.Query<RoomTag>())
        {
            var room      = roomEntity.Get<RoomComponent>();
            string roomId = room.Id;

            // Room center in tile coordinates
            double centerX = room.Bounds.X + room.Bounds.Width  * 0.5;
            double centerY = room.Bounds.Y + room.Bounds.Height * 0.5;

            double totalIntensity = 0.0;
            double weightedColorK = 0.0;
            double weightSum      = 0.0;

            string? dominantId    = null;
            double  dominantMax   = 0.0;

            // ── Source contributions ──────────────────────────────────────────
            foreach (var (srcEntity, src) in sources)
            {
                if (src.RoomId != roomId) continue;

                double effective = _sourceStates.GetEffectiveIntensity(srcEntity);
                if (effective <= 0.0) continue;

                double dist    = Math.Sqrt(
                    Math.Pow(src.TileX - centerX, 2) +
                    Math.Pow(src.TileY - centerY, 2));
                double falloff = Math.Max(0.0, 1.0 - dist / _cfg.SourceRangeBase);
                double contrib = effective * falloff;
                if (contrib <= 0.0) continue;

                totalIntensity += contrib;
                weightedColorK += contrib * src.ColorTemperatureK;
                weightSum      += contrib;

                if (contrib > dominantMax)
                {
                    dominantMax = contrib;
                    dominantId  = src.Id;
                }
            }

            // ── Aperture contributions ────────────────────────────────────────
            foreach (var (aptEntity, apt) in apertures)
            {
                if (apt.RoomId != roomId) continue;

                var beam = _beamSystem.GetBeamState(aptEntity);
                if (beam is null || beam.Value.Intensity <= 0.0) continue;

                double contrib = beam.Value.Intensity;

                totalIntensity += contrib;
                weightedColorK += contrib * beam.Value.ColorTemperatureK;
                weightSum      += contrib;

                if (contrib > dominantMax)
                {
                    dominantMax = contrib;
                    dominantId  = apt.Id;
                }
            }

            // ── Write back ────────────────────────────────────────────────────
            int ambientLevel  = (int)Math.Clamp(Math.Round(totalIntensity), 0, 100);
            int colorTempK    = weightSum > 0.0
                ? (int)Math.Round(weightedColorK / weightSum)
                : 0;

            room.Illumination = new RoomIllumination(ambientLevel, colorTempK, dominantId);
            roomEntity.Add(room);
        }
    }
}
