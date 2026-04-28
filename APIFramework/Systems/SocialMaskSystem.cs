using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems;

/// <summary>
/// Phase: Cognition (30) — after DriveDynamicsSystem, before MaskCrackSystem.
///
/// Per-tick mask growth and decay for the four suppressible drives: irritation,
/// affection, attraction, loneliness. Mask grows when a drive is elevated AND the
/// NPC is in a high-exposure context (bright room + observers). Mask decays when
/// alone or in low-exposure context.
///
/// Fractional accumulators prevent precision loss at sub-integer gain/decay rates.
/// </summary>
public sealed class SocialMaskSystem : ISystem
{
    private readonly EntityRoomMembership _roomMembership;
    private readonly SocialMaskConfig     _cfg;

    // Per-entity fractional growth accumulators: index = drive (0=irritation … 3=loneliness)
    private readonly Dictionary<Guid, double[]> _growthAccum = new();

    // Per-entity fractional decay accumulator (same decay rate for all 4 drives)
    private readonly Dictionary<Guid, double> _decayAccum = new();

    public SocialMaskSystem(EntityRoomMembership roomMembership, SocialMaskConfig cfg)
    {
        _roomMembership = roomMembership;
        _cfg            = cfg;
    }

    public void Update(EntityManager em, float deltaTime)
    {
        // Build room → occupant count so we can compute exposure for each NPC.
        var roomCount = new Dictionary<Entity, int>();
        foreach (var e in em.Query<NpcTag>())
        {
            var r = _roomMembership.GetRoom(e);
            if (r == null) continue;
            if (!roomCount.TryGetValue(r, out int cur)) cur = 0;
            roomCount[r] = cur + 1;
        }

        foreach (var entity in em.Query<NpcTag>().ToList())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip non-Alive NPCs
            if (!entity.Has<SocialMaskComponent>())   continue;
            if (!entity.Has<SocialDrivesComponent>()) continue;
            if (!entity.Has<PersonalityComponent>())  continue;

            var mask   = entity.Get<SocialMaskComponent>();
            var drives = entity.Get<SocialDrivesComponent>();
            var pers   = entity.Get<PersonalityComponent>();

            // Room context
            var myRoom       = _roomMembership.GetRoom(entity);
            int illumination = 0;
            if (myRoom != null && myRoom.Has<RoomComponent>())
                illumination = myRoom.Get<RoomComponent>().Illumination.AmbientLevel;

            int totalInRoom = myRoom != null && roomCount.TryGetValue(myRoom, out var cnt) ? cnt : 0;
            int nearbyCount = Math.Max(0, totalInRoom - 1); // exclude self

            double exposureFactor = (illumination / 100.0) * 0.5
                                  + Math.Min(nearbyCount, 4) / 4.0 * 0.5;

            double personalityBias = (1.0 + pers.Conscientiousness * _cfg.PersonalityMaskScale)
                                   * (1.0 - pers.Extraversion      * _cfg.PersonalityExtraversionScale);
            personalityBias = Math.Max(personalityBias, 0.01);

            // Accumulators
            var id = entity.Id;
            if (!_growthAccum.TryGetValue(id, out var growth))
            {
                growth = new double[4];
                _growthAccum[id] = growth;
            }
            if (!_decayAccum.TryGetValue(id, out var decayAcc))
                _decayAccum[id] = 0.0;

            // Current raw mask values
            int[] masks = { mask.IrritationMask, mask.AffectionMask, mask.AttractionMask, mask.LonelinessMask };
            int[] driveCurrents = {
                drives.Irritation.Current,
                drives.Affection.Current,
                drives.Attraction.Current,
                drives.Loneliness.Current,
            };

            bool lowExposure = exposureFactor < _cfg.LowExposureThreshold;

            // Growth pass
            for (int i = 0; i < 4; i++)
            {
                double driveLoad = Math.Max(0, driveCurrents[i] - 50) / 50.0;
                growth[i] += driveLoad * exposureFactor * personalityBias * _cfg.MaskGainPerTick;
                int gainInt = (int)growth[i];
                growth[i] -= gainInt;
                masks[i] = Math.Clamp(masks[i] + gainInt, 0, 100);
            }

            // Decay pass — shared accumulator across all 4 drives
            if (lowExposure)
            {
                decayAcc += _cfg.MaskDecayPerTick;
                int decayInt = (int)decayAcc;
                _decayAccum[id] = decayAcc - decayInt;
                for (int i = 0; i < 4; i++)
                    masks[i] = Math.Max(0, masks[i] - decayInt);
            }
            else
            {
                _decayAccum[id] = 0.0;
            }

            mask.IrritationMask = masks[0];
            mask.AffectionMask  = masks[1];
            mask.AttractionMask = masks[2];
            mask.LonelinessMask = masks[3];
            mask.CurrentLoad    = Math.Clamp((masks[0] + masks[1] + masks[2] + masks[3]) / 4, 0, 100);
            entity.Add(mask);
        }
    }
}
