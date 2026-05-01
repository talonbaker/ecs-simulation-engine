using System.Collections.Generic;
using APIFramework.Core;

namespace APIFramework.Systems.Dialog;

/// <summary>
/// Singleton queue that DialogContextDecisionSystem writes and DialogFragmentRetrievalSystem reads.
/// Cleared at the start of each decision pass so the queue never carries stale items into the next tick.
/// </summary>
public sealed class PendingDialogQueue
{
    /// <summary>A pending (Speaker, Listener, Context) tuple awaiting fragment selection.</summary>
    /// <param name="Speaker">NPC that will speak.</param>
    /// <param name="Listener">NPC that will hear.</param>
    /// <param name="Context">Corpus context string used to filter fragments.</param>
    public readonly record struct PendingDialog(Entity Speaker, Entity Listener, string Context);

    private readonly List<PendingDialog> _queue = new();

    /// <summary>
    /// Adds a new pending pair to the queue.
    /// </summary>
    /// <param name="speaker">NPC that will speak.</param>
    /// <param name="listener">NPC that will hear.</param>
    /// <param name="context">Corpus context string used to filter fragments.</param>
    public void Enqueue(Entity speaker, Entity listener, string context)
        => _queue.Add(new PendingDialog(speaker, listener, context));

    /// <summary>The pending pairs added since the last <see cref="Clear"/>.</summary>
    public IReadOnlyList<PendingDialog> Items => _queue;

    /// <summary>Removes all pending pairs.</summary>
    public void Clear() => _queue.Clear();
}
