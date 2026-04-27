# Phase 2 Handoff Summary

> Authored by the Phase 2 Opus at closure. The comprehensive reference for the next Opus picking up Phase 3. Read after `docs/PHASE-3-KICKOFF-BRIEF.md`. Companion to `PHASE-2-TEST-GUIDE.md` (operational verification).
>
> Mirrors the shape of `PHASE-1-HANDOFF.md`; this is the audit trail.

---

## 1. Phase 2 in one paragraph

Phase 2 took the alive-feeling foundation Phase 1 built and turned it into observable gameplay. Fourteen Sonnet packets shipped covering the action-selection seam (the layer that turns drives + willpower + inhibitions into intents), schedule layer (per-archetype daily routines), memory recording on relationship entities, stress accumulation with a closed-loop suppression amplifier, social mask cracking under sustained pressure, workload + tasks (the world bible's "orders come in, nobody told you" loop), physiology-overridable-by-inhibition (Sally-with-bodyImageEating-90-doesn't-eat), per-NPC name generation, dialog per-listener tracking (the Affair archetype's code-switching), the engine fact-sheet staleness CI check, and three orchestrator fixes (cast-validate inline mode, CostLedger Windows-race fix, BatchScheduler cross-spec scenario-id collision). The post-closure pass on the Phase 1 orchestrator landed in the kickoff window — Haiku token-capture, Sonnet `tokensUsed` divergence, and the report aggregator's missing Tier 3 table all resolved by stamping `ParentBatchId` and `TokensUsed` from authoritative API response data. SRD §8.7 was added formalising the engine-host-agnostic / WARDEN-vs-RETAIL build split — the architectural commitment that lets Phase 3's Unity host coexist with the dev-time JSONL stream agents read. **Phase 3 starts with death.** The first 3.0.x packets land cause-of-death events, deceased-entity handling, and the choking-on-food / slip-and-fall / locked-in-and-starved emergent scenarios *before* any Unity scaffolding begins, because the visualizer needs to know what "dead" looks like before it can render it.

---

## 2. What shipped — by axis

### 2.1 Engine systems added (in `APIFramework`)

In tick-phase order:

- **PreUpdate phase:** `StressInitializerSystem`, `MaskInitializerSystem`, `WorkloadInitializerSystem`, `TaskGeneratorSystem`, `ScheduleSpawnerSystem`. Spawn-time initializers attach Phase 2 components from per-archetype JSON catalogs.
- **Condition phase:** `ScheduleSystem`. Reads `SimulationClock.GameHour`, writes `CurrentScheduleBlockComponent` from the NPC's `ScheduleComponent` blocks.
- **Cognition phase (= 30, new):** `ActionSelectionSystem`, `PhysiologyGateSystem`. The cognitive seam between social state and observable behaviour.
- **Cleanup phase:** `StressSystem`, `WorkloadSystem`, `MaskCrackSystem`. Late-tick consumers and producers — stress reads suppression events from the willpower queue, workload advances task progress, mask-crack overrides ActionSelection's intent when willpower is depleted under exposure.
- **Off-phase event subscriber:** `MemoryRecordingSystem`. Subscribes to `NarrativeEventBus.OnCandidateEmitted`; routes pair-scoped events to relationship entities, solo events to per-NPC personal logs.

Total: 11 new systems registered in `SimulationBootstrapper`. The engine fact sheet (`docs/engine-fact-sheet.md`, regenerable via `ECSCli ai describe`, now CI-checked by `FactSheetStalenessTests`) lists 48 systems and ~147 component types.

### 2.2 Engine components added

- **Action / cognition:** `IntendedActionComponent` (`Kind`, `TargetEntityId`, `Context: DialogContextValue`, `IntensityHint`); `BlockedActionsComponent` (per-NPC veto set: `Eat | Sleep | Urinate | Defecate`).
- **Schedule:** `ScheduleComponent` (per-day blocks); `CurrentScheduleBlockComponent` (resolved each tick).
- **Memory:** `MemoryEntry` record; `RelationshipMemoryComponent` (per-pair ring buffer, default 32); `PersonalMemoryComponent` (per-NPC ring buffer, default 16).
- **Stress:** `StressComponent` (Acute, Chronic, source counters, last-burnout-day); tags `StressedTag`, `OverwhelmedTag`, `BurningOutTag`.
- **Mask:** `SocialMaskComponent` (per-drive mask deltas, current load, baseline, last-slip-tick).
- **Workload:** `TaskComponent` (Effort, Deadline, Priority, Progress, QualityLevel, AssignedNpcId, CreatedTick); `WorkloadComponent` (active tasks, capacity, current load); tags `TaskTag`, `OverdueTag`, `BurnedOutFromWorkloadTag`.

### 2.3 Narrative event kinds added

- `MaskSlip` (WP-2.5.A) — written to per-pair memory of speaker × each observer. Persistent: true.
- `OverdueTask` (WP-2.6.A) — written to assignee's personal memory. Persistent: true.
- `TaskCompleted` (WP-2.6.A) — written to assignee's personal memory. Persistent: false (routine completion).

### 2.4 SimConfig sections added

`actionSelection`, `stress`, `schedule`, `memory`, `socialMask`, `workload`, `physiologyGate`. Each documented in `SimConfig.json` with inline comments.

### 2.5 Content artifacts added

Under `docs/c2-content/`:

- `archetypes/archetype-stress-baselines.json` — 10 archetypes with chronic-stress baselines (Cynic 20, Vent 50, Recovering 60, etc.).
- `archetypes/archetype-workload-capacity.json` — 10 archetypes with task capacity (Climber 5, Founder's Nephew 1, etc.).
- `schedules/archetype-schedules.json` — 10 archetypes with 4–7 ScheduleBlocks each, 24h coverage with end-of-day wrap, anchors verified against `office-starter.json`.
- `cast/name-pool.json` — 50 first names for the cast generator (early-2000s American office demographic, includes the bible exemplars Donna, Greg, Frank).
- `dialog/corpus-starter.json` (extended) — ~12 new mask-slip fragments tagged `context: maskSlip`, `noteworthiness ≥ 70`, register-spread.

### 2.6 SRD additions

**Axiom 8.7 — The engine is host-agnostic; telemetry is build-conditional.** Formalises:

- `APIFramework` is a class library with three legitimate hosts: `ECSCli`, Unity (Phase 3+), and test harnesses.
- The engine has no host-specific code. `WorldStateDto` projection is the universal observation surface.
- WARDEN build configuration: `Warden.Telemetry`, `Warden.Anthropic`, `Warden.Orchestrator` referenced; AI agents continue to read JSONL streams during dev.
- RETAIL build configuration: every `Warden.*` reference `#if WARDEN` gated and stripped at ship.
- "You don't come shipped with the game" — the dev-time observability and AI tooling are permanent for development; none survives to the player.

This is the axiom that lets Phase 3's Unity host coexist with the entire Phase 0–2 dev infrastructure.

### 2.7 Orchestrator fixes (post-Phase-1-closure pass, landed in Phase 2 kickoff)

Three bugs the original Phase 1 closure flagged as Phase 2 backlog were resolved in the kickoff window before any Phase 2 packets dispatched. The original handoff documented them as separate bugs; the actual root cause was a single missing line:

- `BatchScheduler.ParseSucceeded` never stamped the parsed `HaikuResult` with authoritative server-side values. The role frame instructs the model to placeholder `parentBatchId: "batch-pending"` and `tokensUsed: {0,0,0,0}`, expecting the orchestrator to overwrite. It never did.
- `SonnetDispatcher.RunAsync` had the symmetric bug on the Sonnet path — persisted `result.json` carried the model's self-reported `tokensUsed` while the cost ledger had the correct values from `response.Usage`.

The fix: stamp `ParentBatchId` (with the *Sonnet-side* `scenarioBatch.batchId`, not the Anthropic `msgbatch_...` id which doesn't match the schema pattern `^batch-[a-z0-9-]{1,48}$`) and `TokensUsed` (from `succeeded.Message.Usage` / `response.Usage`) on the parsed result before persisting. Two files, ~10 lines added across both. Closed three "open backlog items" with one root-cause fix.

The cost ledger now matches Anthropic Console for both Sonnet and Haiku entries. The report aggregator's Tier 3 table populates correctly. PHASE-1-HANDOFF.md was updated with §4.1 documenting the post-closure pass for the audit trail.

---

## 3. Architectural decisions — Phase 2's discoveries

### 3.1 Action-selection writes intent; downstream systems consume

`ActionSelectionSystem` is the *single writer* of `IntendedActionComponent`. `DialogContextDecisionSystem` reads `Context` when `Kind == Dialog`; movement systems read `TargetEntityId` when `Kind == Approach | Avoid`; physiology systems run autonomously but check `BlockedActionsComponent` before triggering. This single-writer pattern is what keeps determinism honest — the 5000-tick byte-identical-intent test (WP-2.1.A AT-10) is the contract.

The mask-crack override at Cleanup phase is the *one* exception: `MaskCrackSystem` overwrites `IntendedAction` after `ActionSelectionSystem` ran. The race is intentional — the crack is unintentional behaviour overriding the NPC's planned action. Documented in WP-2.5.A's Design notes; preserved in the codebase.

### 3.2 Multi-source candidate enumeration

`ActionSelectionSystem`'s candidate list is fed by multiple sources, layered:

- **Drive-driven** (WP-2.1.A): irritation → LashOut/Complain dialog; attraction → Approach (or Avoid under high inhibition); etc.
- **Schedule-driven** (WP-2.2.A): when an NPC has `CurrentScheduleBlockComponent.AnchorEntityId != Empty`, add an Approach candidate toward the anchor with `ScheduleAnchorBaseWeight = 0.30`.
- **Workload-driven** (WP-2.6.A): when the NPC has active tasks AND schedule is `AtDesk`, add a `Work` candidate toward the highest-priority task with `WorkActionBaseWeight = 0.40`.
- **Idle fallback**: always present, low weight.

Drive-driven candidates outrank schedule and workload most of the time when drives are elevated; quiet-drive periods produce schedule/work behaviour. This is the design intent — work happens when drives don't.

### 3.3 The closed willpower-stress loop

WP-1.4.A reserved the `WillpowerEventQueue` as a producer/consumer seam without a producer. WP-2.1.A's action-selection became the producer (suppression events). WP-2.4.A's stress system became a *second* producer (amplification events when `AcuteLevel ≥ stressedTagThreshold`). The loop:

> ActionSelection suppresses a high-drive action → SuppressionTick event → willpower depletes → stress accumulates → at `AcuteLevel ≥ 60`, stress pushes additional SuppressionTick events → willpower depletes faster → eventually willpower runs low → existing willpower-leakage in ActionSelection lets the suppressed action break through.

The mask system reads stress + willpower for crack pressure; the crack reset releases pressure on the dominant masked drive. The full breakdown timeline the bibles promised is now mechanised.

### 3.4 Memory has a per-pair primary, per-NPC secondary, chronicle global

The v0.4 schema reserved three memory surfaces; Phase 2 populated all three:

- **Per-pair**: `RelationshipMemoryComponent` on relationship entities. Routed from 2-participant narrative candidates.
- **Per-NPC personal**: `PersonalMemoryComponent` on each NPC. Routed from 1-participant or 3+-participant narrative candidates.
- **Global chronicle**: `Stain` / `BrokenItem` entities. Already populated by WP-1.9.A's `PersistenceThresholdDetector`; unchanged in Phase 2.

The `Persistent` flag on `MemoryEntry` distinguishes lasting from ephemeral (per the bible's per-pair threshold being lighter than chronicle-global). The projector serializes only persistent entries to `relationships[].historyEventIds[]`; the full set lives engine-side.

### 3.5 The composite-key BatchScheduler refactor (WP-2.0.C)

PHASE-1-HANDOFF §4.1 introduced `parentBatchIdFor` keyed by `ScenarioId`. WP-2.0.C discovered this collides cross-spec: when two specs both produce `sc-01..sc-05`, the dictionary throws. Fix: composite key `(BatchId, ScenarioId)` throughout `BatchScheduler.RunAsync`, with Anthropic `custom_id` encoded as `<batchId>::<scenarioId>` (64-char limit enforced). The redundant `parentBatchIdFor` map was eliminated since `key.BatchId` carries the information directly.

This bug pre-dated the post-closure pass and was masked by the IOException flake (WP-2.0.B). Once that flake fixed, the collision surfaced. Both bugs shipped in the same dispatch wave.

---

## 4. The orchestrator real-API path — bugs found and fixed

| # | Bug | Fix | Wave |
|:---|:---|:---|:---|
| 1 | Cost ledger Haiku entries showed 0 tokens; `ParentBatchId` placeholder not overwritten; report aggregator's Tier 3 table empty | `BatchScheduler.ParseSucceeded` stamps `TokensUsed` from `succeeded.Message.Usage`; `RunAsync` builds `parentBatchIdFor` map from input ScenarioBatches and stamps `ParentBatchId` with the Sonnet-side batch id | Kickoff (post-Phase-1) |
| 2 | Sonnet `result.json` `tokensUsed` diverged from cost ledger | `SonnetDispatcher.RunAsync` overwrites `result.TokensUsed` from `response.Usage` after schema validation, before CoT persistence | Kickoff (post-Phase-1) |
| 3 | Cast-validate spec referenced files Sonnets-via-API couldn't read | `InlineReferenceFiles.Build` pre-reads files in `spec.Inputs.ReferenceFiles[]`, prepends as `--- BEGIN <path> ---` / `--- END <path> ---` block to the user turn; 100KB single-file / 200KB aggregate cap; `..` traversal rejected | WP-2.0.A |
| 4 | `RunCommandEndToEndTests.AT01_MockRun_*` flaked with `IOException: file in use` on `cost-ledger.jsonl` | `SemaphoreSlim(1, 1)` around `CostLedger.AppendAsync`; surfaces a previously-masked `BatchScheduler` collision (fixed by WP-2.0.C) | WP-2.0.B |
| 5 | `BatchScheduler.RunAsync` threw `ArgumentException` when two specs both produced scenario IDs `sc-01..sc-05` | Composite key `(BatchId, ScenarioId)` throughout; Anthropic `custom_id` = `<batchId>::<scenarioId>`; 64-char limit validated | WP-2.0.C |

The orchestrator's API path is now healthy end-to-end. No known bugs remain; the live-API smoke mission produces a fully-populated report with non-zero ledger entries on both tiers.

---

## 5. Cost model — actual vs. theoretical

Phase 1 measurements (cache write $0.13–$0.14, cached read $0.02 per call, smoke mission $0.02 cache-warm or $0.14 cache-cold) held throughout Phase 2. Approximate spend totals:

- **Phase 2 packet dispatches** (14 Sonnets): ~$3.50 across all worktree dispatches.
- **Real-API verification runs**: ~$0.84 across 4 runs (2 in kickoff, 1 cast-validate after WP-2.0.A, 1 final smoke after Wave 4).
- **Phase 2 grand total**: ~$4.34 in API spend.

Set `--budget-usd 1.00` for any single smoke mission; `--budget-usd 2.00` for cast-validate or any multi-Haiku-scenario mission.

The cost ledger is now ground truth; cross-checking Anthropic Console is no longer necessary for accounting.

---

## 6. Phase 3 backlog — the punch list

Ordered by dependency / value. Phase 3 is the visualization phase; the engine work below comes *first* so Unity has a death-aware, mutation-clean, performance-ready engine to render.

### 6.0 Phase 3.0.x — engine work before Unity

1. **WP-3.0.0 — `LifeStateComponent` + cause-of-death events**. New component (`Alive | Incapacitated | Deceased`). Every existing system gains a "skip if not Alive" early-return. New `NarrativeEventKind` values: `Choked`, `SlippedAndFell`, `StarvedAlone`, `Died` (general). Engine-internal at v0.1; wire-format surface added at the next schema bump.
2. **WP-3.0.1 — Choking-on-food scenario**. Concrete first death scenario. Eating-failure path in the digestion pipeline: bolus too big + low energy + no proximity-help = choke timeout → `Choked` event → `LifeState = Deceased`. Witnesses get a major memory + stress spike. Tests every existing system at once; canonical first death.
3. **WP-3.0.2 — Deceased-entity handling**. New `CorpseComponent` on the deceased's entity. Body persists until removed by another NPC or the player. Cubicle stays empty; chronicle records the loss. Relationship-state shift on bereavement: every NPC who knew the deceased gets a major persistent memory + +20 stress.
4. **WP-3.0.3 — Slip-and-fall + locked-in-and-starved scenarios**. Existing Stain entities (WP-1.9.A) gain a fall risk; locked door + no-exit + hunger-100 triggers `StarvedAlone`. Both reuse 3.0.0's death machinery.
5. **WP-3.0.4 — Live-mutation hardening**. Engine "topology dirty" signal: when an entity's `PositionComponent`, `RoomMembershipComponent`, or any structural component changes, pathfinding caches invalidate and dependent systems pick up the new state on the next tick. Required for Unity's drag-and-drop building mechanic.
6. **WP-3.0.5 — `ComponentStore<T>` typed-array refactor**. The known boxing issue documented in `Entity.cs` (Dictionary<Type, object>) becomes a frame-rate concern at 30+ NPCs in Unity. Refactor to typed component arrays with O(1) `Get<T>`/`Has<T>` semantics. This is the single most important engine performance packet for Phase 3.

### 6.1 Phase 3.1+ — Unity scaffolding

Begins after 3.0.x lands. The aesthetic bible's "deferred until simulation is rich enough to be worth looking at" milestone has been reached. Expected packet shape (subject to the next Opus revising):

- 3.1.A — Unity project scaffold; loads `APIFramework.dll`; basic camera (pan/zoom/follow); reads `WorldStateDto` from in-process engine; renders rooms as flat colored rectangles, NPCs as dots. The "30 dots that move at 60 FPS" baseline.
- 3.1.B — Per-NPC silhouette renderer (the silhouette catalog from the cast bible: height / build / hair / headwear / distinctive item / dominant color). Layered animator skeleton.
- 3.1.C — Lighting visualization. Engine has illumination state per room (WP-1.2.A); render it.
- 3.1.D — Building mode mechanic. Drag/drop placement; door/wall pickup; the topology-dirty signal from 3.0.4 makes this real-time-safe.
- 3.1.E — Player UI — selection, inspection (drives, mood, schedule, current task — debug-grade), time controls, notification surface.
- 3.1.F — JSONL stream wiring. Background-thread emission of `WorldStateDto` per N ticks to a configurable file; agents continue to consume.
- 3.1.G — Player-facing event log (CDDA-style chronicle reader).
- 3.1.H — Developer console (Minecraft-style; WARDEN-only).

### 6.2 Other Phase 3 commitments

- **UX/UI bible** — Talon is authoring. The next Opus reviews and folds into Phase 3.1.E's design.
- **Art pipeline** — Talon needs a 3D modeler/animator collaborator. Outside the Opus/Sonnet workflow.
- **Save/load round-trip hardening** — important but lower priority than live mutation. WP-3.2.x range.

---

## 7. Open questions for Talon (carried forward)

From the bibles' "Open questions for revision" sections, plus new questions raised during Phase 2:

- **World bible:** what does "the company" actually do? Industry, weekly cadence, calendar-mattering events.
- **Cast bible:** archetype layering cap (2 vs 3); vocabulary register completeness; mask system as generalisation (now partly answered by WP-2.5.A).
- **Action-gating bible:** willpower per-domain vs single-global; inhibition install during play (when does an event create new vs strengthen existing); awareness transitions (hidden → known); conflict between inhibitions; player visibility of inhibition values.
- **Aesthetic bible:** building orientation fixed or parameterised; NPC noticeability separate from raw proximity.
- **Dialog bible:** corpus scope at v0.1 (200 enough, or grow); off-corpus action when no fragment matches; mask-slip dialog calcify behaviour; space-contingent register shifting.
- **New (Phase 2):** `Persistent` mapping nuance — should magnitude/intensity affect the flag (a mild MaskSlip vs a full mask break)? Decay-of-persistence over game-time? Per-archetype persistence biasing (the Cynic genuinely doesn't remember most things)?
- **New (Phase 3):** animation hint surface — engine-emit (`IntendedAnimationHint`) or Unity-derive? Building mode toggle vs always-on? In-game console split — player-event-log vs dev-debug-console as one feature or two?

The next Opus surfaces these when the work depends on them. Don't volunteer answers; ask Talon at the right moment.

---

## 8. Operational lessons learned (process, not code)

From running Phase 2's 14-packet flow:

- **The shared-file conflict pattern is predictable and cheap.** Every wave that touches `SimulationBootstrapper.cs`, `SimConfig.cs/json`, or one of the additive enums (`NarrativeEventKind`, `IntendedActionKind`, `DialogContextValue`) will conflict at merge time. Resolution is always "keep both" — 30 seconds of work per file. Don't try to design around it; just accept it as a per-wave merge tax.
- **The "DO NOT DISPATCH UNTIL X" hint needs to be in the foreground.** WP-2.3.B blocked correctly when dispatched without WP-2.5.A and WP-2.6.A merged — fail-closed semantics worked, no money wasted. But the dependency was buried in side-channels (a comment in the worktree command, a section label on the prompt). For Phase 3, packets with hard wave dependencies should put a "DO NOT DISPATCH UNTIL X IS MERGED" line at the top of the packet itself.
- **Sonnet judgement calls have been good.** When a Sonnet hits an out-of-scope problem (WP-2.0.B exposing the BatchScheduler collision; WP-2.3.B's enum dependency), the structured `blocked` outcome with a clean diagnosis is more valuable than an attempted workaround. The fail-closed protocol kept costs honest.
- **The post-closure pass is cheap when the diagnosis is right.** PHASE-1-HANDOFF flagged three "separate" backlog items that were one root-cause fix — finding that took longer than fixing it. Worth doing the diagnostic walkthrough before scoping the fix packet.
- **Wave 4 packets all parallel-dispatched cleanly.** WP-2.7.A, WP-2.8.A, WP-2.9.A, WP-2.3.B touched fully disjoint files (no `SimulationBootstrapper` modifications); near-zero merge conflicts. Smaller, more disjoint packets = faster wrap. Pattern worth keeping for Phase 3.
- **Real-API smoke after each major merge catches silent breakage.** Doing this cheaply ($0.13 per cache-cold smoke) confirms the dispatch flow stays healthy. Phase 2 didn't need it most weeks but the discipline is right.

---

## 9. Recommended first move for the next Opus

Read this handoff, the SRD (especially §8 — including the new §8.7), the bibles, and the `PHASE-3-KICKOFF-BRIEF.md`. Then **draft `WP-3.0.0 — LifeStateComponent + cause-of-death events`** as the first Phase 3 packet. Suggested shape:

- **Goal:** ship `LifeStateComponent { LifeState Alive | Incapacitated | Deceased }` on every NPC; add `CauseOfDeathComponent` populated when state transitions to Deceased; add four new `NarrativeEventKind` values (`Choked`, `SlippedAndFell`, `StarvedAlone`, `Died`); add a "skip if not Alive" guard pattern that every social/physiology/action system uses.
- **Non-goals:** the choking scenario itself (3.0.1), corpse handling (3.0.2), bereavement integration (3.0.2), wire-format surface (deferred to next schema bump).
- **Acceptance:** an NPC manually forced to `Deceased` no longer ticks — drives stop drifting, willpower stops regenerating, action-selection skips them. Memory recording continues to reference the deceased's entity id correctly. Determinism: 5000-tick test with one NPC dying mid-run produces byte-identical state across two seeds.

Then 3.0.1, 3.0.2, etc. The first three packets together produce the "Mark choked on a sandwich at his desk; nobody noticed for an hour; cubicle 12 is now empty and the office is quieter for a week" narrative arc. Once that works, Unity work begins.

---

## 10. What you don't have to worry about

Things Phase 2 settled that the next Opus should treat as load-bearing:

- The orchestrator's API path is healthy. No known bugs. Ledger matches Console. Reports populate fully.
- The action-selection seam works. 5000-tick determinism holds across all consumers.
- The willpower-stress-mask loop is closed. Sustained suppression produces eventual breakthrough behaviour as designed.
- Per-pair memory + personal memory + chronicle are all populated correctly. The v0.4 schema's reserved surfaces are no longer empty.
- The schema versioning regime (v0.4) is stable. No bumps were required during Phase 2; additive component data lived in engine-internal fields. The next bump is for Phase 3 (likely `LifeState` surface).
- The fail-closed semantics are real and tested. WP-2.3.B's correct block-when-dispatched-too-early proves the protocol holds under adverse conditions.
- The bibles are stable. Phase 2 added one (the open-questions doc was extended; new-systems-ideas was consumed by Wave 2/3 packets but kept as the source-of-truth doc); no bibles were structurally rewritten.
- The build configuration split (axiom 8.7) is documented. Phase 3 packets reference it; no axiom-level work needed.

---

## 11. Goodbye from Phase 2 Opus

The work was good. Fourteen packets shipped in roughly the same wall-clock as Phase 1's sixteen, with cleaner failure modes (every block was meaningful, every fix was surgical). The mid-conversation post-closure pass on Phase 1's orchestrator bugs was the highest-leverage thing I did this phase — three "open" items closed by one root-cause fix, ledger now honest, reports complete. The two-world axiom (8.7) is what makes Phase 3's Unity host coexist with the dev-time observability instead of replacing it; locking that in before Phase 3 starts means no packet drifts into runtime AI dependencies by accident.

Two pieces of meta-advice for the next Opus:

- **Death-from-beginning is non-negotiable per Talon.** The choking-on-food scenario is the canonical test of the engine's emergent gameplay potential. Get 3.0.0–3.0.2 right and Phase 3 has its proof of concept; rush them and Unity work starts on a foundation that can't show what makes the simulation special.
- **Talon authors the UX/UI bible during the engine work.** Don't pre-empt. When the draft lands, critique it the way Phase 1 Opus critiqued the cast bible — surface what's load-bearing for the engine before committing the engine work that builds on it.

Phase 3 begins now. Good luck.
