namespace APIFramework.Systems.Chronicle;

/// <summary>
/// Persistent chronicle event kind. Mirrors <c>Warden.Contracts.Telemetry.ChronicleEventKind</c>.
/// Integer values must stay in sync so the projector can cast (int) between them.
/// </summary>
public enum ChronicleEventKind
{
    /// <summary>An NPC spilled or stained something — accompanied by a Stain physical manifestation.</summary>
    SpilledSomething   = 0,
    /// <summary>An item was broken — accompanied by a BrokenItem physical manifestation.</summary>
    BrokenItem         = 1,
    /// <summary>Two or more NPCs had a heated public exchange.</summary>
    PublicArgument     = 2,
    /// <summary>An NPC was publicly embarrassed in front of others.</summary>
    PublicHumiliation  = 3,
    /// <summary>A romantic affair was exposed.</summary>
    AffairRevealed     = 4,
    /// <summary>An NPC received a promotion.</summary>
    Promotion          = 5,
    /// <summary>An NPC was fired or laid off.</summary>
    Firing             = 6,
    /// <summary>An NPC performed an act of kindness during a crisis moment.</summary>
    KindnessInCrisis   = 7,
    /// <summary>An NPC betrayed another (broken trust, exposed secret, etc.).</summary>
    Betrayal           = 8,
    /// <summary>An NPC died or permanently departed the world.</summary>
    DeathOrLeaving     = 9,
    /// <summary>Unclassified — fallback when no other kind matches.</summary>
    Other              = 10
}
