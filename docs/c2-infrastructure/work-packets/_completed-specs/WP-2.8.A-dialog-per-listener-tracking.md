# WP-2.8.A — Dialog Per-Listener Fragment Tracking

**Tier:** Sonnet
**Depends on:** WP-1.10.A (dialog corpus + retrieval pipeline + DialogHistoryComponent)
**Parallel-safe with:** WP-2.7.A, WP-2.9.A, WP-2.3.B (different file footprints)
**Timebox:** 60 minutes
**Budget:** $0.25

---

## Goal

Resolve the dialog bible's open question: per-listener fragment-use tracking. Currently `DialogHistoryComponent` tracks total uses of each fragment by a speaker — that's enough for calcify-into-tic but not enough to capture the cast bible's Affair archetype, which "code-switches between hidden and public modes" — the same NPC uses different fragments with the affair partner than with everyone else.

This packet extends `DialogHistoryComponent` with a per-listener counter, and gives `DialogFragmentRetrievalSystem` a small bias toward fragments the speaker has used with the same listener before. The Affair NPC's flirt fragments calcify only with the partner; in front of others, the same NPC reaches for different fragments. The dialog bible's "register inconsistent because they're code-switching" tic is mechanised.

After this packet, an NPC develops both a *speaker voice* (calcified globally) and a *per-listener voice* (calcified per pair). The two layer additively in retrieval scoring.

---

## Reference files

- `docs/c2-content/dialog-bible.md` — **read first.** Section "Open questions for revision" → "Per-listener fragment tracking" is the design source. Section "Tying it to the cast bible's archetypes" → The Affair entry is the shape-defining test case.
- `docs/c2-content/cast-bible.md` — The Affair archetype. Code-switching as a behaviour the engine should produce.
- `docs/c2-infrastructure/00-SRD.md` §8.5 (social state is first-class).
- `docs/c2-infrastructure/work-packets/_completed/WP-1.10.A.md` — the dialog stack this packet extends. Confirm the `DialogFragmentRetrievalSystem` interface and the existing `FragmentUseRecord` shape.
- `APIFramework/Components/DialogHistoryComponent.cs` — current shape: `Dictionary<string, FragmentUseRecord> UsesByFragmentId`. This packet adds a sibling per-listener counter.
- `APIFramework/Systems/Dialog/DialogFragmentRetrievalSystem.cs` — the retrieval scoring loop. This packet adds one new score term.
- `APIFramework/Systems/Dialog/DialogCalcifySystem.cs` — calcify mechanism. **No change** at v0.1; per-listener calcification is a future polish (the bible flags it). v0.1 only adds per-listener selection bias, not per-listener calcify.
- `APIFramework/Components/RecognizedTicComponent.cs` — listener-side tic recognition. Read for context; do not modify.
- `APIFramework/Config/SimConfig.cs` → `DialogConfig` — add one new key for the per-listener bias score.
- `SimConfig.json` → `dialog` section.

## Non-goals

- Do **not** add per-listener calcify status. Calcification stays speaker-level at v0.1. Per-listener calcify is the next polish step (bible flags it as deferred).
- Do **not** modify `RecognizedTicComponent` or the tic-propagation mechanism. Tic recognition stays unchanged.
- Do **not** modify the corpus, the retrieval-decision-tree filter passes (register, relationship-fit, valence), or the calcify thresholds. Only the scoring layer gets the new term.
- Do **not** introduce a new component. Extend `DialogHistoryComponent` with a new field; the existing component is the single per-NPC dialog-state anchor.
- Do **not** add memory-cost concerns (e.g., bounded per-listener tracking). At ~15 NPCs × ~200 fragments × ~14 other listeners = ~42K records max per NPC. Acceptable at v0.1; bound it later if profiling shows pressure.
- Do **not** modify wire formats. `DialogHistoryComponent` is engine-internal at v0.1; per-listener counters are not surfaced to the projector.
- Do **not** introduce a NuGet dependency.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (SRD §8.1.)
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing.

---

## Design notes

### Component extension

```csharp
public struct DialogHistoryComponent
{
    public Dictionary<string, FragmentUseRecord> UsesByFragmentId;

    /// <summary>Per-(listener, fragment) use counter. Key: listener int id. Value: counts per fragment.</summary>
    public Dictionary<int, Dictionary<string, int>> UsesByListenerAndFragmentId;

    public DialogHistoryComponent()
    {
        UsesByFragmentId = new Dictionary<string, FragmentUseRecord>();
        UsesByListenerAndFragmentId = new Dictionary<int, Dictionary<string, int>>();
    }
}
```

Both dictionaries are reference types — copies of the struct share the same instance, matching the existing pattern.

### Recording the use

When `DialogCalcifySystem` (or wherever the use-recording happens — confirm during reading) records a fragment use:

```csharp
var listenerIntId = WillpowerSystem.EntityIntId(listenerEntity);
if (!history.UsesByListenerAndFragmentId.TryGetValue(listenerIntId, out var perFragment))
{
    perFragment = new Dictionary<string, int>();
    history.UsesByListenerAndFragmentId[listenerIntId] = perFragment;
}
perFragment[fragmentId] = perFragment.GetValueOrDefault(fragmentId, 0) + 1;
```

The listener id comes from the `SpokenFragmentEvent` payload (or from the proximity event that triggered the dialog moment — confirm the actual signature during reading).

