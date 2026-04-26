using System.Collections.Generic;
using APIFramework.Core;

namespace APIFramework.Systems.Dialog;

/// <summary>
/// Singleton queue that DialogContextDecisionSystem writes and DialogFragmentRetrievalSystem reads.
/// Cleared at the start of each decision pass so the queue never carries stale items into the next tick.
/// </summary>
public sealed class PendingDialogQueue
{
    public readonly record struct PendingDialog(Entity Speaker, Entity Listener, string Context);

    private readonly List<PendingDialog> _queue = new();

    public void Enqueue(Entity speaker, Entity listener, string context)
        => _queue.Add(new PendingDialog(speaker, listener, context));

    public IReadOnlyList<PendingDialog> Items => _queue;

    public void Clear() => _queue.Clear();
}
