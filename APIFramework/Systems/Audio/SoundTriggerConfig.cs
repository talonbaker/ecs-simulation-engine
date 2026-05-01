namespace APIFramework.Systems.Audio;

public class SoundTriggerConfig
{
    /// <summary>Ticks between BulbBuzz emissions for a Flickering light source. Default 10.</summary>
    public int BulbBuzzEmitIntervalTicks { get; set; } = 10;
    /// <summary>Intensity for Footstep. Default 0.3.</summary>
    public float FootstepIntensity { get; set; } = 0.3f;
    /// <summary>Intensity for ChairSqueak. Default 0.4.</summary>
    public float ChairSqueakIntensity { get; set; } = 0.4f;
    /// <summary>Intensity for BulbBuzz. Default 0.2.</summary>
    public float BulbBuzzIntensity { get; set; } = 0.2f;
    /// <summary>Intensity for Chew. Default 0.15.</summary>
    public float ChewIntensity { get; set; } = 0.15f;
    /// <summary>Intensity for Slurp. Default 0.2.</summary>
    public float SlurpIntensity { get; set; } = 0.2f;
    /// <summary>Intensity for Cough. Default 0.6.</summary>
    public float CoughIntensity { get; set; } = 0.6f;
    /// <summary>Intensity for Gasp. Default 0.7.</summary>
    public float GaspIntensity { get; set; } = 0.7f;
    /// <summary>Intensity for Wheeze. Default 0.4.</summary>
    public float WheezeIntensity { get; set; } = 0.4f;
    /// <summary>Intensity for Slip. Default 0.8.</summary>
    public float SlipIntensity { get; set; } = 0.8f;
    /// <summary>Intensity for Thud. Default 0.9.</summary>
    public float ThudIntensity { get; set; } = 0.9f;
    /// <summary>Intensity for SpeechFragment (Loud register multiplier). Default 1.0.</summary>
    public float SpeechFragmentLoudIntensity { get; set; } = 1.0f;
    /// <summary>Intensity for SpeechFragment (Normal register). Default 0.6.</summary>
    public float SpeechFragmentNormalIntensity { get; set; } = 0.6f;
    /// <summary>Intensity for SpeechFragment (Quiet register). Default 0.3.</summary>
    public float SpeechFragmentQuietIntensity { get; set; } = 0.3f;
    /// <summary>Intensity for Sneeze. Default 0.7.</summary>
    public float SneezeIntensity { get; set; } = 0.7f;
    /// <summary>Intensity for Yawn. Default 0.4.</summary>
    public float YawnIntensity { get; set; } = 0.4f;
    /// <summary>Intensity for Sigh. Default 0.3.</summary>
    public float SighIntensity { get; set; } = 0.3f;
}
