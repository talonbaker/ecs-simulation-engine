# WP-1.10.A Completion Note
## Dialog Implementation: Corpus + Calcify

**Completed:** 2026-04-25  
**Branch:** `feat/wp-1.10.A`  
**All tests:** 691 passed, 0 failed

---

### Deliverables

| Artifact | Status |
|----------|--------|
| `docs/c2-infrastructure/schemas/corpus.schema.json` | Created |
| `Warden.Contracts/SchemaValidation/corpus.schema.json` | Created (embedded resource) |
| `docs/c2-content/dialog/corpus-starter.json` | Created (180 fragments) |
| `APIFramework/Components/DialogHistoryComponent.cs` | Created |
| `APIFramework/Components/RecognizedTicComponent.cs` | Created |
| `APIFramework/Systems/Dialog/SpokenFragmentEvent.cs` | Created |
| `APIFramework/Systems/Dialog/PendingDialogQueue.cs` | Created |
| `APIFramework/Systems/Dialog/DialogCorpusService.cs` | Created |
| `APIFramework/Systems/Dialog/DialogContextDecisionSystem.cs` | Created |
| `APIFramework/Systems/Dialog/DialogFragmentRetrievalSystem.cs` | Created |
| `APIFramework/Systems/Dialog/DialogCalcifySystem.cs` | Created |
| `Warden.Contracts/SchemaValidation/Schema.cs` | Modified (added `Corpus`) |
| `Warden.Contracts/SchemaValidation/SchemaValidator.cs` | Modified (added switch case) |
| `APIFramework/Core/SystemPhase.cs` | Modified (added `Dialog = 75`) |
| `APIFramework/Systems/Spatial/ProximityEventBus.cs` | Modified (added `OnSpokenFragment`) |
| `APIFramework/Config/SimConfig.cs` | Modified (added `DialogConfig`) |
| `SimConfig.json` | Modified (added `dialog` section) |
| `APIFramework/Components/EntityTemplates.cs` | Modified (added `WithDialogHistory()`) |
| `APIFramework/Core/SimulationBootstrapper.cs` | Modified (corpus load + system registration) |
| `APIFramework.Tests/Systems/Dialog/DialogCorpusServiceTests.cs` | Created (AT-01–AT-04) |
| `APIFramework.Tests/Systems/Dialog/DialogFragmentRetrievalSystemTests.cs` | Created (AT-05–AT-08) |
| `APIFramework.Tests/Systems/Dialog/DialogCalcifySystemTests.cs` | Created (AT-07, AT-09) |
| `APIFramework.Tests/Systems/Dialog/DialogDeterminismTests.cs` | Created (AT-12) |

---

### Architecture decisions

**Components as structs:** `DialogHistoryComponent` and `RecognizedTicComponent` are value-type structs (matching the `where T : struct` constraint on `Entity.Add<T>`). Their Dictionary fields are reference types, so struct copies share the same underlying collections — mutations from `Get<T>()` copies propagate correctly.

**Phase 75:** Dialog runs after Narrative (70) so drive state has fully settled. Within phase 75: `DialogContextDecisionSystem` → `DialogFragmentRetrievalSystem` → `DialogCalcifySystem`.

**Context decision:** `DialogContextDecisionSystem` subscribes to `ProximityEventBus.OnEnteredConversationRange` to maintain an in-range set. Each tick it probabilistically attempts dialog per pair (`DialogAttemptProbability = 0.05` default) and maps the speaker's most elevated drive to a corpus context string.

**Register from PersonalityComponent:** `VocabularyRegister` is already a field on `PersonalityComponent` — no new component needed.

**Tic recognition:** Hearing counts tracked on the listener's `RecognizedTicComponent` inside `DialogFragmentRetrievalSystem`, using `EntityIntId()` (same byte-extraction pattern as `NarrativeEventDetector`).

**Graceful corpus skip:** If the corpus file is absent at boot, `CorpusService` remains null and the three dialog systems are not registered — the simulation runs normally without dialog.

---

### Acceptance test results

- AT-01 Schema validation: **PASS**
- AT-02 ≥ 180 fragments: **PASS** (180 fragments in starter corpus)
- AT-03 All 78 register×context combos have ≥ 2 fragments: **PASS**
- AT-04 casual×lashOut has ≥ 3 fragments: **PASS**
- AT-05 SpokenFragmentEvent emitted: **PASS**
- AT-06 High-irritation speaker selects irritation:high fragment: **PASS**
- AT-07 Recency penalty redirects selection: **PASS**
- AT-07 Calcification fires at threshold + dominance: **PASS**
- AT-08 CalcifyBias score boosts calcified fragment: **PASS**
- AT-09 Calcified fragment decalcifies after timeout: **PASS**
- AT-12 Same seed → identical fragment selection sequence: **PASS**

---

### Non-goals confirmed

- No LLM calls at runtime
- No per-listener fragment tracking (only tic recognition counts)
- No fragment authoring CLI
- No modifications to `Warden.Anthropic/`, `Warden.Orchestrator/`, `Warden.Telemetry/`, `Warden.Contracts/Telemetry/*`, or `world-state.schema.json`
- Fragment count: 180 (≤ 250)
- No new NuGet dependencies
- `SeededRandom` used throughout (not `System.Random`)
