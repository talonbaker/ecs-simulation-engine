namespace Warden.Contracts.Telemetry;

/// <summary>
/// Chronicle event kind. Mirrors <c>APIFramework.Systems.Chronicle.ChronicleEventKind</c>.
/// Serialises as camelCase via <c>JsonSmartEnumConverterFactory</c>.
/// </summary>
public enum ChronicleEventKind
{
    SpilledSomething,
    BrokenItem,
    PublicArgument,
    PublicHumiliation,
    AffairRevealed,
    Promotion,
    Firing,
    KindnessInCrisis,
    Betrayal,
    DeathOrLeaving,
    Other
}