### Retrieval bias

In `DialogFragmentRetrievalSystem.Score(...)`, after the existing terms (valence, recency, calcify), add:

```csharp
// Per-listener bias: this NPC has used this fragment with this listener before.
if (listenerIntId.HasValue
    && history.UsesByListenerAndFragmentId.TryGetValue(listenerIntId.Value, out var perFragment)
    && perFragment.TryGetValue(fragment.Id, out var listenerUseCount)
    && listenerUseCount > 0)
{
    score += config.PerListenerBiasScore;   // default 2 (small but meaningful)
}
```

The bias is small relative to calcify (3) and valence-match (5) — enough to break ties toward established per-pair phrasings, not enough to override register or context.

### Why a small bias, not a large one

The dialog bible flags per-listener tracking as a v0.1 deferral specifically because the *speaker-level* voice is the dominant axis. Per-listener differentiation is for the Affair-shaped edge cases. A large bias would over-cluster fragments per pair (every relationship has its "shorthand"), which feels right in some pairs and wrong in most. Default `PerListenerBiasScore = 2` produces visible code-switching in pairs with high interaction frequency without flattening the speaker voice elsewhere.

### SimConfig addition

```jsonc
{
  "dialog": {
    // ... existing keys ...
    "perListenerBiasScore": 2
  }
}
```

### Tests

- `DialogHistoryComponentPerListenerTests.cs` — extending the dictionary preserves equality of the struct (reference type pointer equality); adding to one listener doesn't affect another.
- `DialogFragmentRetrievalPerListenerBiasTests.cs` — given a corpus and a speaker, after 5 uses of fragment F with listener L, retrieval-score(F, listener=L) = baseline + perListenerBiasScore; retrieval-score(F, listener=other) = baseline (no bias).
- `DialogCodeSwitchingScenarioTests.cs` — the Affair-archetype shape: an NPC speaks with two different listeners; over 50 dialog moments, the per-fragment use distribution diverges per listener. Statistical assertion (chi-square or KS test) that listener identity matters.
- `DialogPerListenerDeterminismTests.cs` — 5000-tick byte-identical dialog history.
- All existing `DialogContextDecisionSystemTests`, `DialogFragmentRetrievalSystemTests`, `DialogCalcifySystemTests` from WP-1.10.A stay green — additive change only.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Components/DialogHistoryComponent.cs` (modified) | Add `UsesByListenerAndFragmentId` field. |
| code | `APIFramework/Systems/Dialog/DialogCalcifySystem.cs` (modified) | Record per-listener counts when recording fragment uses. |
| code | `APIFramework/Systems/Dialog/DialogFragmentRetrievalSystem.cs` (modified) | Add per-listener bias to scoring. |
| code | `APIFramework/Config/SimConfig.cs` (modified) | Add `PerListenerBiasScore` to `DialogConfig`. |
| code | `SimConfig.json` (modified) | Add `perListenerBiasScore` under `dialog`. |
| code | `APIFramework.Tests/Components/DialogHistoryComponentPerListenerTests.cs` | Component extension correctness. |
| code | `APIFramework.Tests/Systems/Dialog/DialogFragmentRetrievalPerListenerBiasTests.cs` | Bias applied correctly. |
| code | `APIFramework.Tests/Systems/Dialog/DialogCodeSwitchingScenarioTests.cs` | Affair-shaped statistical test. |
| code | `APIFramework.Tests/Systems/Dialog/DialogPerListenerDeterminismTests.cs` | 5000-tick determinism. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-2.8.A.md` | Completion note. SimConfig defaults; statistical-test methodology. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `DialogHistoryComponent` with new field compiles, instantiates, equality round-trip. Adding to one listener's per-fragment count does not affect another listener's. | unit-test |
| AT-02 | After 5 uses of fragment F with listener L, retrieval-score(F, listener=L) = baseline + `PerListenerBiasScore`; retrieval-score(F, listener=other) = baseline. | unit-test |
| AT-03 | Statistical: NPC speaks with 2 listeners across 50 dialog moments; per-listener fragment-use distributions diverge significantly (chi-square p<0.01 or KS test). | unit-test |
| AT-04 | All existing WP-1.10.A dialog tests stay green — no regression in calcify, retrieval, recognition. | regression |
| AT-05 | Determinism: 5000-tick run, two seeds with the same world: byte-identical `DialogHistoryComponent` state across runs (including the new per-listener field). | unit-test |
| AT-06 | All Wave 1, Wave 2, Wave 3 acceptance tests stay green. | regression |
| AT-07 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-08 | `dotnet test ECSSimulation.sln` — all green. | build + unit-test |

---

## Followups (not in scope)

- **Per-listener calcify status.** A fragment used 8+ times with the same listener becomes a *per-pair tic* — calcified only with that listener. Visible in retrieval as a stronger bias when speaking to that specific listener.
- **Per-pair tic recognition.** Listener marks "X's thing with me" — separate from "X's general thing." Cross-system polish.
- **Memory bounds.** Per-listener tracking at ~15 NPCs × ~200 fragments × ~14 listeners ≈ 42K entries per NPC. Profile under playtest; add a per-listener LRU bound if needed.
- **Wire-format surface.** Per-listener history could surface in telemetry for design-time observability. Defer to a v0.5+ schema bump.
