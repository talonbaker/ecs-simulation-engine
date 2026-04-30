using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;

namespace APIFramework.Systems;

/// <summary>
/// Per-tick mutation of social drive Current values.
/// Three forces per drive per NPC:
///   1. Decay toward Baseline  — linear approach at DriveDecayPerTick
///   2. Circadian shape        — sinusoidal nudge; each drive peaks at its configured day-fraction
///   3. Volatility noise       — per-NPC random nudge scaled by Neuroticism + global scale
///
/// Fractional deltas accumulate in an internal dictionary until they cross ±1,
/// then apply as integer steps to Current — this ensures small rates still produce
/// movement over time without losing precision to int truncation.
///
/// Baseline is never modified. Final Current is clamped to 0–100.
/// Phase: Cognition.
/// </summary>
/// <remarks>
/// Reads: <see cref="NpcTag"/>, <see cref="SocialDrivesComponent"/>,
/// <see cref="PersonalityComponent"/>, optional <see cref="StressComponent"/>, and the
/// <see cref="SimulationClock"/> for the day fraction.
/// Writes: <see cref="SocialDrivesComponent"/> Current values (single writer per tick).
/// Ordering: runs in the Cognition phase before <see cref="ActionSelectionSystem"/> so
/// candidate scoring sees the freshly-updated drives.
/// </remarks>
/// <seealso cref="SocialDrivesComponent"/>
/// <seealso cref="SocialSystemConfig"/>
/// <seealso cref="ActionSelectionSystem"/>
public class DriveDynamicsSystem : ISystem
{
    private readonly SocialSystemConfig _cfg;
    private readonly SimulationClock    _clock;
    private readonly SeededRandom       _rng;
    private readonly StressConfig?      _stressCfg;

    // Per-entity fractional accumulators for each of the 8 drives.
    // Key = entity Guid; value = double[8] one slot per canonical drive index.
    private readonly Dictionary<Guid, double[]> _accum = new();

    private const int DriveCount = 8;

    /// <summary>
    /// Constructs the system.
    /// </summary>
    /// <param name="cfg">Social tuning — decay rate, circadian amplitude/phase, volatility scale.</param>
    /// <param name="clock">Simulation clock; supplies the current day fraction for the circadian term.</param>
    /// <param name="rng">Seeded RNG used for the per-tick volatility noise; ensures determinism.</param>
    /// <param name="stressCfg">Optional stress tuning. When supplied (and the entity has
    /// <see cref="StressComponent"/>), acute stress amplifies volatility.</param>
    public DriveDynamicsSystem(SocialSystemConfig cfg, SimulationClock clock, SeededRandom rng,
        StressConfig? stressCfg = null)
    {
        _cfg       = cfg;
        _clock     = clock;
        _rng       = rng;
        _stressCfg = stressCfg;
    }

    /// <summary>
    /// Per-tick update. For every alive NPC with social drives + personality, applies
    /// decay-toward-baseline, the circadian sinusoid, and (stress-amplified) volatility noise
    /// to all eight drives, then writes the updated <see cref="SocialDrivesComponent"/> back.
    /// </summary>
    public void Update(EntityManager em, float deltaTime)
    {
        float dayFraction = _clock.GameTimeOfDay / SimulationClock.SecondsPerDay;

        foreach (var entity in em.Query<NpcTag>().ToList())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip non-Alive NPCs
            if (!entity.Has<SocialDrivesComponent>()) continue;
            if (!entity.Has<PersonalityComponent>())  continue;

            if (!_accum.TryGetValue(entity.Id, out var acc))
            {
                acc = new double[DriveCount];
                _accum[entity.Id] = acc;
            }

            var drives      = entity.Get<SocialDrivesComponent>();
            var personality = entity.Get<PersonalityComponent>();

            // Neuroticism –2..+2 → volatility multiplier 0..2
            double neuroMult     = Math.Clamp(1.0 + 0.5 * personality.Neuroticism, 0.0, 2.0);
            double volatilityMax = _cfg.DriveVolatilityScale * neuroMult;

            // Stress amplification: high acute stress increases drive volatility
            if (_stressCfg != null && entity.Has<StressComponent>())
            {
                double stressMult = 1.0 + entity.Get<StressComponent>().AcuteLevel
                                         / 100.0 * _stressCfg.StressVolatilityScale;
                volatilityMax *= stressMult;
            }

            drives.Belonging.Current  = Apply(0, acc, drives.Belonging,  "belonging",  dayFraction, volatilityMax);
            drives.Status.Current     = Apply(1, acc, drives.Status,     "status",     dayFraction, volatilityMax);
            drives.Affection.Current  = Apply(2, acc, drives.Affection,  "affection",  dayFraction, volatilityMax);
            drives.Irritation.Current = Apply(3, acc, drives.Irritation, "irritation", dayFraction, volatilityMax);
            drives.Attraction.Current = Apply(4, acc, drives.Attraction, "attraction", dayFraction, volatilityMax);
            drives.Trust.Current      = Apply(5, acc, drives.Trust,      "trust",      dayFraction, volatilityMax);
            drives.Suspicion.Current  = Apply(6, acc, drives.Suspicion,  "suspicion",  dayFraction, volatilityMax);
            drives.Loneliness.Current = Apply(7, acc, drives.Loneliness, "loneliness", dayFraction, volatilityMax);

            entity.Add(drives);
        }
    }

    private int Apply(int idx, double[] acc, DriveValue drive,
        string name, float dayFraction, double volatilityMax)
    {
        // 1. Decay toward baseline (capped so it never overshoots in one tick)
        double diff      = drive.Baseline - drive.Current;
        double decayStep = diff >= 0
            ? Math.Min(_cfg.DriveDecayPerTick,  diff)
            : Math.Max(-_cfg.DriveDecayPerTick, diff);

        // 2. Circadian: cosine peaks at the configured day-fraction phase
        double amplitude = _cfg.GetCircadianAmplitude(name);
        double phase     = _cfg.GetCircadianPhase(name);
        double circadian = amplitude * Math.Cos((dayFraction - phase) * 2.0 * Math.PI);

        // 3. Volatility: uniform random in [−max, +max] from the seeded RNG
        double noise = _rng.NextFloatRange(-(float)volatilityMax, (float)volatilityMax);

        // Accumulate fractional progress; extract whole-integer steps only
        acc[idx] += decayStep + circadian + noise;
        int intDelta = (int)acc[idx];   // truncates toward zero
        acc[idx] -= intDelta;

        return SocialDrivesComponent.Clamp0100(drive.Current + intDelta);
    }
}
