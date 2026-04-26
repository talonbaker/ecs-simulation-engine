# Phase 1 Handoff Summary

> Authored by the Phase 1 Opus at closure. The comprehensive reference for the next Opus picking up Phase 2. Read after `PHASE-2-KICKOFF-BRIEF.md`. Companion to `PHASE-1-TEST-GUIDE.md` (operational verification).
>
> The shape of this document mirrors what the Phase 0 → Phase 1 handoff would have looked like, had it been written. It is the audit trail.

---

## 1. Phase 1 in one paragraph

Phase 1 took the Phase-0 orchestration factory and wired it to a real engine that simulates social, spatial, and emergent behavior. Schemas grew from v0.1 (physiology only) to v0.4 (social + spatial + chronicle). Sixteen new engine systems shipped covering rooms, lighting, movement quality, social drives, willpower, inhibitions, relationships, narrative event detection, persistent chronicle, and the silent-protagonist dialog mechanism. Five content bibles were authored (world, cast, aesthetic, action-gating, dialog) plus a new-systems-ideas doc that captures stress/social-mask/workload concepts deferred to Phase 2. The orchestrator's real-API path was proven end-to-end after six bugs were discovered and fixed in real-API testing — bugs that the mock-mode test suite had silently passed in Phase 0. The cast generator and a starter `office-starter.json` world definition shipped, enabling the engine to boot a populated office. The cast-validate smoke mission revealed an architectural mismatch between Sonnets-via-API and file-system reads — flagged for Phase 2 redesign. A post-closure pass on 2026-04-26 (see §4.1) resolved the three remaining ledger/report bugs that the original Phase-1 closure had punted to Phase 2; the live-API smoke run now produces a fully-populated report with non-zero Haiku ledger entries, schema-clean `parentBatchId`, and Sonnet/Haiku `tokensUsed` matching Anthropic Console. **The alive-feeling foundation is in place; the systems that turn that foundation into observable gameplay are Phase 2's job.**

---

## 2. What shipped — by axis

### 2.1 Schema versions (current state)

| Version | Status | What it carries | Packet that landed it |
|:---|:---|:---|:---|
| v0.1.0 | Frozen | Original physiology + clock + invariants | Phase 0 (WP-02) |
| v0.2.0 | Superseded | Social drives split self vs pair (the wrong design) | WP-1.0.A — corrected by 1.0.A.1 |
| v0.2.1 | Active | All 8 drives + willpower + inhibitions on entity; relationships as entities; pair-targeting removed | WP-1.0.A.1 |
| v0.3.0 | Active | Rooms + light sources + apertures + sun state | WP-1.0.B |
| v0.4.0 | Active | Persistent chronicle + Stain/BrokenItem references | WP-1.9.A |

The `schemaVersion` enum on `world-state.schema.json` accepts `["0.1.0", "0.2.1", "0.3.0", "0.4.0"]`. v0.2.0 was collapsed (no producer ever stamped it). Additive-compatibility holds across all four versions: a v0.1 sample validates clean under v0.4; a v0.4 emitter populates fields that v0.1 consumers ignore.

New schema files added in Phase 1:

- `archetypes.schema.json` v0.1.0 — the cast bible's archetype catalog shape.
- `world-definition.schema.json` v0.1.0 — what the engine boots from.
- `corpus.schema.json` v0.1.0 — the dialog phrase corpus shape.

### 2.2 Engine systems added (in `APIFramework`)

In approximate phase order per tick:

