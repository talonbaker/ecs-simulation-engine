namespace ECSVisualizer.ViewModels;

/// <summary>
/// Holds every chart series tracked by the live dashboard.
///
/// ChartViewModel owns the series objects but does NOT push data into them.
/// MainViewModel is responsible for calling PushBio() and PushPerf() at the
/// right intervals so that game-time-based and real-time sampling stay aligned
/// with the simulation clock and UI refresh rate.
///
/// All series use 480 samples = 8 game-hours at 1 sample/game-minute.
/// Performance series use 120 samples = 2 minutes of real-time history.
/// </summary>
public sealed class ChartViewModel
{
    // -- Biological — per-entity (first living entity = "Billy") --------------

    /// <summary>Satiation 0–100 — how full the entity's stomach is. Drains as hunger builds.</summary>
    public ChartSeriesViewModel Satiation  { get; } = new("SATIATION",  "#30D158", 0, 100);

    /// <summary>Hydration 0–100 — water stores. Drains as thirst builds.</summary>
    public ChartSeriesViewModel Hydration  { get; } = new("HYDRATION",  "#0A84FF", 0, 100);

    /// <summary>Energy 0–100 — alertness level. Drains awake, restores while sleeping.</summary>
    public ChartSeriesViewModel Energy     { get; } = new("ENERGY",     "#FFD60A", 0, 100);

    /// <summary>Sleepiness 0–100 — pressure to sleep. Grows awake, drains while sleeping.</summary>
    public ChartSeriesViewModel Sleepiness { get; } = new("SLEEPINESS", "#6E40C9", 0, 100);

    /// <summary>Body temperature in Celsius. Normal = 37°C. Fever > 37.5°C.</summary>
    public ChartSeriesViewModel BodyTemp   { get; } = new("BODY TEMP",  "#FF9F0A", 34, 41);

    // -- Emotions (Plutchik) ---------------------------------------------------

    /// <summary>Joy 0–100 — positive mood. Rises after eating, social interactions, rest.</summary>
    public ChartSeriesViewModel Joy        { get; } = new("JOY",        "#FFD60A", 0, 100);

    /// <summary>Anger 0–100 — frustration, cortisol, unmet needs.</summary>
    public ChartSeriesViewModel Anger      { get; } = new("ANGER",      "#FF453A", 0, 100);

    /// <summary>Sadness 0–100 — low mood, withdrawn state.</summary>
    public ChartSeriesViewModel Sadness    { get; } = new("SADNESS",    "#0A84FF", 0, 100);

    // -- Performance -----------------------------------------------------------

    /// <summary>Real frames-per-second of the UI render loop (target 60).</summary>
    public ChartSeriesViewModel Fps        { get; } = new("REAL FPS",   "#00FF41", 0, 70,  capacity: 120);

    /// <summary>
    /// Number of living entities queried per tick.
    /// Tracks sim load growth as the world fills with agents.
    /// </summary>
    public ChartSeriesViewModel EntityLoad { get; } = new("ENTITIES",   "#64D2FF", 0, 20,  capacity: 120);
}
