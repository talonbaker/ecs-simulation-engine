using System;
using System.Collections.Generic;

namespace APIFramework.Components;

/// <summary>Action class blocked by an inhibition. Mirrors SocialStateDto.InhibitionClass.</summary>
public enum InhibitionClass
{
    Infidelity,
    Confrontation,
    BodyImageEating,
    PublicEmotion,
    PhysicalIntimacy,
    InterpersonalConflict,
    RiskTaking,
    Vulnerability
}

/// <summary>
/// Whether the NPC is aware of the inhibition.
/// Hidden inhibitions shape behaviour without surfacing in dialogue or debug overlays.
/// </summary>
public enum InhibitionAwareness { Known, Hidden }

/// <summary>A single inhibition: what it blocks, how strongly, and whether the NPC knows about it.</summary>
public readonly struct Inhibition
{
    public InhibitionClass     Class     { get; }
    public int                 Strength  { get; }   // 0–100
    public InhibitionAwareness Awareness { get; }

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

    public IReadOnlyList<Inhibition> Inhibitions =>
        _inhibitions ?? Array.Empty<Inhibition>();

    public InhibitionsComponent(IReadOnlyList<Inhibition> inhibitions)
    {
        if (inhibitions.Count > 8)
            throw new ArgumentException(
                "InhibitionsComponent supports at most 8 inhibitions.", nameof(inhibitions));
        _inhibitions = inhibitions;
    }
}
