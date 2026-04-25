using System.Collections.Generic;

namespace APIFramework.Components;

/// <summary>
/// Carries one or more post-it-style notes attached to an entity.
/// World-bootstrap sets these from <c>notesAttached[]</c> in the world definition.
/// <see cref="Notes"/> may be null if the struct was default-initialized; callers should null-check.
/// </summary>
public struct NoteComponent
{
    public IReadOnlyList<string>? Notes { get; init; }
}
