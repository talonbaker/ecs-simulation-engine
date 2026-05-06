using System;

namespace APIFramework.Systems.Audio;

public readonly record struct SoundTriggerEvent(
    SoundTriggerKind Kind,
    Guid             SourceEntityId,
    float            SourceX,
    float            SourceZ,
    float            Intensity,
    long             Tick,
    long             SequenceId
);
