using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using APIFramework.Systems.Narrative;
using APIFramework.Systems.Spatial;

namespace APIFramework.Systems;

/// <summary>
/// Cleanup phase. When mask + low-willpower + stress + burnout pressure exceeds the
/// configured threshold, the social mask cracks: the system overrides
/// <see cref="IntendedActionComponent"/> with a Dialog(MaskSlip) intent, emits a
/// <see cref="NarrativeEventKind.MaskSlip"/> candidate, and resets the dominant masked
/// drive to zero. The override is unintentional — the NPC did not choose to speak.
/// </summary>
/// <remarks>
/// Reads: <see cref="SocialMaskComponent"/>, <see cref="WillpowerComponent"/>,
/// <see cref="StressComponent"/>, <see cref="BurningOutTag"/>,
/// <see cref="LifeStateComponent"/>, <see cref="RoomComponent"/>.<br/>
/// Writes: <see cref="SocialMaskComponent"/> (slip cooldown, masked drives, current load),
/// <see cref="IntendedActionComponent"/> (override), candidates onto
/// <see cref="NarrativeEventBus"/>.<br/>
/// Phase: Cleanup, after <see cref="ActionSelectionSystem"/> and
/// <see cref="SocialMaskSystem"/> (both Cognition) so the override wins for the next
/// Dialog phase.
/// </remarks>
public sealed class MaskCrackSystem : ISystem
{
    private readonly EntityRoomMembership _roomMembership;
    private readonly NarrativeEventBus    _narrativeBus;
    private readonly SocialMaskConfig     _cfg;

    private long _tick;

    /// <summary>Constructs the mask-crack detector.</summary>
    /// <param name="roomMembership">Room membership service used to enumerate co-located observers.</param>
    /// <param name="narrativeBus">Bus that receives the resulting MaskSlip narrative candidate.</param>
    /// <param name="cfg">Mask configuration (crack threshold, cooldown, contribution scales).</param>
    public MaskCrackSystem(
        EntityRoomMembership roomMembership,
        NarrativeEventBus    narrativeBus,
        SocialMaskConfig     cfg)
    {
        _roomMembership = roomMembership;
        _narrativeBus   = narrativeBus;
        _cfg            = cfg;
    }

    /// <summary>Per-tick crack detection pass.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds, unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        _tick++;

        foreach (var entity in em.Query<NpcTag>().ToList())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip non-Alive NPCs
            if (!entity.Has<SocialMaskComponent>()) continue;
            if (!entity.Has<WillpowerComponent>())  continue;
            if (!entity.Has<StressComponent>())     continue;

            var mask      = entity.Get<SocialMaskComponent>();
            var willpower = entity.Get<WillpowerComponent>();
            var stress    = entity.Get<StressComponent>();

            // Sticky cooldown: LastSlipTick == 0 means never slipped → no cooldown.
            if (mask.LastSlipTick > 0 && mask.LastSlipTick + _cfg.SlipCooldownTicks > _tick)
                continue;

            // Crack pressure
            double pressureMask      = mask.CurrentLoad / 100.0;
            double pressureWillpower = Math.Max(0,
                (_cfg.LowWillpowerThreshold - willpower.Current))
                / (double)_cfg.LowWillpowerThreshold;
            double pressureStress    = (stress.AcuteLevel / 100.0) * _cfg.StressCrackContribution;
            double pressureBurnout   = entity.Has<BurningOutTag>() ? _cfg.BurnoutCrackBonus : 0.0;

            double crackPressure = pressureMask + pressureWillpower + pressureStress + pressureBurnout;

            if (crackPressure < _cfg.CrackThreshold) continue;

            // Identify dominant masked drive (highest value; ties broken in declaration order)
            int maxVal = Math.Max(
                Math.Max(mask.IrritationMask, mask.AffectionMask),
                Math.Max(mask.AttractionMask, mask.LonelinessMask));

            string dominantName;
            if      (mask.IrritationMask == maxVal) { dominantName = "irritation"; mask.IrritationMask = 0; }
            else if (mask.AffectionMask  == maxVal) { dominantName = "affection";  mask.AffectionMask  = 0; }
            else if (mask.AttractionMask == maxVal) { dominantName = "attraction"; mask.AttractionMask = 0; }
            else                                    { dominantName = "loneliness"; mask.LonelinessMask = 0; }

            // Observers in the same room (up to 3, sorted by int ID for determinism)
            int npcIntId  = EntityIntId(entity);
            var myRoom    = _roomMembership.GetRoom(entity);
            var observers = FindObservers(em, entity, myRoom, 3);

            // Narrative candidate
            var participants = new List<int>(1 + observers.Count) { npcIntId };
            participants.AddRange(observers);

            string? roomId = null;
            if (myRoom != null && myRoom.Has<RoomComponent>())
                roomId = myRoom.Get<RoomComponent>().Id;

            var detail = $"{npcIntId} mask slipped: {dominantName}";
            if (detail.Length > 280) detail = detail.Substring(0, 280);

            _narrativeBus.RaiseCandidate(new NarrativeEventCandidate(
                _tick, NarrativeEventKind.MaskSlip, participants, roomId, detail));

            // Override intent — unintentional speech wins over ActionSelection's choice
            int targetId = observers.Count > 0 ? observers[0] : 0;
            entity.Add(new IntendedActionComponent(
                IntendedActionKind.Dialog,
                targetId,
                DialogContextValue.MaskSlip,
                (int)(Math.Min(crackPressure, 3.0) / 3.0 * 100)));

            // Update mask state
            mask.LastSlipTick = _tick;
            mask.CurrentLoad  = Math.Clamp(
                (mask.IrritationMask + mask.AffectionMask + mask.AttractionMask + mask.LonelinessMask) / 4,
                0, 100);
            entity.Add(mask);
        }
    }

    private List<int> FindObservers(EntityManager em, Entity self, Entity? room, int max)
    {
        var result = new List<int>();
        if (room == null) return result;

        foreach (var other in em.Query<NpcTag>())
        {
            if (other.Id == self.Id) continue;
            if (_roomMembership.GetRoom(other) != room) continue;
            result.Add(EntityIntId(other));
            if (result.Count >= max) break;
        }

        result.Sort();
        return result;
    }

    /// <summary>
    /// Extracts the low-32-bit deterministic counter from an entity's Guid.
    /// Used to populate participant lists in narrative candidates.
    /// </summary>
    /// <param name="entity">Entity whose Guid to convert.</param>
    /// <returns>Low 32 bits of the Guid byte array, in little-endian order.</returns>
    public static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }
}
