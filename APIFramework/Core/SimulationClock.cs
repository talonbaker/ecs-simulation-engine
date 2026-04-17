namespace APIFramework.Core;

/// <summary>
/// Central time authority for the simulation.
///
/// GAME TIME vs REAL TIME
/// ─────────────────────
/// TimeScale (default 120) means 1 real second = 120 game seconds = 2 game minutes.
/// Systems receive a pre-scaled deltaTime so they always think in game-seconds.
///
/// DAY/NIGHT
/// ─────────
/// The game world starts at 6:00 AM on Day 1.  GameTimeOfDay wraps every 24 h.
/// CircadianFactor amplifies or suppresses the sleep drive by time of day.
/// </summary>
public class SimulationClock
{
    // ── Timing ────────────────────────────────────────────────────────────────

    /// <summary>Current TimeScale. 120 = 2 game-minutes per real second (default).</summary>
    public float TimeScale = 120f;

    /// <summary>Total accumulated game-seconds since the simulation started.</summary>
    public double TotalTime { get; private set; }

    // ── Day / Night constants ─────────────────────────────────────────────────

    public const float SecondsPerDay  = 86_400f;   // 24 × 3600
    public const float DawnHour       = 6f;         // 6:00 AM — world starts here
    public const float SunriseHour    = 7f;
    public const float SunsetHour     = 19f;        // 7:00 PM
    public const float DuskHour       = 20f;        // 8:00 PM

    // World clock starts at 6:00 AM = 21 600 game-seconds into the day
    private const float StartOffsetSeconds = DawnHour * 3600f;

    // ── Game-time accessors ───────────────────────────────────────────────────

    /// <summary>Seconds since midnight in the current game day (0–86 399).</summary>
    public float GameTimeOfDay => (float)((TotalTime + StartOffsetSeconds) % SecondsPerDay);

    /// <summary>Game hour as a float (e.g. 13.5 = 1:30 PM).</summary>
    public float GameHour => GameTimeOfDay / 3600f;

    /// <summary>Minute component of the current game time (0–59).</summary>
    public int GameMinute => (int)(GameTimeOfDay / 60f) % 60;

    /// <summary>Second component of the current game time (0–59).</summary>
    public int GameSecond => (int)GameTimeOfDay % 60;

    /// <summary>How many full game days have passed (1-based).</summary>
    public int DayNumber => (int)((TotalTime + StartOffsetSeconds) / SecondsPerDay) + 1;

    /// <summary>True while the sun is up (Dawn → Dusk).</summary>
    public bool IsDaytime => GameHour >= DawnHour && GameHour < DuskHour;

    /// <summary>
    /// Formatted game time string, e.g. "6:05 AM".
    /// Suitable for both the Avalonia header and the CLI snapshot.
    /// </summary>
    public string GameTimeDisplay
    {
        get
        {
            int h    = (int)GameHour;
            int m    = GameMinute;
            string period = h >= 12 ? "PM" : "AM";
            int h12  = h == 0 ? 12 : (h > 12 ? h - 12 : h);
            return $"{h12}:{m:D2} {period}";
        }
    }

    /// <summary>
    /// Compact day label, e.g. "Day 1 · 6:05 AM".
    /// </summary>
    public string DayTimeDisplay => $"Day {DayNumber}  ·  {GameTimeDisplay}";

    // ── Circadian factor ──────────────────────────────────────────────────────

    /// <summary>
    /// Multiplier applied to SleepUrgency in BrainSystem based on time of day.
    ///
    ///   Morning   (6–8h)   → 0.40  (body clock suppresses sleepiness after waking)
    ///   Forenoon  (8–12h)  → 0.60
    ///   Noon dip  (12–14h) → 0.75  (post-lunch lull)
    ///   Afternoon (14–18h) → 0.60
    ///   Evening   (18–20h) → 0.90  (wind-down begins)
    ///   Night     (20–22h) → 1.20  (sleep window opens)
    ///   Late/early (22–6h) → 1.40  (peak sleep pressure)
    /// </summary>
    public float CircadianFactor
    {
        get
        {
            float h = GameHour;
            if (h >= 6f  && h < 8f)  return 0.40f;
            if (h >= 8f  && h < 12f) return 0.60f;
            if (h >= 12f && h < 14f) return 0.75f;
            if (h >= 14f && h < 18f) return 0.60f;
            if (h >= 18f && h < 20f) return 0.90f;
            if (h >= 20f && h < 22f) return 1.20f;
            return 1.40f; // 22:00 → 6:00
        }
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Advance game time by one real-time delta. Called by SimulationEngine each tick.
    /// Returns the scaled delta (game-seconds elapsed this tick) for system use.
    /// </summary>
    public float Tick(float realDeltaTime)
    {
        float scaled = realDeltaTime * TimeScale;
        TotalTime   += scaled;
        return scaled;
    }

    /// <summary>Returns deltaTime scaled by the current TimeScale.</summary>
    public float GetScaledDelta(float realDeltaTime) => realDeltaTime * TimeScale;
}
