using System;
using System.Collections.Generic;

namespace APIFramework.Components;

/// <summary>Action class blocked by an inhibition. Mirrors SocialStateDto.InhibitionClass.</summary>
public enum InhibitionClass
{
    /// <summary>Acting on attraction outside an existing pairing.</summary>
    Infidelity,
    /// <summary>Direct face-to-face confrontation.</summary>
    Confrontation,
    /// <summary>Eating in front of others / body-image eating restraint.</summary>
    BodyImageEating,
    /// <summary>Outward expression of strong emotion in public.</summary>
    PublicEmotion,
    /// <summary>Initiating or accepting physical intimacy.</summary>
    PhysicalIntimacy,
    /// <summary>Engaging in interpersonal conflict (escalation, hostility).</summary>
    InterpersonalConflict,
    /// <summary>Taking risks (physical, social, or reputational).</summary>
    RiskTaking,
    /// <summary>Showing vulnerability to others.</summary>
    Vulnerability
}

/// <summary>
/// Whether the NPC is aware of the inhibition.
/// Hidden inhibitions shape behaviour without surfacing in dialogue or debug overlays.
/// </summary>
public enum InhibitionAwareness
{
    /// <summary>The NPC is consciously aware of the inhibition.</summary>
    Known,
    /// <summary>The NPC is unaware of the inhibition; it shapes behaviour without surfacing.</summary>
    Hidden
}

/// <summary>A single inhibition: what it blocks, how strongly, and whether the NPC knows about it.</summary>
public readonly struct Inhibition
{
    /// <summary>The action class this inhibition blocks.</summary>
    public InhibitionClass     Class     { get; }
    /// <summary>How strongly the inhibition acts as a veto, in [0, 100].</summary>
    public int                 Strength  { get; }   // 0–100
    /// <summary>Whether the NPC is aware of this inhibition.</summary>
    public InhibitionAwareness Awareness { get; }

    /// <summary>Constructs an inhibition. <paramref name="strength"/> is clamped to [0, 100].</summary>
    public Inhibition(InhibitionClass @class, int strength, InhibitionAwareness awareness)
    {
        Class     = @class;
        Strength  = Math.Clamp(strength, 0, 100);
        Awareness = awareness;
    }
}

/// <summary>
/// Up to eight inhibitions per NPC.
/// Action-selection reads this list to cost candidate actions.
/// Inhibition installation is deferred to the cast-generator (Phase 1.8);
/// this packet only carries the component shape and the decay-over-time system.
/// </summary>
public struct InhibitionsComponent
{
    private readonly IReadOnlyList<Inhibition>? _inhibitions;

    /// <summary>The NPC's inhibition list. Empty when the struct was default-initialised.</summary>
    public IReadOnlyList<Inhibition> Inhibitions =>
        _inhibitions ?? Array.Empty<Inhibition>();

    /// <summary>Constructs the component. Throws if more than 8 inhibitions are supplied.</summary>
    public InhibitionsComponent(IReadOnlyList<Inhibition> inhibitions)
    {
        if (inhibitions.Count > 8)
            throw new ArgumentException(
                "InhibitionsComponent supports at most 8 inhibitions.", nameof(inhibitions));
        _inhibitions = inhibitions;
    }
}
