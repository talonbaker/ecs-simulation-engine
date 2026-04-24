# WP-06 — Prompt Cache Manager (Four-Slab Prompt Model)

**Tier:** Sonnet
**Depends on:** WP-05
**Timebox:** 60 minutes
**Budget:** $0.30

---

## Goal

Encapsulate the orchestrator's prompt-assembly rules so slabs 1–3 always carry `cache_control` and slab 4 never does. Make it impossible for a caller to accidentally pollute a cached prefix with per-request data. This is the single biggest cost lever on the Sonnet tier — it needs a dedicated owner.

---

## Reference files

- `docs/c2-infrastructure/00-SRD.md` §2 Pillar D.1
- `docs/c2-infrastructure/02-cost-model.md` §3
- `Warden.Anthropic/MessageRequest.cs` (from WP-05)
- `Warden.Anthropic/CacheControl.cs` (from WP-05)

## Non-goals

- Do not call the Anthropic API from this packet. The cache manager assembles requests; the client sends them.
- Do not persist the engine fact sheet here — you read it from the filesystem each time. Regenerating the cache on content change is Anthropic's problem.
- Do not guess TTLs. The rule is in §Design notes below.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Orchestrator/Cache/PromptSlab.cs` | `public sealed record PromptSlab(string Name, string Text, CacheDisposition Cache)`. |
| code | `Warden.Orchestrator/Cache/CacheDisposition.cs` | `public enum CacheDisposition { Uncached, Ephemeral5m, Ephemeral1h }`. |
| code | `Warden.Orchestrator/Cache/PromptCacheManager.cs` | See public surface below. |
| code | `Warden.Orchestrator/Cache/CachedPrefixSource.cs` | Loads the four static files (engine fact sheet, engineering guide, architecture guide, schemas) and assembles slab 1. Caches the string in memory. Invalidates on file mtime change. |
| code | `Warden.Orchestrator.Tests/Cache/PromptCacheManagerTests.cs` | See acceptance. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-06.md` | Completion note including observed cache hit-rate on the smoke mission. |

### Public surface

```csharp
public sealed class PromptCacheManager
{
    public PromptCacheManager(CachedPrefixSource source);

    public MessageRequest BuildRequest(
        ModelId model,
        string userTurnBody,
        IReadOnlyList<PromptSlab>? missionSlabs = null,
        int maxTokens = 8192,
        TimeSpan? expectedTotalLatency = null);
}
```

`BuildRequest` lays out the system content blocks in this strict order:

1. Slab 1 (role frame) — hard-coded string owned by `CachedPrefixSource`. Always `Ephemeral5m` unless `expectedTotalLatency > 5 min`, in which case `Ephemeral1h`.
2. Slab 2 (engine fact sheet + docs) — loaded from disk by `CachedPrefixSource`. Same TTL as slab 1.
3. Slab 3 (mission slabs, optional) — passed by the caller, typically the Opus brief framing. Same TTL logic.
4. Slab 4 (user turn body) — goes in the `Messages[0]` user content, **not** the system. `CacheDisposition.Uncached` is the only legal value here.

If a caller passes a `missionSlab` with `Uncached`, `BuildRequest` throws `ArgumentException`. If a caller sets `userTurnBody` to null or empty, throws. If a caller sets `userTurnBody` to something that is clearly shared context (detect: >4000 tokens), log a warning — that is usually the bug.

---

## TTL selection rule

```
if (expectedTotalLatency >= TimeSpan.FromMinutes(5))
    use Ephemeral1h;
else
    use Ephemeral5m;
```

For interactive Sonnet fan-outs the 5-minute TTL is cheaper. For Haiku batch submissions, the orchestrator should pass `expectedTotalLatency = TimeSpan.FromMinutes(30)` because batch turn-around regularly crosses the 5-minute boundary and a cache miss on the second Haiku wave would undo the optimisation. This is the rule.

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `BuildRequest` output has `cache_control` on exactly the slabs it should (slabs 1–3), never on slab 4. | unit-test |
| AT-02 | Passing a mission slab with `Uncached` disposition throws `ArgumentException`. | unit-test |
| AT-03 | `expectedTotalLatency = null` defaults to 5m TTL; `= 10min` yields 1h TTL. | unit-test |
| AT-04 | Editing the engine fact sheet file on disk causes the next `BuildRequest` call to include the new content (mtime invalidation). | unit-test |
| AT-05 | Two consecutive `BuildRequest` calls within the same TTL window produce identical slab-1/2 bytes — proves the cache key is stable. | unit-test |
| AT-06 | `userTurnBody = ""` throws. | unit-test |
| AT-07 | Slab 1 begins with a clearly labelled role frame and ends with a boundary marker (`\n\n---\n\n`) so concatenation ambiguity is impossible. | manual-review |
