using System;
using System.Collections.Generic;
using APIFramework.Components;

namespace APIFramework.Systems.Coupling;

/// <summary>
/// Per-NPC, per-drive floating-point accumulator that buffers fractional deltas across ticks
/// and flushes integer increments into SocialDrivesComponent when the accumulator crosses ±1.
///
/// Drive Current is an int (0–100). Per-tick deltas from the coupling table are float.
/// Without this accumulator, a 0.08/tick delta would be truncated to 0 every tick and
/// never produce a drive change. The accumulator preserves the fractional progress.
///
/// Accumulators persist across save/load cycles (they live in engine memory, not WorldStateDto).
/// Losing a few sub-1 fractional points on load is acceptable.
/// </summary>
public sealed class SocialDriveAccumulator
{
    // Canonical drive index: 0=belonging, 1=status, 2=affection, 3=irritation,
    //                        4=attraction, 5=trust, 6=suspicion, 7=loneliness
    private static readonly Dictionary<string, int> DriveIndex =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["belonging"]  = 0,
            ["status"]     = 1,
            ["affection"]  = 2,
            ["irritation"] = 3,
            ["attraction"] = 4,
            ["trust"]      = 5,
            ["suspicion"]  = 6,
            ["loneliness"] = 7,
        };

    private const int DriveCount = 8;

    // Key: (entityId, driveIdx). Value: accumulated fractional delta not yet flushed.
    private readonly Dictionary<(Guid entityId, int driveIdx), float> _acc = new();

    /// <summary>
    /// Adds <paramref name="delta"/> to the named drive's accumulator for <paramref name="entityId"/>.
    /// Unknown drive names are silently ignored.
    /// </summary>
    public void AddDelta(Guid entityId, string driveName, float delta)
    {
        if (!DriveIndex.TryGetValue(driveName, out int idx)) return;
        var key = (entityId, idx);
        _acc.TryGetValue(key, out float current);
        _acc[key] = current + delta;
    }

    /// <summary>
    /// Extracts whole-integer parts from all drive accumulators for <paramref name="entityId"/>
    /// and applies them (clamped to 0–100) to <paramref name="drives"/>.
    /// The fractional remainders stay in the accumulator for the next flush.
    /// </summary>
    public void FlushTo(Guid entityId, ref SocialDrivesComponent drives)
    {
        for (int idx = 0; idx < DriveCount; idx++)
        {
            var key = (entityId, idx);
            if (!_acc.TryGetValue(key, out float accum)) continue;

            int intPart = (int)accum;   // truncates toward zero
            if (intPart == 0) continue;

            _acc[key] = accum - intPart;
            ApplyDelta(idx, intPart, ref drives);
        }
    }

    private static void ApplyDelta(int driveIdx, int delta, ref SocialDrivesComponent d)
    {
        switch (driveIdx)
        {
            case 0: d.Belonging.Current  = SocialDrivesComponent.Clamp0100(d.Belonging.Current  + delta); break;
            case 1: d.Status.Current     = SocialDrivesComponent.Clamp0100(d.Status.Current     + delta); break;
            case 2: d.Affection.Current  = SocialDrivesComponent.Clamp0100(d.Affection.Current  + delta); break;
            case 3: d.Irritation.Current = SocialDrivesComponent.Clamp0100(d.Irritation.Current + delta); break;
            case 4: d.Attraction.Current = SocialDrivesComponent.Clamp0100(d.Attraction.Current + delta); break;
            case 5: d.Trust.Current      = SocialDrivesComponent.Clamp0100(d.Trust.Current      + delta); break;
            case 6: d.Suspicion.Current  = SocialDrivesComponent.Clamp0100(d.Suspicion.Current  + delta); break;
            case 7: d.Loneliness.Current = SocialDrivesComponent.Clamp0100(d.Loneliness.Current + delta); break;
        }
    }
}