- `SpatialIndexSyncSystem`, `RoomMembershipSystem`, `ProximityEventSystem` — WP-1.1.A. The spatial layer.
- `SunSystem`, `LightSourceStateSystem`, `ApertureBeamSystem`, `IlluminationAccumulationSystem` — WP-1.2.A. Lighting state.
- `PathfindingTriggerSystem`, `MovementSpeedModifierSystem`, `StepAsideSystem`, `FacingSystem`, `IdleMovementSystem` — WP-1.3.A. Movement quality.
- `LightingToDriveCouplingSystem` — WP-1.5.A. The bible's flickering→irritation, sunbeam→mood, dark→suspicion mappings.
- `DriveDynamicsSystem`, `WillpowerSystem`, `RelationshipLifecycleSystem` — WP-1.4.A. Social engine.
- `NarrativeEventDetector` — WP-1.6.A. Drive-delta + proximity-driven candidate stream.
- `PersistenceThresholdDetector`, `PhysicalManifestSpawner` — WP-1.9.A. Chronicle filtering and Stain/BrokenItem materialization.
- `DialogContextDecisionSystem`, `DialogFragmentRetrievalSystem`, `DialogCalcifySystem` — WP-1.10.A. The silent-protagonist dialog stack.

Total: 16 new systems registered in `SimulationBootstrapper`. The engine fact sheet (`docs/engine-fact-sheet.md`, regenerable via `ECSCli ai describe`) lists all of them with phase + order.

### 2.3 Engine components added

Most live alongside existing physiological components in `APIFramework/Components/`:

- Spatial: `RoomComponent`, `RoomIllumination`, `BoundsRect`, `RoomCategory`, `BuildingFloor`, `ProximityComponent`, `RoomTag`.
- Lighting: `LightSourceComponent`, `LightApertureComponent`, `LightKind`, `LightState`, `ApertureFacing`, `DayPhase`, `SunStateRecord`, `LightSourceTag`, `LightApertureTag`.
- Movement: `HandednessComponent`, `HandednessSide`, `FacingComponent`, `FacingSource`, `PathComponent`, `ObstacleTag`. `MovementComponent` gained `SpeedModifier`.
- Social: `SocialDrivesComponent`, `WillpowerComponent`, `PersonalityComponent`, `InhibitionsComponent`, `RelationshipComponent`, `NpcTag`, `RelationshipTag`.
- Bootstrap: `NamedAnchorComponent`, `NoteComponent`, `NpcSlotComponent`, `NpcSlotTag`.
- Chronicle: `StainComponent`, `BrokenItemComponent`, `StainTag`, `BrokenItemTag`.
- Dialog: `DialogHistoryComponent`, `RecognizedTicComponent`.

### 2.4 Wire-format DTOs added (in `Warden.Contracts`)

Mirrors of every engine component above that's projectable. The projector populates these in `Warden.Telemetry/TelemetryProjector.cs`. Schema version stamp emitted: `"0.4.0"`.

### 2.5 CLI verbs added

- `ECSCli ai narrative-stream` — WP-1.6.A. Streams narrative event candidates as JSONL for design-time observability.
- `--world-definition <path>` flag added to existing `ai snapshot`, `ai stream`, `ai replay` — WP-1.7.A. Engine boots from a `world-definition.json` rather than hardcoded EntityTemplates.

### 2.6 Content artifacts authored

Drafts under `docs/c2-content/`:

- `DRAFT-world-bible.md` — the office. Three floors, named anchors, tone, gameplay loop, persistence threshold.
- `DRAFT-cast-bible.md` — the generator. 10 archetypes, drive catalog, register list, relationship-pattern library, silhouette dimensions, deal catalog.
- `DRAFT-aesthetic-bible.md` — priority-ordered foundation systems (lighting > proximity > movement); deferred visual rendering style.
- `DRAFT-action-gating.md` — willpower, inhibitions, approach-avoidance inversion, physiology-overridable-by-inhibition. The bible the action-selection layer (Phase 2) reads.
- `DRAFT-dialog-bible.md` — silent-protagonist principle, phrase-corpus shape, calcify mechanism, tic propagation.
- `DRAFT-new-systems-ideas.md` — StressSystem, WorkloadSystem, SocialMaskSystem (Phase 2+ candidates).

Data files under `docs/c2-content/`:

