using Warden.Anthropic;

namespace Warden.Orchestrator.Cache;

/// <summary>
/// Assembles a <see cref="MessageRequest"/> using the four-slab prompt model.
/// Slabs 1–3 always carry <c>cache_control</c>; slab 4 (the user turn) never does.
/// </summary>
/// <remarks>
/// Slab layout:
/// <list type="number">
///   <item>Role frame — hard-coded, from <see cref="CachedPrefixSource"/>.</item>
///   <item>Corpus — engine docs from manifest, from <see cref="CachedPrefixSource"/>.</item>
///   <item>Mission slabs — caller-supplied; must all be cached.</item>
///   <item>User turn — per-request variable content; placed in <c>Messages[0]</c>, never cached.</item>
/// </list>
/// </remarks>
public sealed class PromptCacheManager
{
    private readonly CachedPrefixSource _source;

    public PromptCacheManager(CachedPrefixSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>
    /// Builds a <see cref="MessageRequest"/> with cache markers applied to slabs 1–3.
    /// </summary>
    /// <param name="model">Model to invoke.</param>
    /// <param name="userTurnBody">
    /// The per-request task text. Goes in <c>Messages[0]</c> with no cache marker.
    /// Must not be null or empty. If it exceeds ~4 000 tokens (heuristic: 16 000 chars),
    /// a warning is emitted to stderr — that is usually shared context misplaced here.
    /// </param>
    /// <param name="missionSlabs">
    /// Optional slab-3 content blocks (e.g. Opus mission framing). Every entry must
    /// have <see cref="CacheDisposition"/> other than <see cref="CacheDisposition.Uncached"/>;
    /// otherwise an <see cref="ArgumentException"/> is thrown.
    /// </param>
    /// <param name="maxTokens">Maximum output tokens. Defaults to 8 192.</param>
    /// <param name="expectedTotalLatency">
    /// When ≥ 5 minutes, all cached slabs use the 1-hour TTL; otherwise the 5-minute TTL.
    /// Pass <see cref="TimeSpan.FromMinutes(30)"/> for Haiku batch submissions.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="userTurnBody"/> is null/empty, or when any
    /// <paramref name="missionSlabs"/> entry carries <see cref="CacheDisposition.Uncached"/>.
    /// </exception>
    public MessageRequest BuildRequest(
        ModelId                      model,
        string                       userTurnBody,
        IReadOnlyList<PromptSlab>?   missionSlabs          = null,
        int                          maxTokens              = 8192,
        TimeSpan?                    expectedTotalLatency   = null)
    {
        if (string.IsNullOrEmpty(userTurnBody))
            throw new ArgumentException("User turn body cannot be null or empty.", nameof(userTurnBody));

        if (missionSlabs is not null)
        {
            foreach (var slab in missionSlabs)
            {
                if (slab.Cache == CacheDisposition.Uncached)
                    throw new ArgumentException(
                        $"Mission slab '{slab.Name}' has CacheDisposition.Uncached. " +
                        "Mission slabs must be cached (Ephemeral5m or Ephemeral1h).",
                        nameof(missionSlabs));
            }
        }

        // Heuristic: ~4 chars per token on average English text.
        if (userTurnBody.Length / 4 > 4000)
        {
            Console.Error.WriteLine(
                $"[PromptCacheManager WARNING] userTurnBody is ~{userTurnBody.Length / 4} tokens " +
                $"({userTurnBody.Length} chars). Large user turns are usually a sign that shared " +
                "context was placed here instead of in a mission slab.");
        }

        var cacheControl = BuildCacheControl(DetermineTtl(expectedTotalLatency));

        var system = new List<ContentBlock>();

        // Slab 1 — role frame
        system.Add(new TextBlock(_source.GetRoleFrameText(), cacheControl));

        // Slab 2 — corpus (engine docs + schemas)
        system.Add(new TextBlock(_source.GetCorpusText(), cacheControl));

        // Slab 3 — mission slabs (optional, each with the same TTL)
        if (missionSlabs is not null)
            foreach (var slab in missionSlabs)
                system.Add(new TextBlock(slab.Text, cacheControl));

        // Slab 4 — user turn; no cache_control, placed in Messages not System
        var messages = new List<MessageTurn>
        {
            new("user", [new TextBlock(userTurnBody)])
        };

        return new MessageRequest(model, maxTokens, messages)
        {
            System = system,
        };
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static CacheDisposition DetermineTtl(TimeSpan? expectedTotalLatency)
    {
        if (expectedTotalLatency.HasValue && expectedTotalLatency.Value >= TimeSpan.FromMinutes(5))
            return CacheDisposition.Ephemeral1h;
        return CacheDisposition.Ephemeral5m;
    }

    private static CacheControl BuildCacheControl(CacheDisposition disposition)
    {
        return disposition switch
        {
            CacheDisposition.Ephemeral5m => new CacheControl("ephemeral"),
            CacheDisposition.Ephemeral1h => new CacheControl("ephemeral", "1h"),
            _ => throw new ArgumentOutOfRangeException(nameof(disposition), disposition, null),
        };
    }
}
