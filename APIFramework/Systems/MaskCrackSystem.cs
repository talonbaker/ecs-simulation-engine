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
/// Phase: Cleanup (80) — runs after ActionSelectionSystem (Cognition=30) and
/// SocialMaskSystem (Cognition=30). When crackPressure exceeds the threshold,
/// overrides IntendedActionComponent with Dialog(MaskSlip) and emits a
/// MaskSlip narrative candidate. The override is unintentional — the NPC did not
/// choose to speak; the mask failed.
/// </summary>
public sealed class MaskCrackSystem : ISystem
{
    private readonly EntityRoomMembership _roomMembership;
    private readonly NarrativeEventBus    _narrativeBus;
    private readonly SocialMaskConfig     _cfg;

    private long _tick;

    public MaskCrackSystem(
        EntityRoomMembership roomMembership,
        NarrativeEventBus    narrativeBus,
        SocialMaskConfig     cfg)
    {
        _roomMembership = roomMembership;
        _narrativeBus   = narrativeBus;
        _cfg            = cfg;
    }

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

    public static int EntityIntId(Entity entity)
    {
        var b = entity.Id.ToByteArray();
        return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    }
}
