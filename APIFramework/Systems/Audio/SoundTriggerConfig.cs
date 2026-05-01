namespace APIFramework.Systems.Audio;

/// <summary>Tuning knobs for engine-side sound trigger emission.</summary>
public class SoundTriggerConfig
{
    /// <summary>
    /// Ticks between successive BulbBuzz emissions while a fluorescent source is Flickering.
    /// At 20 game-ticks/second, 30 ticks ≈ 1.5 seconds between buzz events.
    /// </summary>
    public int BulbBuzzEmitIntervalTicks { get; set; } = 30;
}