- `archetypes/archetypes.json` — 10 archetypes with drive ranges, personality ranges, register, willpower baseline, deal options, silhouette family, starter inhibitions, relationship spawn hints.
- `world-definitions/office-starter.json` — starter office layout with rooms, light sources, apertures, NPC slots, named anchors per the world bible.
- `dialog/corpus-starter.json` — ~200 hand-authored phrase fragments per the dialog bible's authoring scope.

---

## 3. Architectural decisions — Phase 1's discoveries

Three substantive design decisions landed in Phase 1 that were not in the original SRD:

### 3.1 The silent-protagonist dialog principle

When Talon proposed exploring small runtime LLMs (DistilBERT/GGUF/ONNX) for emergent NPC dialog, the answer was a hard no — the cast bible already commits to "voice emerges from gameplay, no pre-authored catchphrases" and "NPCs emote more than they speak." Runtime LLMs would violate SRD §8.1, hurt determinism, and *worsen* dialog quality compared to a curated corpus + emergent-tic mechanism. The dialog bible (`DRAFT-dialog-bible.md`) captures the principle:

- Hand-authored phrase corpus tagged by register, context, valence, relationship-fit, noteworthiness.
- Decision-tree retrieval at runtime reads NPC drive state + register + listener relationship and picks the best-fit fragment.
- Per-NPC fragment-use tracking; calcify threshold (default 8 same-context uses) flips a fragment to "tic" status; calcified fragments get +30% selection bias.
- Listeners that hear a calcified fragment ≥5 times mark it as "X's tic" — recognition propagates across NPCs over game-time.

The implementation (WP-1.10.A) ships the corpus + components + three systems. No LLM ever runs at game time.

### 3.2 The action-gating axiom (drives ≠ action)

Talon corrected the initial drive design (which had pair-targeted drives like "attraction toward Bob") with an insight worth quoting: *"every human is the same and the things that they differ in is the values of these drives compared to the values of the human player characters around them."* The result:

