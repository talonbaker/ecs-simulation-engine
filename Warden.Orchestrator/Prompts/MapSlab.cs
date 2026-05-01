using Warden.Orchestrator.Cache;

namespace Warden.Orchestrator.Prompts;

#if WARDEN
/// <summary>A rendered segment of the ASCII world map, ready for prompt injection.</summary>
public sealed record MapSlab(string Text, CacheDisposition Cache);
#endif
