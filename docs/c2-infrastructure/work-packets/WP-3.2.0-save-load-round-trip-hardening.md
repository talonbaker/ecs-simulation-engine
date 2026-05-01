# WP-3.2.0 — Save/Load Round-Trip Hardening

> **DO NOT DISPATCH UNTIL ALL OF PHASE 3.1.x IS MERGED.**
> This packet hardens the `WorldStateDto` serialization contract that every Phase 0/1/2/3.x component participates in. Phase 3.1.E wired the save/load UI; this packet stresses what 3.1.E sits on top of.

**Tier:** Sonnet
**Depends on:** All Phase 0/1/2/3.0.x/3.1.x packets merged
**Parallel-safe with:** WP-3.2.1 (sound bus — disjoint surface)
**Timebox:** 130 minutes
**Budget:** $0.55

---

## Goal

The engine has supported `WorldStateDto` JSON serialization since SRD §8.2. Each phase has added components, and each phase has assumed serialization works because the projector serializes whatever the engine emits. This is mostly true — but a save during a mid-choke event, or a load with a missing optional component, or a round-trip across a v0.4 → v0.5 schema bump exposes edges. This packet hardens those edges.

After this packet:

- A save taken at any tick produces a `WorldStateDto` JSON that, when loaded fresh, restores the engine to a state byte-identical (or to within float-precision tolerance) to the saved state.
- Round-trip preserves: every NPC's full component set, every relationship row, every chronicle entry, every persistent memory, every task, every active mask state, every in-flight choke / faint / lockout, every corpse, every locked door, every spawned stain with its fall-risk.
- Edge cases handled: save during incapacitation budget mid-countdown (the budget resumes correctly); save mid-conversation (dialog history preserved); save during build mode (intent persists through load); save with deceased NPCs that still appear in relationship rows.
- Schema-version mismatch: a v0.4-format save loaded by a v0.5 engine receives default values for new fields; a v0.5-format save loaded by a v0.4 engine fails-closed with a clear error message. No silent data loss.

This packet does **not** add new player-facing UI (3.1.E shipped that). It hardens the substrate.

---

## Reference files

- `docs/c2-infrastructure/00-SRD.md` — §8.2 (save/load reuses WorldStateDto), §4.2 (determinism).
- `docs/c2-infrastructure/SCHEMA-ROADMAP.md` — current schema version.
- `docs/c2-infrastructure/work-packets/_completed/*` — every prior WP that added a component (WP-1.4.A, WP-1.7.A, WP-1.9.A, WP-2.3.A, WP-2.5.A, WP-2.6.A, WP-3.0.0, WP-3.0.2, WP-3.0.3) for component shape.
- `Warden.Telemetry/Projectors/*` — projection logic; entry point for save.
- `APIFramework/Core/SimulationBootstrapper.cs` — boot-from-WorldStateDto path; entry point for load.
- `docs/c2-content/ux-ui-bible.md` §3.4 — save/load UX commitments.
- `docs/c2-infrastructure/work-packets/_completed/WP-3.1.E.md` — UI implementation; this packet serves it.

---

## Non-goals

- Do **not** introduce a new save format. JSON only, per SRD §8.2.
- Do **not** change UI surfaces. UI is 3.1.E.
- Do **not** add cross-save migration tooling (e.g., load-from-v0.3-format). v0.4 → v0.5 is the only version delta.
- Do **not** introduce a binary save format for size optimization.
- Do **not** add network save sync.
- Do **not** modify the projector's wire format.
- Do **not** retry, recurse, or self-heal.

---

## Design notes

### Round-trip determinism contract

For a state `S` at tick `T`:

1. `S' = Project(S)` — a `WorldStateDto`.
2. `J = Serialize(S')` — a JSON string.
3. `S'' = Deserialize(J)` — back to `WorldStateDto`.
4. `S''' = Bootstrap(S'')` — engine state restored.
5. **Assertion:** Tick `S` and `S'''` for `N` ticks; their projection sequences must be byte-identical.

The packet's central test verifies this contract for `N=1000` ticks across many save points.

### Save-during-X edge cases

Each of these states needs explicit test coverage:

- **Mid-conversation.** Two NPCs in dialog; save mid-exchange. Load. Conversation continues from where it left off.
- **Mid-choke.** NPC has `IsChokingTag` and `LifeStateComponent.State == Incapacitated` with non-zero `IncapacitatedTickBudget`. Save. Load. Budget continues to decrement; eventual transition to Deceased fires correctly.
- **Mid-faint.** Similar to choke but recovery path.
- **Mid-build-mode.** Player has a structural item drag-staged. Save (autosave-on-build is a UX commitment). Load. Drag state cleared (build mode is per-session UI); engine state preserved.
- **Mid-bereavement-cascade.** Death event just fired; not all witnesses processed yet. Save. Load. Cascade resumes correctly.
- **Locked door.** Save with one door bearing `LockedTag`. Load. Pathfinding cache rebuilds with the lock; NPCs respect it.
- **Spawned stains with FallRisk.** Save. Load. Stain entities preserved with their `FallRiskComponent`.
- **Active corpses.** Save with `CorpseTag` entities. Load. Corpses preserved at their `LocationRoomId`.
- **Mid-task.** NPC working on a task with progress 0.5. Save. Load. Task progress continues.
- **Overdue tasks.** Save with `OverdueTag` tasks. Load. Stress source counters preserved.

