namespace APIFramework.Components;

/// <summary>
/// Tracks an entity's physical energy reserve and accumulated sleepiness.
///
/// ENERGY (0–100)
///   How much "go" the body has.  Drains slowly while awake; restores during sleep.
///   Starts at ~85 — the entity just woke up feeling well rested but not perfect.
///
/// SLEEPINESS (0–100)
///   Accumulated sleep pressure (adenosine build-up in biological terms).
///   Rises while awake; drains while sleeping.
///   Starts at ~15 — low after a full night's rest.
///
/// ISSLEEPING
///   Set/cleared by SleepSystem based on BrainSystem's dominant desire.
///   EnergySystem reads this flag each frame to decide which direction to evolve.
///
/// Rates are all in GAME-SECONDS.  At the default TimeScale of 120, the drain
/// values produce a natural ~16-hour awake / ~8-hour sleep rhythm.
/// </summary>
public struct EnergyComponent
{
    // -- State -----------------------------------------------------------------

    /// <summary>Physical energy reserve (0–100).  100 = fully rested.</summary>
    public float Energy;

    /// <summary>Accumulated sleep pressure (0–100).  0 = wide awake, 100 = can't keep eyes open.</summary>
    public float Sleepiness;

    /// <summary>True while the entity is in the sleep state managed by SleepSystem.</summary>
    public bool IsSleeping;

    // -- Rates (game-seconds) --------------------------------------------------

    /// <summary>Energy lost per game-second while awake.</summary>
    public float EnergyDrainRate;

    /// <summary>Sleepiness gained per game-second while awake.</summary>
    public float SleepinessGainRate;

    /// <summary>Energy restored per game-second while sleeping.</summary>
    public float EnergyRestoreRate;

    /// <summary>Sleepiness lost per game-second while sleeping.</summary>
    public float SleepinessDrainRate;

    // -- Derived ---------------------------------------------------------------

    /// <summary>Inverse of Energy — how fatigued the entity is (0 = alert, 100 = exhausted).</summary>
    public readonly float Tiredness => 100f - Energy;
}
