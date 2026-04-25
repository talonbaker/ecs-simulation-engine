using System.Text.Json;
using Warden.Anthropic;
using Warden.Contracts;
using Warden.Contracts.Handshake;

namespace Warden.Orchestrator.Cache;

/// <summary>
/// Assembles a prompt-cached <see cref="MessageRequest"/> for a Haiku scenario call.
/// This stub provides the minimal surface required by <see cref="Warden.Orchestrator.Batch.BatchScheduler"/>.
/// WP-06 replaces the body of <see cref="BuildRequest"/> with the full four-slab caching implementation.
/// </summary>
public sealed class PromptCacheManager
{
    /// <summary>
    /// Builds a <see cref="MessageRequest"/> for a single Haiku scenario.
    /// </summary>
    /// <param name="scenario">The scenario to evaluate.</param>
    /// <param name="expectedTotalLatency">
    /// Projected end-to-end batch latency. When longer than 5 minutes, slab 1 uses the 1-hour TTL.
    /// </param>
    public MessageRequest BuildRequest(ScenarioDto scenario, TimeSpan expectedTotalLatency)
    {
        // WP-06 assembles the real multi-slab cached prompt (engine fact sheet + docs + mission context).
        // This stub returns a minimal valid request so WP-07 can compile and test independently.
        var ttl = expectedTotalLatency > TimeSpan.FromMinutes(5) ? "1h" : null;
        var systemBlocks = new List<ContentBlock>
        {
            new TextBlock("ECS simulation engine context.", new CacheControl("ephemeral", ttl))
        };

        var userText = JsonSerializer.Serialize(scenario, JsonOptions.Wire);
        return new MessageRequest(
            ModelId.HaikuV45,
            4096,
            new List<MessageTurn> { new("user", new List<ContentBlock> { new TextBlock(userText) }) })
        {
            System = systemBlocks
        };
    }
}