### Optional component handling

The `WorldStateDto` schema treats some components as optional. The packet ensures:

- **Missing optional component** in a saved DTO → loaded entity does not have the component.
- **Present optional component** in a saved DTO → loaded entity has the component with saved values.
- **Schema unknowns** → fail-closed with clear error message: `"Schema version mismatch: save claims v0.5, engine supports v0.4. Cannot load."`

### Float-precision tolerance

JSON round-trip uses `System.Text.Json`. Round-trip is bit-for-bit identical for `double` but `float` may have rounding edges. Assert `Math.Abs(a - b) < 1e-9` for float comparisons; document in test fixtures.

### Schema-version handling

The DTO carries a top-level `schemaVersion` string. Loader logic:

```csharp
public WorldStateDto Load(string json)
{
    var dto = JsonSerializer.Deserialize<WorldStateDto>(json, _options);
    if (dto.SchemaVersion == null)
        throw new SaveLoadException("Save file missing schemaVersion field.");
    if (dto.SchemaVersion == EngineSchemaVersion.Current) return dto;
    if (CanMigrate(dto.SchemaVersion, EngineSchemaVersion.Current))
        return Migrate(dto, EngineSchemaVersion.Current);
    throw new SaveLoadException($"Save schema version {dto.SchemaVersion} not loadable by engine schema version {EngineSchemaVersion.Current}.");
}
```

`Migrate` handles v0.4 → v0.5. Higher version saves fail-closed for lower-version engines.

### Test matrix

Round-trip tests for every component family: Identity, Spatial (Position, RoomMembership, MovementTarget, Path, BoundsRect), Lighting (RoomIllumination, LightAperture, LightSource, SunStateRecord), Physiology (Energy, Metabolism, Bladder, Stomach, Colon, LargeIntestine, SmallIntestine, EsophagusTransit, Bolus), Social (SocialDrives, Willpower, Inhibitions, Relationship, RelationshipMemory, PersonalMemory), Cognition (IntendedAction, BlockedActions, Mood, Schedule, CurrentScheduleBlock), Workload (Task, Workload), Stress/Mask (Stress, SocialMask), Life-state (LifeState, CauseOfDeath, Corpse, Choking, LockedIn, BereavementHistory), Tags (all `*Tag` markers), Hazards (Stain with FallRisk), Spatial structures (walls, doors, named anchors), Chronicle (ChronicleEntry), Dialog (DialogHistory).

### Tests