- All 8 drives live on `entities[].social.drives` (no pair-targeting).
- Each drive is `{current, baseline}` — captures both who-they-usually-are and how-they-feel-today.
- Willpower is a separate meta-state that gates feeling-into-doing (depletes with sustained suppression, regenerates with rest, varies per person).
- Inhibitions are hard blockers on action classes (sometimes hidden even from the NPC themselves) that override drive state.
- High drive can produce *avoidance* of the drive's target when stakes are high (Bob avoids Donna because he likes her).
- Physiology is overridable by inhibition (Sally at hunger 120% not eating because she thinks she's fat).

WP-1.0.A.1 corrected the schema; WP-1.4.A landed the engine components; the action-selection layer that *consults* these is the most foundational Phase 2 work.

### 3.3 The schema roadmap promotion (spatial v0.5 → v0.3)

The original schema roadmap put spatial / room overlay at v0.5. The aesthetic bible makes spatial a foundation system for both lighting (priority 1) and proximity (priority 2). The bibles forced a roadmap revision: spatial promoted to v0.3, chronicle to v0.4, characters to v0.5, etc. WP-1.0.B implemented the promotion. Documented in the updated `SCHEMA-ROADMAP.md`.

---

## 4. The orchestrator real-API path — bugs found and fixed

Phase 0's orchestrator passed all of its mock-mode tests but had never run a real-API mission to completion. Phase 1's first cast-validate run exposed five distinct bugs that all manifest only against the live API. Each fix is on `staging` now. Documented here so the next Opus understands the *category* of issue before the first real-API dispatch in Phase 2.

| # | Bug | Root cause | Fix |
|:---|:---|:---|:---|
| 1 | API returns 400 `cache_control cannot be set for empty text blocks` | `RunCommand` used `new PromptCacheManager()` (the test-mode parameterless constructor) for both mock and real API. With no `CachedPrefixSource` wired, slabs assembled to empty strings with `cache_control` markers — Anthropic rejects this. | `RunCommand` now wires `CachedPrefixSource` for real-API mode; `PromptCacheManager.BuildRequest` defensively skips empty slabs. |
| 2 | Sonnet response is markdown narration with embedded JSON, not raw JSON | Cached corpus role frame didn't tell the model to return raw JSON only; model improvised helpful narration. | Role frame updated to demand raw-JSON output; `SonnetDispatcher` defensively strips prose/code-fences via `ExtractJsonObject` before parsing. |
| 3 | Haiku responds as a Sonnet (returns SonnetResult-shaped JSON) | Haikus shared the Sonnet's role frame because both used the same cached prefix. | `CachedPrefixSource` now exposes separate `GetRoleFrameText()` and `GetHaikuRoleFrameText()`; `PromptCacheManager.BuildRequest` selects based on `model.Name.Contains("haiku")`. |
| 4 | Haiku result files persist to `runs/<runId>/<runId>/haiku-NN/...` (doubled runId) | `RunCommand` passed `runDir` to the Haiku's `InfraCoT` constructor instead of `root`; CoT internally composed `<basePath>/<runId>/...` again. | `RunCommand` passes `root` to `InfraCoT` for Haiku CoT, matching the Sonnet pattern. |
| 5 | Whole mission run crashes with `InvalidOperationException` when Sonnet returns `BlockReason.MissingReferenceFile` | `FailClosedEscalator.ApplyStateMachine` had no switch branch for that enum value. | Explicit branch added for `MissingReferenceFile`; catch-all `(Blocked, _)` branch added so future enum additions never crash. |

Issues 6, 7, and 8 below were left open at the original Phase-1 closure and resolved in the 2026-04-26 post-closure pass; they are documented here for the audit trail. The current state is captured in §4.1.

The Phase-1-closure handoff diagnosed two of the three as separate bugs ("aggregator scan path is wrong" + "ledger doesn't capture Haiku usage"). The post-closure investigation found they share a single root cause in `BatchScheduler.ParseSucceeded`, which never stamped the model's parsed `HaikuResult` with authoritative server-side values — the model emitted role-frame placeholders (`parentBatchId: "batch-pending"`, `tokensUsed: {0,0,0,0}`) and the orchestrator left them in place. The aggregator's scan path was actually fine (`SearchOption.AllDirectories` matches `haiku-*` at any depth); the join failed because `result.ParentBatchId` was the placeholder rather than the parent ScenarioBatch's `BatchId`. A symmetric bug existed on the Sonnet path (the persisted `result.json` carried a model-self-reported `tokensUsed` that diverged from the cost ledger).

**The cast-validate architectural finding** is the largest item Phase 1 left for Phase 2. The cast-validate spec asks the Sonnet to read `office-starter.json` and `archetypes.json` — but Sonnets dispatched through the orchestrator's API path don't have file-system access. They see only the cached corpus and the spec text in the user turn. The Sonnet correctly emits `outcome=blocked, reason=missing-reference-file`; the orchestrator now handles that gracefully (Patch 9 above), but the validation work doesn't actually happen. Two fix paths for Phase 2:

- **Inline mode** — orchestrator pre-reads files listed in `inputs.referenceFiles` and prepends them to the user turn. Adds ~24KB to the prompt. Cleanest fix.
- **Snapshot mode** — orchestrator boots the engine, runs cast generator, projects `WorldStateDto`, passes that as the spec input. Sonnet validates the projected world directly. Most ambitious; closest to what the bibles want long-term.

---

## 4.1 Post-closure pass — orchestrator parse-time stamping (2026-04-26)

The three open items from the original §4 closure list — Haiku ledger zero-tokens, report aggregator missing Tier 3, and the symmetric Sonnet `tokensUsed` divergence — were resolved in a single short pass. Three patches landed across two files; all 759 unit tests stay green; live-API smoke verified end-to-end.

| # | Bug (as originally diagnosed) | Actual root cause | Fix |
|:---|:---|:---|:---|
| 6 | "Cost ledger Haiku entries always show 0 tokens." | `BatchScheduler.ParseSucceeded` returned the model's parsed `HaikuResult` verbatim. The role frame instructs the model to placeholder `tokensUsed` to zeros, expecting the orchestrator to overwrite from the batch API response headers. The orchestrator never did. | `ParseSucceeded` now stamps `TokensUsed` from `succeeded.Message.Usage` before returning. |
| 7 | "Report aggregator scans wrong path under `sonnet-NN/haiku-NN/`." | Scan path was already correct (`SearchOption.AllDirectories` finds `haiku-*` at any depth). The actual failure was a **join-key mismatch**: `BatchScheduler.ParseSucceeded` left `parsed.ParentBatchId` at the role-frame placeholder `"batch-pending"`, while the aggregator joins Haikus to their parent Sonnet via `haiku.ParentBatchId == sonnet.scenarioBatch.batchId`. | `BatchScheduler.RunAsync` now builds a `scenarioToParentBatchId` map from the input ScenarioBatch list and stamps `ParentBatchId` with the **Sonnet-side** batch id (e.g., `"batch-smoke-01"`), not the Anthropic submission id (e.g., `"msgbatch_01..."`). The Anthropic id was never the right value — it doesn't match the schema pattern `^batch-[a-z0-9-]{1,48}$` and isn't what the aggregator joins on. The Anthropic id remains persisted separately in `runs/<runId>/batch-id.txt` for traceability. The duplicate-scenario reattachment branch also re-stamps `ParentBatchId` (a duplicate from a different Sonnet's batch must carry its own parent id, not the original's). |
| 8 | (Discovered during post-closure review.) The persisted `sonnet-NN/result.json` `tokensUsed` carried the model's self-reported placeholder values, diverging from the cost ledger and from Anthropic Console. | `SonnetDispatcher.RunAsync` validated the model's text and persisted the result without overwriting `tokensUsed` from the API response. (Cost ledger was correct because the ledger entry was built directly from `response.Usage`; only the persisted JSON file was stale.) | After schema validation, `result = result with { TokensUsed = ... }` populated from `response.Usage` before CoT persistence. |

**Verification.** Live-API smoke run on 2026-04-26 (run id `20260426-175125-run`) produced:

- Report Tier 3 table populated: scenario `sc-01`, outcome `ok`, 2/2 assertions, $0.0045 spend.
- Aggregate key metrics rendered (entitiesActive, systemsRun, ticksExecuted, worldObjectsPresent) — a section the aggregator was always able to produce but couldn't reach because the join failed.
- Cost ledger Haiku entry: input 97, cachedRead 34,605, output 414, $0.00454 — non-zero.
- Persisted `haiku-01/result.json` `parentBatchId` = `"batch-smoke-01-haiku"`, schema-conforming, joins correctly.
- Persisted `sonnet-01/result.json` `tokensUsed` = `{194, 0, 635, 34108}` — matches the cost ledger Sonnet entry exactly.
- Total spend $0.1426; mission outcome `ok`; exit 0.

**Files touched.** `Warden.Orchestrator/Batch/BatchScheduler.cs` (ParseSucceeded plus the `RunAsync` parent-batch-id map), `Warden.Orchestrator/Dispatcher/SonnetDispatcher.cs` (TokensUsed overwrite). No schema changes, no DTO changes, no test changes. Existing fixture-based BatchScheduler unit tests pass unchanged because the test harness happened to use a `parentBatchId` that already equalled the parent ScenarioBatch's `BatchId`.

**One pre-existing flake observed** (not introduced by this pass, unfixed): `Warden.Orchestrator.Tests.RunCommandEndToEndTests.AT01_MockRun_ExitsZeroAndWritesLedger` fails reproducibly with `IOException: file in use` on `cost-ledger.jsonl`. The failure happens during `CostLedger.AppendAsync` (plain `File.AppendAllTextAsync`, no locking) and pre-dates the post-closure pass — confirmed by stashing the patches and re-running the test. Likely cause: a Windows file-handle race in the mock-mode end-to-end harness when the test's `CostLedger` writer overlaps with the `ReportAggregator.ReadLedger` reader. Phase-2 backlog item below.

---

## 5. Cost model — actual vs. theoretical

The Phase 0 cost model (`02-cost-model.md`) projected $0.08–$0.15 for a smoke mission. Phase 1's actual measurements:

- **First call in a run (cache write):** $0.13–$0.14. Cache write at 1.25× input rate dominates. The 34KB cached prefix writes once per 5-minute window.
- **Subsequent calls in the same window (cache read):** $0.02. Cached prefix reads at 0.10× input rate — 90% savings as advertised.
- **Total smoke mission (1 Sonnet + 1 Haiku):** $0.02 if cache-warm; $0.14 if cache-cold. Console reflects exact spend.
- **Cast-validate failure case:** $0.02 (Sonnet read corpus, returned blocked, no Haiku batch dispatched).
- **Phase 1 total real-API spend across all testing:** approximately $0.40 across 6 runs; +$0.42 across 2 post-closure verification runs (2026-04-26).

The cost model is honest within ±20%. Set `--budget-usd 1.00` for any single smoke mission as a 5–7× safety ceiling. For the eventual cast-validate-fixed run with 5+ Haiku scenarios, $2.00 is appropriate.

The local cost ledger now matches Anthropic Console for both Sonnet and Haiku entries (resolved in §4.1, 2026-04-26).

---

## 6. Phase 2 backlog — explicit punch list

Ordered by dependency / value:

1. **Action-selection layer** (foundational, blocks behavior emergence). Per the action-gating bible: read drives + willpower + inhibitions + situation context, emit (1) movement targets, (2) dialog moments, (3) physiological action intents. Reads from the `WillpowerEventQueue` and writes to `MovementTargetComponent` + the dialog system's `PendingDialogQueue`. Approach-avoidance inversion at high stakes is the trickiest piece — needs careful test scaffolding.
2. **Schedule layer.** Per-NPC daily routine attached at spawn from archetype hints. "Donna at her desk 8am–10:30, breakroom 10:30–10:45, desk again, lunch outside 12–12:45..." Schedule emits movement targets that the action-selection layer can override under sufficient drive pressure. Probably implemented as a `ScheduleComponent` + `ScheduleSystem` that proposes targets which action-selection accepts or rejects.
3. **Memory recording on relationship entities.** The schema reserves `relationships[].historyEventIds[]`; the narrative event bus emits proximity-mediated candidates; nothing wires them together. A `MemoryRecordingSystem` listens to the narrative bus, decides per-pair memory persistence (lighter threshold than chronicle-global), writes to a per-relationship ring buffer.
4. **Cast-validate fix.** Pick inline mode or snapshot mode (§4). Snapshot mode is closer to the long-term "Sonnet validates a real projected world" goal and unblocks Phase 2 content-tuning workflows.
5. **Mock-mode end-to-end test flake.** `RunCommandEndToEndTests.AT01_MockRun_ExitsZeroAndWritesLedger` fails reproducibly with `IOException: file in use` on `cost-ledger.jsonl`. Pre-existing on `staging`, surfaced during the §4.1 verification. Likely a Windows file-handle race in `CostLedger.AppendAsync` (plain `File.AppendAllTextAsync`, no locking). Either add a lightweight mutex around the append, or switch to a single `FileStream` opened once with `FileShare.Read` and held for the run's lifetime. ~45 minutes of Sonnet work + an integration test that runs the mock pipeline 50 times in a loop.
6. **Stress system.** From `new-systems-ideas.md`. Cortisol-like accumulator; pushes events into `WillpowerEventQueue` when sustained; gates burnout at the chronic-stress level. The long-arc breakdown timeline the bibles promise.
7. **Workload system.** From `new-systems-ideas.md`. Tasks as entities, deadlines, NPC capacity, work-progress-modulated-by-physiology. The "orders come in, nobody told you" loop the world bible commits the gameplay to.
8. **Social mask system.** From `new-systems-ideas.md`. Difference between performed and felt emotion; mask cracks under willpower depletion. Visible to other NPCs.
9. **Per-pair memory differentiation in dialog.** The dialog bible flags the Affair archetype's code-switching (different fragments to different listeners) as a v0.1 deferral. Add per-listener fragment-use tracking when playtests show speaker-level differentiation reads as too uniform.
10. **Visualization layer.** The aesthetic bible's deferred 2.5D pixel-art-from-3D renderer. Substantial — probably its own multi-packet phase.
11. **Per-NPC name generation.** Cast generator currently produces unnamed NPCs; the world bible implies names like Donna, Frank, Greg. Small content packet.
12. **Engine fact sheet auto-regeneration.** Add a CI/build step that regenerates `docs/engine-fact-sheet.md` whenever `SimulationBootstrapper.cs` changes, so the cached corpus always reflects current engine state.

(Items 5 and 6 from the original Phase-1-closure backlog — Haiku token capture and report aggregator path — were resolved in §4.1.)

The PHASE-1-TEST-GUIDE.md §10 has these in operational terms — read both lists together.

---

## 7. Open questions in the bibles (Talon's calibration needed)

The bibles each have an "Open questions for revision" section. Phase 1 didn't resolve these; the next Opus should surface them at the right moments:

- **World bible:** what does "the company" actually do? Industry choice affects work-pressure flavor (software contractor vs widget manufacturing vs ad agency). Talon hadn't committed at Phase 1 close.
- **World bible:** seasonal/weekly cadence — is there a calendar that matters (Friday-feeling, end-of-quarter, holidays)?
- **Cast bible:** archetype layering cap — 2 or 3? Two reads cleaner; three may produce more interesting outliers.
- **Cast bible:** vocabulary register completeness — does anything Talon imagined not fit one of the six?
- **Action-gating bible:** willpower per-domain vs single-global — current commitment is single-global with inhibition class carrying domain. Revisit if behaviors feel undifferentiated.
- **Action-gating bible:** when does an event create a *new* inhibition vs strengthen an existing one? Threshold rules need definition during Phase 2's action-selection work.
- **Aesthetic bible:** building orientation fixed or parameterized (which windows face which direction)?
- **Aesthetic bible:** should NPCs have a separate `noticeability` from raw proximity (an NPC in a sunbeam is more noticed than the same NPC in shadow at the same distance)?
- **Dialog bible:** corpus scope at v0.1 — 200 enough? Tune based on playtests.
- **Dialog bible:** off-corpus action — when no fragment matches the filters, NPC stays silent (current). Revisit if silence reads as broken.

Don't volunteer answers; surface the question when the work depends on it.

---

## 8. Operational lessons learned (process, not code)

From running the Phase 1 dispatch flow with Talon:

- **Always commit packet files before creating a worktree.** Phase 1 hit this failure mode three separate times: write a packet, dispatch a Sonnet against it, Sonnet's worktree branched off a commit that didn't have the file (because it was uncommitted on the source branch). The Sonnet correctly reports `blocked: file not found` and stops. Fix is one `git add && git commit` step in the dispatch sequence; remind Talon explicitly when handing over each packet.
- **Worktree cleanup needs `--expire=now`.** Default `git worktree prune` respects a 3-month expiration window and won't remove orphaned metadata. Always use `git worktree prune --expire=now -v`.
- **Stale `.git/index.lock` files happen** under heavy worktree churn. `del .git\index.lock` is safe to run when no git operation is active.
- **The "merge to staging instead of feature branch" mistake** doesn't break things — staging just becomes the working mainline and the original branch (`ecs-p1-initial`) drifts behind. Talon hit this once; it self-resolved.
- **Real-API testing is non-optional.** Mock-mode tests passed every Phase 0 packet's AT suite, but six real-API bugs survived to Phase 1. Add real-API integration tests as part of any future packet that touches the orchestrator's API path. Cost is small ($0.02–$0.20 per run); the bug-discovery value is large.
- **Tier-aware role frames matter.** When two tiers share a cached prefix, they must have tier-distinct role frames or the smaller tier (Haiku) misinterprets the larger tier's (Sonnet) instructions. Generalize: any future tier (Opus dispatch path?) needs its own role frame.
- **Defensive parsers earn their keep.** Models occasionally drift from "raw JSON only" instructions even when the prompt is explicit. The `ExtractJsonObject` helper added to `SonnetDispatcher` and `BatchScheduler` rescues runs that would otherwise block. Apply the same pattern to any future cross-tier message parser.

---

## 9. Recommended first move for the next Opus

Read the SRD, this handoff, and at minimum the world + cast + action-gating bibles. Then propose **WP-2.1.A — Action-selection scaffold** as the first Phase 2 packet. Suggested shape:

- **Goal:** ship `ActionSelectionSystem` that reads each NPC's drive vector + willpower + inhibitions + current proximity context per tick and emits an `IntendedAction` (movement target, dialog moment, or physiological intent). Defer the *execution* of those intents to existing systems where possible (movement system already handles `MovementTargetComponent`; dialog system has `PendingDialogQueue`; physiology systems are already action-driven).
- **Non-goals:** complete behavior trees, per-archetype tuning, schedule integration. Phase 2.1.A is the scaffold; tuning and integration are 2.1.B+.
- **Acceptance:** an NPC with elevated `irritation` produces a `lashOut` dialog intent within N ticks; an NPC with elevated `attraction` toward an in-proximity NPC plus low `vulnerability` inhibition produces an approach intent; the same NPC with `vulnerability` strength 80 produces an avoidance intent (approach-avoidance inversion). Determinism: 5000-tick test produces byte-identical intent stream across two seeds.

Talon may push back on scope; iterate. The packet's value is establishing the seam between social state and observable behavior — once that exists, everything from schedules to stress to social mask becomes incremental work.

---

## 10. What you don't have to worry about

Things Phase 1 settled that the next Opus should treat as load-bearing:

- The schema versioning regime works. v0.5 spatial was promoted to v0.3 cleanly; chronicle slid to v0.4 cleanly; future minor bumps follow the additive-compatibility rule.
- The orchestrator's real-API path is healthy. The fixes from §4 are committed and the smoke mission proves end-to-end function.
- The cached prefix is sized appropriately (~34KB). Fits in Anthropic's prompt cache; produces 90% read discount on the 2nd+ call within 5 minutes.
- The fail-closed semantics are real. No worker retries; no agent spawns helpers; every path returns a structured outcome the orchestrator handles.
- Determinism holds across all engine systems with seeded RNG. Save/load and replay both work on the same `WorldStateDto` shape.
- The bibles are stable. Don't volunteer revisions unless the bibles force one (Phase 1 forced exactly one — the spatial v0.5 → v0.3 promotion).

---

## 11. Goodbye from Phase 1 Opus

The work was good. The bugs found in real-API testing are the kind that only emerge from end-to-end runs against the live system; Phase 0 couldn't have caught them, and Phase 2 can take the now-healthy orchestrator as a given rather than a hypothesis. The bibles are richer and the engine substrate is more complete than the original kickoff brief promised.

Two pieces of meta-advice:

- **Trust Talon's design instincts.** The pair-drive correction was a real architectural improvement. The "no runtime LLM" pushback closed a path that would have hurt the game. The willpower/inhibition framing came from him; the dialog mechanism came from his "decision tree for emotions" hint. He sees this clearly.
- **Be tight with non-goals.** Every Phase 1 packet that landed cleanly had a tight non-goals list. Every packet that needed re-dispatch had a non-goal that should have been there but wasn't. The non-goals section is the highest-leverage sentence-per-token in the WP format.

Phase 2 begins now. Good luck.
