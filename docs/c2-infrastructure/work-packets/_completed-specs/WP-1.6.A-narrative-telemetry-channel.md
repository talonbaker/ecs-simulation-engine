# WP-1.6.A — Narrative Telemetry Channel

**Tier:** Sonnet
**Depends on:** WP-1.4.A (social engine — drives, willpower, inhibitions). Already merged on `staging`. WP-1.1.A (proximity events). Already merged.
**Parallel-safe with:** WP-1.2.A (Lighting), WP-1.3.A (Movement). Different file footprints; only `SimulationBootstrapper.cs` and `ECSCli/Program.cs` are commonly touched and conflicts there are sectional/auto-mergeable.
**Timebox:** 75 minutes
**Budget:** $0.30

---

## Goal

Land the narrative-telemetry channel — a detector that watches engine state for *notable* moments and emits structured `NarrativeEventCandidate` records, plus a CLI verb that streams these candidates for design-time observability. The candidates are how Phase-1.7+ Sonnets and Phase-1.8 Haikus will recognize "something interesting just happened," which is the seam where the simulation's emergent stories become legible to content tooling.

Three things land:

1. **`NarrativeEventDetector`** — runs each tick. Watches drive deltas (one tick to the next), proximity events from WP-1.1.A's bus, and willpower deltas from WP-1.4.A. Emits a `NarrativeEventCandidate` when thresholds fire. The detector is *open-loop* — it doesn't decide which candidates persist (that's the chronicle packet later, WP-1.9.A); it just emits them.

2. **`NarrativeEventBus`** — singleton bus carrying `OnCandidateEmitted` events. Subscribers receive candidates in deterministic order.

3. **`ECSCli ai narrative-stream`** — new CLI verb that runs the simulation and emits one JSON-line per candidate to stdout (or `--out file.jsonl`). Pattern matches the existing `ai stream` verb. Designed to be tailed by a Sonnet during development to see what the world is producing.

What this packet does **not** do: persist any candidate (chronicle is WP-1.9.A); correlate candidates into stories (also chronicle); make any judgment about which candidates are interesting "enough" (a future tunable); inject candidates back into the simulation as memory events (memory recording is also a follow-up).

---

## Reference files

- `docs/c2-content/DRAFT-action-gating.md` — context on what kinds of moments count as notable. Drive spikes, willpower depletion, inhibition near-misses are the highest-signal events. **Read first.**
- `docs/c2-content/DRAFT-cast-bible.md` — the eight drives and what their elevations mean.
- `docs/c2-content/DRAFT-world-bible.md` — persistence threshold §"Sticks". A guide for what *eventually* gets chronicled; this packet emits *candidates*, the chronicle decides what sticks.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.4.A.md` — confirms social drives, willpower, willpower events queue are available.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.1.A.md` — confirms proximity event bus is available.
- `APIFramework/Components/SocialDrivesComponent.cs` — drive-delta detection reads from here.
- `APIFramework/Components/WillpowerComponent.cs` — willpower-delta detection reads from here.
- `APIFramework/Systems/Spatial/ProximityEventBus.cs` — narrative detector subscribes to this for proximity-mediated events.
- `APIFramework/Systems/WillpowerEventQueue.cs` — for tracking willpower events.
- `ECSCli/Ai/AiStreamCommand.cs` — pattern reference for the new `ai narrative-stream` verb. The new verb mirrors its structure.
- `ECSCli/Ai/AiCommand.cs` — for the dispatch surface.
- `ECSCli/Program.cs` — for command registration.
- `APIFramework/Core/SimulationBootstrapper.cs` — system + service registration site.
- `APIFramework/Core/SeededRandom.cs` — RNG source. Required for any random nudges in the detector. The detector itself should be deterministic; no RNG is required, but it'll be added for any future tunable noise.
- `SimConfig.json` — runtime tuning lives here.

## Non-goals

- Do **not** modify `Warden.Telemetry/TelemetryProjector.cs` or any file under `Warden.Telemetry/`. The narrative channel is a separate stream from the world-state telemetry; it doesn't go on the wire format. **This is the parallel-safety contract with WP-1.2.A and WP-1.3.A.**
- Do **not** modify any file under `Warden.Contracts/`. No new DTOs in the wire-format contracts; `NarrativeEventCandidate` is engine-internal at this stage. (A future packet may expose it on the wire when content-validation Haikus need it.)
- Do **not** modify any file under `docs/c2-infrastructure/schemas/`. No schema bump.
- Do **not** persist any candidate. No file writes during simulation, no chronicle entries. The CLI verb writes a JSONL stream to the file the user specifies, but that's a debug aid, not a save.
- Do **not** correlate candidates into stories. "Donna's irritation spiked → Donna left the breakroom abruptly → Frank laughed" is three candidates, three lines of stream output. The story they tell together is a Haiku's job to recognize, not the engine's.
- Do **not** filter candidates by interest. Every threshold-crossing produces a candidate. Quality filtering (the chronicle's "would they still be talking about this in a month" test) is later.
- Do **not** inject candidates back into the simulation as memory events. That's the memory-recording packet (Phase 1.4 follow-up).
- Do **not** change drive thresholds globally. Detector thresholds for "notable" are tunable in SimConfig and start at sensible defaults.
- Do **not** broadcast the event over network or external endpoints. The bus is in-process only.
- Do **not** add a NuGet dependency.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (Architectural axiom 8.1.)

---

## Design notes

### What counts as a notable moment — three classes

**Drive-delta candidates.** Per NPC, per tick, compare each of the eight drives' `Current` to the value at the previous tick. If `|delta| >= SimConfig.narrativeDriveSpikeThreshold` (default 15 points), emit a `DriveSpike` candidate. The candidate carries: NPC entity id, drive name, before/after values, tick.

A small per-drive cache lives inside the detector (`Dictionary<int entityId, int[8] previousDriveValues>`). Updated each tick.

**Willpower-delta candidates.** Per NPC, per tick, watch willpower's `Current`. If it drops by ≥ `SimConfig.narrativeWillpowerDropThreshold` (default 10) in a single tick, emit a `WillpowerCollapse` candidate (the gate breaking — a sustained suppression released). If it crosses below `SimConfig.narrativeWillpowerLowThreshold` (default 20) for the first time after being above it, emit a `WillpowerLow` candidate (the gate weakening).

**Proximity-mediated candidates.** Subscribe to `ProximityEventBus`:
- `OnEnteredConversationRange` — emit a `ConversationStarted` candidate. Carries both NPC ids and the room they're in.
- `OnRoomMembershipChanged` — when an NPC leaves a room *while a notable drive delta just occurred*, emit a `LeftRoomAbruptly` candidate. The "abruptly" detection is "drive delta was emitted on this NPC within the last 3 ticks."

These are starting heuristics. The chronicle packet will tune them; this packet ships the *capability* with sensible thresholds.

### Candidate shape

```csharp
public sealed record NarrativeEventCandidate(
    long Tick,
    NarrativeEventKind Kind,            // DriveSpike, WillpowerCollapse, WillpowerLow,
                                        // ConversationStarted, LeftRoomAbruptly
    IReadOnlyList<int> ParticipantIds,  // 1–N entity ids involved
    string? RoomId,                     // null if not room-localized
    string Detail                       // a short structured-summary string, max 280 chars
);
```

The `Detail` field carries human-readable context: `"irritation: 30 → 65 (+35)"` or `"willpower collapsed: 45 → 12 (-33)"` or `"conversation started in first-floor-breakroom"`. Format is `key: value (delta)` style for drive/willpower events; free phrasing for proximity events. `Detail` is informational only; consumers parse `Kind` + structured fields.

### NarrativeEventBus

`NarrativeEventBus` is a singleton in DI:

```csharp
public sealed class NarrativeEventBus
{
    public event Action<NarrativeEventCandidate>? OnCandidateEmitted;
    public void RaiseCandidate(NarrativeEventCandidate candidate);
}
```

Events fire in deterministic order (entity-id ascending within a tick; chronological across ticks).

### ECSCli ai narrative-stream verb

Command signature mirrors existing `ai stream`:

```
ECSCli ai narrative-stream
    --interval <gameSeconds>   # tick interval (defaults to existing stream defaults)
    --duration <gameSeconds>   # how long to run; missing = forever
    --out <path.jsonl>         # output file; missing = stdout
    --seed <n>                 # seed (defaults to existing default)
```

Each candidate emitted by the bus is serialized as a single JSON line and flushed to the output stream. JSON shape mirrors the C# record (camelCase fields, kebab-case enums, consistent with existing telemetry serialization).

The command uses the existing `Warden.Contracts` JSON serializer for consistency with other telemetry. Implementation lives in `ECSCli/Ai/AiNarrativeStreamCommand.cs`, registered in `ECSCli/Program.cs` alongside the existing `ai stream` command.

### Phase ordering

`NarrativeEventDetector` runs at the **end** of the tick, after all engine state has settled (after movement, social, lighting, proximity events). Reading "what just happened this tick" requires all systems to have written their state.

In `SystemPhase`, add a `Narrative` phase that runs last. Or slot into an existing post-everything phase if cleaner.

### Determinism

The detector is deterministic — it reads engine state and emits candidates with no RNG. Two runs with the same seed produce byte-identical candidate streams.

### SimConfig additions

```jsonc
{
  "narrative": {
    "driveSpikeThreshold":         15,
    "willpowerDropThreshold":      10,
    "willpowerLowThreshold":       20,
    "abruptDepartureWindowTicks":   3,
    "candidateDetailMaxLength":   280
  }
}
```

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Systems/Narrative/NarrativeEventCandidate.cs` | The record per Design notes. |
| code | `APIFramework/Systems/Narrative/NarrativeEventKind.cs` | Enum: `DriveSpike, WillpowerCollapse, WillpowerLow, ConversationStarted, LeftRoomAbruptly`. |
| code | `APIFramework/Systems/Narrative/NarrativeEventBus.cs` | Singleton event bus per Design notes. |
| code | `APIFramework/Systems/Narrative/NarrativeEventDetector.cs` | Per-tick system: subscribes to ProximityEventBus, watches drive + willpower deltas, emits candidates via NarrativeEventBus. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `NarrativeEventBus` (singleton), `NarrativeEventDetector` system in the new `Narrative` phase. |
| code | `APIFramework/Core/SystemPhase.cs` (modified, if needed) | Add a `Narrative` phase that runs last, or document why an existing phase suffices. |
| code | `SimConfig.json` (modified) | Add the `narrative` section per Design notes. |
| code | `ECSCli/Ai/AiNarrativeStreamCommand.cs` | New CLI verb mirroring `AiStreamCommand`. Subscribes to the narrative bus, writes JSONL to stdout or file. |
| code | `ECSCli/Program.cs` (modified) | Register the new `narrative-stream` command alongside existing `ai` verbs. |
| code | `APIFramework.Tests/Systems/Narrative/NarrativeEventDetectorTests.cs` | (1) Drive spike of +20 emits a `DriveSpike` candidate with correct before/after. (2) Drive spike of +5 (below threshold) emits no candidate. (3) Willpower drop of -15 emits a `WillpowerCollapse` candidate. (4) Willpower crossing 25→18 emits a `WillpowerLow` candidate; subsequent ticks below 20 do not re-emit. (5) `EnteredConversationRange` event emits a `ConversationStarted` candidate. (6) `RoomMembershipChanged` within 3 ticks of a drive spike emits `LeftRoomAbruptly`. |
| code | `APIFramework.Tests/Systems/Narrative/NarrativeEventBusTests.cs` | Order: candidates fire in entity-id ascending order within a tick. Determinism: two runs same seed → byte-identical stream. |
| code | `ECSCli.Tests/AiNarrativeStreamCommandTests.cs` | Integration test: command runs for 600 game-seconds, writes valid JSONL with at least one candidate of each common kind. JSONL parses without error. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-1.6.A.md` | Completion note. Standard template. Enumerate (a) what events are now detected, (b) what's deferred (chronicle persistence, story correlation, memory recording, candidate filtering). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `NarrativeEventDetector` emits a `DriveSpike` candidate when a drive's `Current` changes by ≥ `narrativeDriveSpikeThreshold` between ticks. | unit-test |
| AT-02 | `NarrativeEventDetector` does not emit a candidate when a drive changes by less than the threshold. | unit-test |
| AT-03 | `NarrativeEventDetector` emits a `WillpowerCollapse` candidate when willpower drops by ≥ `narrativeWillpowerDropThreshold` between ticks. | unit-test |
| AT-04 | `NarrativeEventDetector` emits a `WillpowerLow` candidate the first tick willpower crosses below `narrativeWillpowerLowThreshold`; does not re-emit on subsequent low ticks. | unit-test |
| AT-05 | `NarrativeEventDetector` re-emits `WillpowerLow` if willpower rose above the threshold and then dropped below again. | unit-test |
| AT-06 | `NarrativeEventDetector` emits `ConversationStarted` candidates in response to `EnteredConversationRange` events. | unit-test |
| AT-07 | `NarrativeEventDetector` emits `LeftRoomAbruptly` when a `RoomMembershipChanged` event fires within `abruptDepartureWindowTicks` of a `DriveSpike` for the same NPC. | unit-test |
| AT-08 | `NarrativeEventBus` fires candidates in entity-id ascending order within a tick. | unit-test |
| AT-09 | `NarrativeEventBus` is deterministic across two runs with the same seed (5000 ticks → byte-identical stream). | unit-test |
| AT-10 | `AiNarrativeStreamCommand` writes valid JSONL to the specified output path. Each line parses as a valid `NarrativeEventCandidate`. | unit-test |
| AT-11 | `AiNarrativeStreamCommand` integration test produces ≥ 1 candidate of `DriveSpike`, `WillpowerLow`, and `ConversationStarted` kinds within a 600-game-second run with default seed. | unit-test |
| AT-12 | `Warden.Telemetry.Tests` all pass — projector unchanged. | build + unit-test |
| AT-13 | All existing `APIFramework.Tests` stay green (rooms, social, lighting, movement, physiology). | build + unit-test |
| AT-14 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-15 | `dotnet test ECSSimulation.sln` — every existing test stays green; new tests pass. | build |

---

## Followups (not in scope)

- WP-1.9.A — Persistent chronicle (v0.4): candidate filtering against the bible's persistence threshold ("would they still be talking about this in a month"); persisted chronicle entries; spill-stays-spilled mechanic via Stain/BrokenItem entity templates.
- Memory recording driven by candidates (Phase 1.4 follow-up): notable conversations within proximity range produce memory event records on the relationship entity between the participants.
- Story correlation / arc detection — a Haiku reads the candidate stream over a window and recognizes "this is the start of an argument" or "this is a romance forming." Not engine work; tooling work.
- Refined detection: drive deltas weighted by archetype (a Cynic's irritation spike is less notable than a Hermit's; the cynic spikes constantly, the hermit only when something real happened). Cast-generator integration.
- More candidate kinds: physiology-related (someone's bladder critical mid-meeting); workload-related (deadline missed); social-mask-related (mask cracked publicly). Add as the contributing systems land.
- Candidate-filter Haikus: design-time Haiku batches that read the candidate stream and rate candidates for interest. Output drives chronicle filtering thresholds.
- Per-NPC notability calibration — what's notable for one NPC is mundane for another. A drift detector could learn an NPC's typical day and only emit candidates for departures from it.
