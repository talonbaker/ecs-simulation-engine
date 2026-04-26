namespace APIFramework.Systems.Chronicle;

/// <summary>
/// Persistent chronicle event kind. Mirrors <c>Warden.Contracts.Telemetry.ChronicleEventKind</c>.
/// Integer values must stay in sync so the projector can cast (int) between them.
/// </summary>
public enum ChronicleEventKind
{
    SpilledSomething   = 0,
    BrokenItem         = 1,
    PublicArgument     = 2,
    PublicHumiliation  = 3,
    AffairRevealed     = 4,
    Promotion          = 5,
    Firing             = 6,
    KindnessInCrisis   = 7,
    Betrayal           = 8,
    DeathOrLeaving     = 9,
    Other              = 10
}