- `WorldStateDtoSerializationTests.cs` — round-trip empty world, office-starter, and after 1000 ticks of activity.
- `EveryComponentRoundTripTests.cs` — for each component, save → load → assert preservation.
- `SaveMidConversationTests.cs`, `SaveMidChokeTests.cs`, `SaveMidFaintTests.cs`, `SaveMidBuildModeTests.cs`, `SaveMidBereavementCascadeTests.cs`, `SaveWithLockedDoorTests.cs`, `SaveWithStainsAndFallRiskTests.cs`, `SaveWithCorpsesTests.cs`, `SaveWithAllNpcsDeceasedTests.cs`, `SaveWithOverdueTasksTests.cs`, `SaveWithMidTaskProgressTests.cs`.
- `LoadOlderSchemaTests.cs`, `LoadNewerSchemaFailClosedTests.cs`, `LoadMissingSchemaVersionTests.cs`.
- `SaveLoad1000TickRoundTripTests.cs`, `SaveLoadDeterminismAcrossSeedsTests.cs`.
- `SaveSerializationPerformanceTests.cs` — full office-starter at 30 NPCs serializes ≤ 50ms.
- `LoadDeserializationPerformanceTests.cs` — full office-starter JSON loads ≤ 100ms.
- `FloatPrecisionToleranceTests.cs` — round-trip floats within 1e-9.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `Warden.Telemetry/SaveLoad/SaveLoadService.cs` | Save / load entry points. |
| code | `Warden.Telemetry/SaveLoad/SchemaVersionMigrator.cs` | v0.4 → v0.5 migration. |
| code | `Warden.Telemetry/SaveLoad/SaveLoadException.cs` | Typed errors. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Add `BootFromWorldStateDto` overload. |
| test | (~25 test files per Tests section) | Comprehensive coverage. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.2.0.md` | Completion note. Test count, perf measurements, special-case fields. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | Empty world round-trips: serialize → deserialize → byte-identical. | unit-test |
| AT-02 | Office-starter at boot round-trips byte-identical. | unit-test |
| AT-03 | After 1000 ticks of activity, full state round-trips: serialize → deserialize → boot → tick 1000 → byte-identical projection sequence. | unit-test |
| AT-04 | Every component in test matrix round-trips with values preserved (within float tolerance). | unit-test |
| AT-05 | Save mid-choke; load; budget continues; eventual `Deceased(Choked)` fires at correct tick. | integration test |
| AT-06 | Save mid-faint; load; recovery to `Alive` fires at correct tick. | integration test |
| AT-07 | Save with `LockedTag` doors; load; pathfinding cache rebuilds with locks honored. | integration test |
| AT-08 | Save with `CorpseTag`; load; corpses preserved at `LocationRoomId`. | integration test |
| AT-09 | Save during bereavement cascade; load; cascade completes without double-stress. | integration test |
| AT-10 | v0.4 save loaded by v0.5 engine: succeeds; new fields default-filled. | unit test |
| AT-11 | v0.5 save loaded by v0.4 engine: throws `SaveLoadException` with version-mismatch message. | unit test |
| AT-12 | Save serialization at 30 NPCs ≤ 50ms. | perf test |
| AT-13 | Load deserialization at 30 NPCs ≤ 100ms. | perf test |
| AT-14 | Float values round-trip within 1e-9 tolerance. | unit test |
| AT-15 | All Phase 0/1/2/3.0.x/3.1.x tests stay green. | regression |
| AT-16 | `dotnet build` warning count = 0; `dotnet test` all green. | build + test |

---

## Followups (not in scope)

- Cloud save sync. Future.
- Binary save format. Size optimization; future.
- Cross-version migration tooling (v0.3 → v0.5). Future.
- Save thumbnails (camera screenshot at save time). UX polish.
- Auto-save on critical events. UX bible §6 open question.
- Save corruption detection / recovery (checksum / signature). Future hardening.
- Save compression (gzip the JSON). Future.
- Save metadata (game-day, NPC count, hours-played, last-event) surfaced in slot picker. UX polish.


---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: NOT NEEDED

This is a Track 1 (engine) packet. All verification is handled by the xUnit test suite. Once `dotnet test` returns green for `APIFramework.Tests` (and any other affected test project), the packet is ready to push and PR. **No Unity Editor steps required.**

The Sonnet executor's pipeline:

1. Implement the spec.
2. Add or update xUnit tests to cover all acceptance criteria.
3. Run `dotnet test` from the repo root. Must be green.
4. Run `dotnet build` to confirm no warnings introduced.
5. Stage all changes including the self-cleanup deletion (see below).
6. Commit on the worktree's feature branch.
7. Push the branch and open a PR against `staging`.
8. Stop. Do **not** merge. Talon merges after review.

If a test fails or compile fails, fix the underlying cause. Do **not** skip tests, do **not** mark expected-failures, do **not** push a red branch.

### Cost envelope (1-5-25 Claude army)

Target: **$0.50–$1.20** per packet wall-time on the orchestrator. Timebox is stated above in the packet header. If the executing Sonnet observes its own cost approaching the upper bound without nearing acceptance criteria, **escalate to Talon** by stopping work and committing a `WP-X-blocker.md` note to the worktree explaining what burned the budget. Do not silently exceed the envelope.

Cost-discipline rules of thumb:
- Read reference files at most once per session — cache content in working memory rather than re-reading.
- Run `dotnet test` against the focused subset (`--filter`) during iteration; full suite only at the end.
- If a refactor is pulling far more files than the spec named, stop and re-read the spec; the spec may be wrong about scope.

### Self-cleanup on merge

The active `docs/c2-infrastructure/work-packets/` directory should contain only **pending** packets. Shipped packets are deleted, not archived to `_completed-specs/` (Talon's convention from 2026-04-30 forward).

Before opening the PR, the executing Sonnet must:

1. **Check downstream dependents** with this command from the repo root:
   ```bash
   git grep -l "<THIS-PACKET-ID>" docs/c2-infrastructure/work-packets/ | grep -v "_completed" | grep -v "_PACKET-COMPLETION-PROTOCOL"
   ```
   Replace `<THIS-PACKET-ID>` with this packet's identifier (e.g., `WP-3.0.4`).

2. **If the grep returns no results** (no other pending packet references this one): include `git rm docs/c2-infrastructure/work-packets/<this-packet-filename>.md` in the staging set. The deletion ships in the same commit as the implementation. Add the line `Self-cleanup: spec file deleted, no pending dependents.` to the commit message.

3. **If the grep returns one or more pending packets**: leave the spec file in place. Add a one-line status header to the top of this spec file (immediately under the H1):
   ```markdown
   > **STATUS:** SHIPPED to staging YYYY-MM-DD. Retained because pending packets depend on this spec: <list>.
   ```
   Add the line `Self-cleanup: spec retained, dependents: <list>.` to the commit message.

4. **Do not touch** files under `_completed/` or `_completed-specs/` — those are historical artifacts from earlier phases.

5. The git history (commit message + PR body) is the historical record. The spec file itself is ephemeral once shipped without dependents.
