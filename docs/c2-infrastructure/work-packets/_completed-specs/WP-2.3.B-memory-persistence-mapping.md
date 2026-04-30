# WP-2.3.B — Memory Persistence Mapping for Wave 3 Narrative Kinds

**Tier:** Sonnet
**Depends on:** WP-2.3.A (memory recording — `IsPersistent` table to extend), WP-2.5.A (adds `MaskSlip` narrative kind), WP-2.6.A (adds `OverdueTask`, `TaskCompleted` narrative kinds)
**Parallel-safe with:** WP-2.7.A, WP-2.8.A, WP-2.9.A (different file footprints)
**Timebox:** 30 minutes
**Budget:** $0.10

---

## Goal

Wave 3 added three new `NarrativeEventKind` values: `MaskSlip` (WP-2.5.A), `OverdueTask` (WP-2.6.A), `TaskCompleted` (WP-2.6.A). `MemoryRecordingSystem.IsPersistent` (WP-2.3.A) classifies which narrative kinds count as persistent (memorable next week per the bible's per-pair threshold). It currently maps only Phase-1 kinds; the Wave 3 additions default to `Persistent: false` because they're not in the switch table.

This packet patches the `IsPersistent` table to give the Wave 3 kinds the correct persistence:

- `MaskSlip` → **true** (a coworker watching their boss break is the kind of thing remembered for months)
- `OverdueTask` → **true** (missed deadlines stick — both for the person who missed and anyone affected)
- `TaskCompleted` → **false** (most completions are routine; doesn't deserve a memory slot)

After this packet, the Wave 3 narrative output flows correctly into per-pair / personal memory persistence.

---

## Reference files

- `docs/c2-infrastructure/00-SRD.md` §8.3 (per-pair memory threshold).
- `docs/c2-content/world-bible.md` — persistence threshold ("would the staff still be talking about this in a month?"). Per-pair memory threshold is *lighter* than chronicle-global but the same intuition applies.
- `docs/c2-infrastructure/work-packets/_completed/WP-2.3.A.md` — the original `IsPersistent` table this packet extends.
- `docs/c2-infrastructure/work-packets/_completed/WP-2.5.A.md` — confirms `MaskSlip` was added to `NarrativeEventKind`.
- `docs/c2-infrastructure/work-packets/_completed/WP-2.6.A.md` — confirms `OverdueTask` and `TaskCompleted` were added to `NarrativeEventKind`.
- `APIFramework/Systems/MemoryRecordingSystem.cs` — the `IsPersistent` switch table to extend. The function is small (currently ~5 lines); the modification is 3 added cases.
- `APIFramework/Systems/Narrative/NarrativeEventKind.cs` — confirm the three new enum values exist (they should after Wave 3 merges; this packet must not dispatch until Wave 3 is on staging).

## Non-goals

- Do **not** modify `MemoryRecordingSystem` beyond the `IsPersistent` switch. Routing, buffering, projection — all unchanged.
- Do **not** add new narrative kinds. This packet only extends the persistence mapping for kinds Wave 3 already added.
- Do **not** modify `NarrativeEventKind`, `NarrativeEventBus`, `NarrativeEventDetector`, or the chronicle (WP-1.9.A).
- Do **not** modify `MemoryEntry`, `RelationshipMemoryComponent`, `PersonalMemoryComponent`, or any DTO.
- Do **not** modify the projector. Persistence-flag changes flow through the existing serialisation path.
- Do **not** introduce a NuGet dependency.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (SRD §8.1.)
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing.

---

## Design notes

### The patch

```csharp
public static bool IsPersistent(NarrativeEventKind kind) => kind switch
{
    NarrativeEventKind.WillpowerCollapse => true,
    NarrativeEventKind.LeftRoomAbruptly  => true,
    NarrativeEventKind.MaskSlip          => true,    // ← added
    NarrativeEventKind.OverdueTask       => true,    // ← added
    NarrativeEventKind.TaskCompleted     => false,   // ← added (explicit; same as default but intentional)
    _                                    => false,
};
```

The `TaskCompleted: false` line is technically redundant (the default is false) but is added explicitly for documentation: future readers see that the kind was considered and deliberately classified, not just missed.

### Tests

- `MemoryPersistenceWaveThreeMappingTests.cs` — one test per new kind, asserting `IsPersistent(kind)` returns the expected value. Plus a regression test that the existing kinds (`WillpowerCollapse`, `LeftRoomAbruptly`) still return `true`.
- Optional integration: `MaskSlip` candidate emitted in a small simulation produces a memory entry with `Persistent: true`; `OverdueTask` similarly; `TaskCompleted` produces an entry with `Persistent: false`. (Skip if WP-2.3.A's existing tests already cover the routing; this packet only adjusts the classification.)

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Systems/MemoryRecordingSystem.cs` (modified) | Three new switch cases per Design notes. |
| code | `APIFramework.Tests/Systems/MemoryPersistenceWaveThreeMappingTests.cs` | Per-kind classification assertions + existing-kind regression. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-2.3.B.md` | Completion note. The classification table the Sonnet committed to with one-line rationale per kind. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `IsPersistent(NarrativeEventKind.MaskSlip)` returns `true`. | unit-test |
| AT-02 | `IsPersistent(NarrativeEventKind.OverdueTask)` returns `true`. | unit-test |
| AT-03 | `IsPersistent(NarrativeEventKind.TaskCompleted)` returns `false`. | unit-test |
| AT-04 | `IsPersistent(NarrativeEventKind.WillpowerCollapse)` and `LeftRoomAbruptly` still return `true` (regression). | unit-test |
| AT-05 | All Wave 1, Wave 2, Wave 3 acceptance tests stay green. | regression |
| AT-06 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-07 | `dotnet test ECSSimulation.sln` — all green. | build + unit-test |

---

## Followups (not in scope)

- **Per-archetype persistence weights.** Some archetypes (the Cynic) genuinely don't remember most things; some (the Recovering) remember everything as significant. Per-archetype `IsPersistent` weighting would be more nuanced than the global table. Defer to playtest evidence.
- **Magnitude-aware persistence.** A `MaskSlip` with intensity 30 (mild crack) might not deserve persistence; intensity 90 (full break) definitely does. Wire the candidate's `IntensityHint` (or a magnitude field) into the classification. Defer.
- **Time-decay of persistence flag.** A persistent memory ages out of the ring buffer eventually anyway, but a smarter system would *demote* old persistent memories to ephemeral as a consolidation pass. Speculative.
