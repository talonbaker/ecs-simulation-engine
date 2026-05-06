using System;

namespace APIFramework.Components;

public struct ThrownVelocityComponent
{
    public float VelocityX;
    public float VelocityZ;
    public float VelocityY;

    /// <summary>Fraction of velocity shed per tick (0.10 = 10% drag per tick).</summary>
    public float DecayPerTick;

    public long ThrownAtTick;
    public Guid ThrownByEntityId;
}
