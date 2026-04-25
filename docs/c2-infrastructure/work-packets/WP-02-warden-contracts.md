# WP-02 — Warden.Contracts (DTOs, Schemas, Validator)

**Tier:** Sonnet
**Depends on:** WP-01
**Timebox:** 75 minutes
**Budget:** $0.30

---

## Goal

Translate the four JSON schemas in `docs/c2-infrastructure/schemas/` into C# record types, wire up schema-round-trip validation, and ensure the DTOs serialise to JSON that validates against the original schemas. This is the **Intelligence Handshake** backbone — every other tier depends on these types staying identical to the schemas.

---

## Reference files

- `docs/c2-infrastructure/00-SRD.md` §2 Pillar C
- `docs/c2-infrastructure/schemas/world-state.schema.json`
- `docs/c2-infrastructure/schemas/opus-to-sonnet.schema.json`
- `docs/c2-infrastructure/schemas/sonnet-result.schema.json`
- `docs/c2-infrastructure/schemas/sonnet-to-haiku.schema.json`
- `docs/c2-infrastructure/schemas/haiku-result.schema.json`
- `docs/c2-infrastructure/schemas/ai-command-batch.schema.json`
- `APIFramework/Core/SimulationSnapshot.cs` (style reference for record layout)

## Non-goals

- Do **not** connect these DTOs to the engine in this packet. That is WP-03.
- Do **not** call the Anthropic API from this packet. That is WP-05.
- Do **not** add behaviour to the records. These are pure data.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Contracts/Telemetry/WorldStateDto.cs` | Top-level record plus nested `EntityStateDto`, `ClockStateDto`, `PositionStateDto`, `DrivesStateDto`, `PhysiologyStateDto`, `WorldItemDto`, `WorldObjectDto`, `TransitItemDto`, `InvariantDigestDto`, `InvariantEventDto`. |
| code | `Warden.Contracts/Handshake/OpusSpecPacket.cs` | Includes nested `SpecInputs`, `SpecDeliverable`, `SpecAcceptanceTest`. |
| code | `Warden.Contracts/Handshake/SonnetResult.cs` | Includes nested `AcceptanceTestResult`, `DiffSummary`, `TokenUsage`. |
| code | `Warden.Contracts/Handshake/ScenarioBatch.cs` | Includes nested `ScenarioDto`, `ScenarioCommandDto`, `ScenarioAssertionDto`. |
| code | `Warden.Contracts/Handshake/HaikuResult.cs` | Includes nested `AssertionResult`, `TelemetryDigest`. |
| code | `Warden.Contracts/Handshake/AiCommandBatch.cs` | Tagged-union via `System.Text.Json` polymorphism on `kind`. |
| code | `Warden.Contracts/Handshake/OutcomeCode.cs` | Shared enum: `Ok`, `Blocked`, `Failed`. |
| code | `Warden.Contracts/Handshake/BlockReason.cs` | Shared enum mirroring all `blockReason` enums across schemas (union of Sonnet + Haiku). |
| code | `Warden.Contracts/SchemaValidation/SchemaValidator.cs` | `public static ValidationResult Validate<T>(string json, Schema s)` — uses `System.Text.Json` for parsing and a minimal JSON-Schema subset validator (Draft 2020-12, `type`/`required`/`enum`/`minimum`/`maximum`/`maxLength`/`maxItems`/`additionalProperties:false`). No NuGet dependency allowed beyond `System.Text.Json`. |
| code | `Warden.Contracts/SchemaValidation/Schema.cs` | Enum naming each embedded schema. Resources embedded via `<EmbeddedResource Include="**/*.schema.json"/>`. |
| code | `Warden.Contracts/SchemaValidation/ValidationResult.cs` | `record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)`. |
| code | `Warden.Contracts/JsonOptions.cs` | The canonical `JsonSerializerOptions` used by every tier. `PropertyNamingPolicy = CamelCase`, `WriteIndented = false`, `IncludeFields = false`, `DefaultIgnoreCondition = WhenWritingNull`. |
| code | `Warden.Contracts.Tests/SchemaRoundTripTests.cs` | Round-trip tests — see acceptance criteria. |
| code | `Warden.Contracts.Tests/SchemaValidatorTests.cs` | Validator unit tests covering each error type. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-02.md` | Completion note. |

---

## Design notes

**On polymorphism for `AiCommandBatch`.** Use `[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]` with `[JsonDerivedType]` attributes. The tagged-union in the schema maps 1:1.

**On enums.** Serialise as camelCase strings via a shared `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`. Never as integers.

**On the minimal validator.** We do not take a NuGet schema-validation dependency because (a) the existing suite of JSON-Schema NuGets is oversized, (b) we only use a narrow subset of Draft 2020-12, and (c) our validator runs on trusted wire messages — we need correctness on the whitelisted keywords, not coverage of every corner of the spec. Every unsupported keyword **must** throw `NotSupportedException` at load time, not silently pass. That is the discipline.

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | Each schema deserialises into its DTO and re-serialises to JSON that is semantically equal to a canonical sample. (Six samples in `Warden.Contracts.Tests/Samples/*.json`.) | unit-test |
| AT-02 | Mutating any required field to null, missing, or out-of-range fails `SchemaValidator.Validate` with a specific, actionable error. | unit-test |
| AT-03 | `SchemaValidator` rejects `additionalProperties:false` violations. | unit-test |
| AT-04 | `SchemaValidator` rejects `maxItems` overflow (e.g., 26 scenarios in a `ScenarioBatch`). | unit-test |
| AT-05 | Any schema keyword not in the supported set throws `NotSupportedException` on validator load, not on first call. | unit-test |
| AT-06 | `JsonOptions` produces identical JSON on both .NET 8 SDK 8.0.400 and the project's pinned SDK version (bit-for-bit). | unit-test |
| AT-07 | `dotnet build` warning count = 0. | build |
