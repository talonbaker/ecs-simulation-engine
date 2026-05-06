# WP-05 — Warden.Anthropic (Thin Messages + Batches Client)

**Tier:** Sonnet
**Depends on:** WP-02
**Timebox:** 90 minutes
**Budget:** $0.40

---

## Goal

Write the narrowest possible HTTP client for Anthropic's Messages and Message Batches endpoints. Nothing beyond what the orchestrator needs. The client owns the on-wire shape; nothing else in Warden.* should ever assemble HTTP bodies itself.

---

## Reference files

- `docs/c2-infrastructure/00-SRD.md` §2 Pillar D
- `docs/c2-infrastructure/02-cost-model.md`
- `Warden.Contracts/JsonOptions.cs` (from WP-02)
- Anthropic docs, current as of 2026-04: `https://docs.claude.com/en/api/messages` and `https://docs.claude.com/en/api/creating-message-batches`. **Read these before writing the body shapes.** Do not hand-roll from memory.

## Non-goals

- No streaming support. Non-streaming only.
- No retries here — retries belong to the orchestrator via Polly. This client surfaces one-shot failures.
- No cost calculation. That is `CostRates.cs`, consumed by WP-08's ledger.
- No prompt assembly. That is WP-06.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Anthropic/ModelId.cs` | `public readonly record struct ModelId(string Name)` with static `OpusV46`, `SonnetV46`, `HaikuV45` values. A private constructor prevents ad-hoc strings. |
| code | `Warden.Anthropic/CostRates.cs` | Per-model rate struct. Source of truth for the numbers in `02-cost-model.md` §1. Marked `static readonly` with a `DateOnly PricedAsOf` field so tests can assert the file has been reviewed since a cutoff. |
| code | `Warden.Anthropic/AnthropicClient.cs` | `public sealed class AnthropicClient : IDisposable`. Public surface: `Task<MessageResponse> CreateMessageAsync(MessageRequest, CancellationToken)`, `Task<BatchSubmission> CreateBatchAsync(BatchRequest, CancellationToken)`, `Task<BatchStatus> GetBatchAsync(string batchId, CancellationToken)`, `IAsyncEnumerable<BatchResultEntry> StreamBatchResultsAsync(string batchId, CancellationToken)`. |
| code | `Warden.Anthropic/MessageRequest.cs` | Record covering: `ModelId`, `List<MessageTurn> Messages`, `List<ContentBlock>? System`, `int MaxTokens`, `double? Temperature`, metadata. |
| code | `Warden.Anthropic/MessageTurn.cs` | Role (`user`/`assistant`) + content blocks. |
| code | `Warden.Anthropic/ContentBlock.cs` | Tagged union over `text`, `tool_use`, `tool_result`. Carries optional `CacheControl`. |
| code | `Warden.Anthropic/CacheControl.cs` | `public sealed record CacheControl(string Type = "ephemeral", string? Ttl = null)` where `Ttl ∈ {"5m", "1h"}`. |
| code | `Warden.Anthropic/MessageResponse.cs` | Includes `TokenUsage` (input, cache_creation_input, cache_read_input, output). |
| code | `Warden.Anthropic/BatchRequest.cs` | Typed batch submission: list of custom-id-tagged `MessageRequest` entries. |
| code | `Warden.Anthropic/BatchSubmission.cs` | Return from create: id, created_at, processing_status. |
| code | `Warden.Anthropic/BatchStatus.cs` | Poll result: counts, status enum. |
| code | `Warden.Anthropic/BatchResultEntry.cs` | One custom-id-tagged result when streaming the completed batch. |
| code | `Warden.Anthropic/AnthropicApiException.cs` | Thrown on 4xx/5xx. Carries status code, response body (truncated at 4k), and a `bool IsRetryable` derived from the code. |
| code | `Warden.Anthropic/Internal/HttpClientFactory.cs` | Configures a single `HttpClient` with base address `https://api.anthropic.com/`, `x-api-key` header, `anthropic-version: 2023-06-01`, `anthropic-beta: prompt-caching-2024-07-31, message-batches-2024-09-24`, user-agent. Honours `--base-url` override for tests. |
| code | `Warden.Anthropic.Tests/` — new project | Unit tests with `HttpMessageHandler` stub. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-05.md` | Completion note. List every beta header you added and why. |

Update `ECSSimulation.sln` with `Warden.Anthropic.Tests`.

---

## Design notes

**`cache_control` placement.** Per Anthropic, `cache_control` attaches to a content block (not a message). The client must preserve the caller's cache markers verbatim on the wire. Do not invent or move cache markers here — that is the cache manager's job in WP-06.

**Beta headers.** Prompt caching and message batches are currently behind beta opt-in headers. Check current `anthropic-beta` flag names at implementation time; the values in `HttpClientFactory.cs` above are guidance, not guarantees. If the doc differs, the doc wins.

**Retry philosophy.** This client throws on non-2xx. `AnthropicApiException.IsRetryable` is a hint, not a policy. Polly in the orchestrator reads the hint. Do not decorate the client with Polly here — that couples the two projects and makes testing retries harder.

**Offline testing.** Inject an `HttpMessageHandler` via constructor so tests can return canned responses without a network hop. One test per public method, with at least one error-path case each.

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `CreateMessageAsync` produces a request body that deserialises to Anthropic's documented `POST /v1/messages` shape. (Canonical sample in `AnthropicSamples/messages-request.json`.) | unit-test |
| AT-02 | `CacheControl` is serialised as `{"type":"ephemeral"}` or `{"type":"ephemeral","ttl":"1h"}` and only on the blocks it was attached to. | unit-test |
| AT-03 | `CreateBatchAsync` produces a valid `POST /v1/messages/batches` body with per-entry `custom_id`s. | unit-test |
| AT-04 | `StreamBatchResultsAsync` correctly parses JSONL results via the `results_url`. | unit-test |
| AT-05 | A 429 response is raised as `AnthropicApiException` with `IsRetryable == true`; a 400 with `IsRetryable == false`. | unit-test |
| AT-06 | `AnthropicClient` does not allocate more than one `HttpClient` per process instance (verify with a ref-tracking test). | unit-test |
| AT-07 | `ModelId` cannot be constructed with an arbitrary string from outside the assembly. | unit-test |
| AT-08 | `CostRates.PricedAsOf` ≥ `new DateOnly(2026, 4, 1)`; test fails if someone forgets to update. | unit-test |
