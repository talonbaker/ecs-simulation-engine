using System;

namespace APIFramework.Systems.Visual;

public readonly record struct ParticleTriggerEvent(
    ParticleTriggerKind Kind,
    Guid                SourceEntityId,
    float               SourceX,
    float               SourceZ,
    float               IntensityMult,
    long                Tick,
    long                SequenceId
);
