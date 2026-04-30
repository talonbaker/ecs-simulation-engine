# WP-1.10.A — Dialog Implementation: Phrase Corpus + Retrieval + Calcify

**Tier:** Sonnet
**Depends on:** WP-1.4.A (social drives, register on `PersonalityComponent` — merged), WP-1.1.A (proximity events — merged), WP-1.6.A (narrative bus — merged for trigger detection).
**Parallel-safe with:** WP-1.8.A (Cast generator), WP-1.9.A (Chronicle). Different file footprints.
**Timebox:** 120 minutes
**Budget:** $0.55

---

## Goal

Land the dialog system the new dialog bible commits to. Selection from a curated palette under emotional pressure with calcification. No LLM, no embedding, no inference — a deterministic decision tree over a hand-authored phrase corpus, with a calcify mechanism that turns repeated selections into a character's signature voice.

Five pieces:

1. **Corpus schema.** A new `corpus.schema.json` defining a phrase-corpus file. Each entry: id, text, register, context, valenceProfile, relationshipFit, noteworthiness.

2. **Starter corpus.** `docs/c2-content/dialog/corpus-starter.json` — ~200 hand-authored fragments distributed across registers, contexts, and valences per the dialog bible's authoring scope. Sonnet authors this from the bible's structure (every register × every context combination has 2–4 fragments).

3. **Engine components.** `DialogHistoryComponent` per NPC tracking fragment use counts and calcify state. Optional `RecognizedTicComponent` for cross-NPC tic recognition.

4. **Engine systems.** Three new systems run per tick:
   - `DialogContextDecisionSystem` — reads triggers (proximity events, drive deltas) and decides "this NPC has a dialog moment of context X."
   - `DialogFragmentRetrievalSystem` — runs the decision tree to select a fragment, emits a `SpokenFragmentEvent` on the proximity bus.
   - `DialogCalcifySystem` — updates use counts; flips `calcified` when thresholds hit; propagates recognized tics to listeners.

5. **Tests + determinism.** Two runs same seed → byte-identical fragment selections.

What this packet does **not** do: integrate with action selection (the action-gating bible's full action-selection logic isn't built yet — this packet uses simple drive-delta heuristics for trigger detection). Doesn't implement per-listener fragment differentiation (Affair archetype's code-switching). Doesn't ship a fragment-authoring tool — corpus edits happen in the JSON file directly.

---

## Reference files

- `docs/c2-content/DRAFT-dialog-bible.md` — **read first**. Every component shape, every system, every threshold this packet implements.
- `docs/c2-content/DRAFT-cast-bible.md` — vocabulary register list, the eight drives that feed `valenceProfile` matching.
- `docs/c2-content/DRAFT-action-gating.md` — for context on how dialog moments factor into the larger willpower/inhibition layer.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.4.A.md` — confirms `SocialDrivesComponent`, `PersonalityComponent.VocabularyRegister`.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.1.A.md` — confirms `ProximityEventBus`, `EnteredConversationRange`.
- `docs/c2-infrastructure/work-packets/_completed/WP-1.6.A.md` — confirms `NarrativeEventBus`, `DriveSpike` candidates.
- `Warden.Contracts/SchemaValidation/SchemaValidator.cs` — for validating the corpus file at boot.
- `Warden.Contracts/JsonOptions.cs` — JSON parsing.
- `APIFramework/Components/SocialDrivesComponent.cs`, `PersonalityComponent.cs` — the inputs the retrieval system reads.
- `APIFramework/Systems/Spatial/ProximityEventBus.cs` — for trigger detection and `SpokenFragmentEvent` emission.
- `APIFramework/Core/SimulationBootstrapper.cs` — system + service registration.
- `APIFramework/Core/SeededRandom.cs` — for the weighted-random fragment pick.
- `SimConfig.json` — runtime tuning lives here.

## Non-goals

- Do **not** generate any fragments via LLM, Markov chain, or any algorithmic source. All fragments are hand-authored in `corpus-starter.json` by the Sonnet, drawing on the dialog bible's authoring guidance and the world bible's office-tone reference.
- Do **not** implement per-listener fragment tracking. Speaker-level tracking is enough at v0.1; per-listener differentiation (the Affair archetype's code-switching) is a follow-up.
- Do **not** add a fragment-authoring CLI tool. Edits happen in the JSON file directly.
- Do **not** modify any file under `Warden.Anthropic/`, `Warden.Orchestrator/`, or `Warden.Telemetry/`. Dialog state is engine-internal at v0.1; projection comes when a future schema bump reserves `entities[].dialogHistory`.
- Do **not** modify `Warden.Contracts/Telemetry/*` (no DTO changes — dialog state isn't on the wire yet).
- Do **not** modify `world-state.schema.json`. No schema bump for the wire format.
- Do **not** ship a fragment count higher than 250. The bible's authoring scope is 200–400 at v0.1; aim for ~200 for the starter so review is tractable.
- Do **not** ship fragments that would violate the world bible's tone commitments (no slurs, no fragments that endorse harassment, no fragments that sanitize office life — the bible is explicit on this).
- Do **not** ship per-NPC custom corpora. All NPCs share the same corpus; differentiation comes from register filtering and calcify history.
- Do **not** integrate with any action-selection layer that doesn't exist yet. Use the heuristic trigger detection described in Design notes.
- Do **not** introduce a NuGet dependency.
- Do **not** use `System.Random`. `SeededRandom` only.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (Architectural axiom 8.1.)

---

## Design notes

### Corpus schema

`corpus.schema.json` (new):

```jsonc
{
  "type": "object",
  "additionalProperties": false,
  "required": ["schemaVersion", "fragments"],
  "properties": {
    "schemaVersion": { "type": "string", "const": "0.1.0" },
    "fragments": {
      "type": "array",
      "maxItems": 1000,
      "items": { "$ref": "#/$defs/fragment" }
    }
  },
  "$defs": {
    "fragment": {
      "type": "object",
      "additionalProperties": false,
      "required": ["id", "text", "register", "context", "valenceProfile", "noteworthiness"],
      "properties": {
        "id":       { "type": "string", "maxLength": 64 },
        "text":     { "type": "string", "maxLength": 200 },
        "register": { "type": "string",
                      "enum": ["formal", "casual", "crass", "clipped", "academic", "folksy"] },
        "context":  { "type": "string",
                      "enum": ["greeting", "refusal", "agreement", "complaint",
                               "flirt", "deflect", "lashOut", "share",
                               "brushOff", "encouragement", "thanks",
                               "apology", "acknowledge"] },
        "valenceProfile": {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "belonging":  { "type": "string", "enum": ["low", "mid", "high"] },
            "status":     { "type": "string", "enum": ["low", "mid", "high"] },
            "affection":  { "type": "string", "enum": ["low", "mid", "high"] },
            "irritation": { "type": "string", "enum": ["low", "mid", "high"] },
            "attraction": { "type": "string", "enum": ["low", "mid", "high"] },
            "trust":      { "type": "string", "enum": ["low", "mid", "high"] },
            "suspicion":  { "type": "string", "enum": ["low", "mid", "high"] },
            "loneliness": { "type": "string", "enum": ["low", "mid", "high"] }
          }
        },
        "relationshipFit": {
          "type": "array",
          "maxItems": 7,
          "items": {
            "type": "string",
            "enum": ["closeFriend", "colleague", "rival", "stranger",
                    "romantic", "superior", "subordinate"]
          }
        },
        "noteworthiness": { "type": "integer", "minimum": 0, "maximum": 100 }
      }
    }
  }
}
```

`additionalProperties: false`. All numeric fields bounded.

### Starter corpus authoring

The Sonnet authors `corpus-starter.json` with ~200 fragments. Distribution:

- Every register × every context: 2–4 fragments.
- 6 registers × 13 contexts × 3 fragments avg ≈ 234 fragments. Round to ~200 by trimming low-priority combinations (e.g., `formal lashOut` is rare; one fragment is enough).
- Higher-noteworthiness contexts (lashOut, flirt, share) get extra coverage.
- Fragments draw on the world bible's office tone — early-2000s, dry-cynical, occasional crass moments. **Read the world bible's "Tone" section before writing.**
- Examples for calibration:
  - `casual / brushOff` low noteworthiness: `"Eh, whatever."`, `"Sure, fine."`, `"Mm-hm."`
  - `crass / lashOut` high noteworthiness: `"Are you fucking kidding me right now."`, `"Get out of my face, Frank."`
  - `clipped / acknowledge` low: `"Mm."`, `"Yeah."`, `"Right."`
  - `folksy / share` mid: `"Y'know what my granddad used to say..."`, `"Reminds me of when..."`
  - `formal / greeting` low: `"Good morning."`, `"Hello."`, `"Pleasure."`

The Sonnet does NOT repeat real famous quotes, brand names, or content that violates the world bible's tone commitments (no slurs, no harassment-endorsing content). When in doubt, the bible's "Comedy contract" subsection is the test: "Sexual humor is allowed and characters react in-character; profanity is normal but not constant; depression and shame are depicted honestly; conflict is interpersonal, not violent."

### Engine components

`DialogHistoryComponent`:

```csharp
public sealed class DialogHistoryComponent : IComponent
{
    public Dictionary<string, FragmentUseRecord> UsesByFragmentId { get; init; } = new();
}

public sealed class FragmentUseRecord
{
    public int  UseCount;
    public long FirstUseTick;
    public long LastUseTick;
    public string DominantContext;        // most common context this fragment was used in
    public bool   Calcified;
}
```

Mutable record because use counts increment per tick. Initialized empty per NPC at spawn.

`RecognizedTicComponent`:

```csharp
public sealed class RecognizedTicComponent : IComponent
{
    public Dictionary<int, HashSet<string>> RecognizedTicsBySpeakerId { get; init; } = new();
}
```

A listener's record of "I recognize these fragments as `<speakerId>`'s tic." Updated by `DialogCalcifySystem` when a listener has heard a calcified fragment from a specific speaker ≥ `SimConfig.dialog.ticRecognitionThreshold` (default 5) times.

### Engine systems

**`DialogContextDecisionSystem`** runs after `ProximityEventSystem` and `NarrativeEventDetector`. For each NPC, decides whether this tick is a "dialog moment" and what context:

- If `EnteredConversationRange` event for this NPC and the partner this tick → context = `greeting` (initial moment) or move to next branch.
- If a `DriveSpike` candidate fired for this NPC this tick → context derived from drive:
  - High `irritation` spike → `lashOut`
  - High `affection` spike → `flirt` or `share` (random pick weighted by `attraction` drive)
  - High `loneliness` spike → `share`
  - High `suspicion` spike → `deflect`
- Otherwise no dialog moment this tick (silent protagonist principle — most ticks produce no spoken line).

The system writes `(EntityId, Context, ListenerId)` tuples to a `PendingDialogQueue` for the retrieval system to consume.

**`DialogFragmentRetrievalSystem`** runs after the decision system. For each pending dialog moment:

1. Read speaker's `PersonalityComponent.VocabularyRegister`.
2. Query corpus: filter fragments by register and context.
3. If listener relationship is known (via the relationship entity between speaker and listener), filter by `relationshipFit`.
4. Score remaining fragments:
   - +5 per matching `valenceProfile` constraint (drive value at low/mid/high matches speaker's drive level).
   - −10 if recently used (within `SimConfig.dialog.recencyWindowSeconds`, default 300).
   - +3 if calcified (the calcify bias).
5. Pick highest-scored fragment. Tie-break by fragment id ascending.
6. Emit `SpokenFragmentEvent(SpeakerId, ListenerId, FragmentId, Tick)` on the proximity bus.
7. Increment speaker's `DialogHistoryComponent.UsesByFragmentId[fragmentId].UseCount` and update `LastUseTick`.

If no fragment matches the filters (which can happen for niche register/context combinations), emit no event. Silent moment.

**`DialogCalcifySystem`** runs after retrieval:

- For each speaker NPC, walk `DialogHistoryComponent.UsesByFragmentId`. If any fragment has `UseCount >= SimConfig.dialog.calcifyThreshold` (default 8) AND `≥ 70%` of those uses share the same `DominantContext`, set `Calcified = true`.
- For each `SpokenFragmentEvent` this tick, find listeners within conversation range. For each listener, increment a per-(speakerId, fragmentId) counter on their `RecognizedTicComponent`. When the counter crosses `ticRecognitionThreshold`, add the fragment to `RecognizedTicsBySpeakerId[speakerId]`.

### Determinism

The decision tree, the score computation, the fragment pick — all deterministic given the same inputs. Tie-breaking by `id` ensures consistent picks across runs. Tests verify two runs same seed produce byte-identical `SpokenFragmentEvent` streams.

### SimConfig additions

```jsonc
{
  "dialog": {
    "calcifyThreshold":         8,
    "calcifyContextDominanceMin": 0.70,
    "ticRecognitionThreshold":   5,
    "recencyWindowSeconds":    300,
    "valenceMatchScore":         5,
    "recencyPenalty":          -10,
    "calcifyBiasScore":          3,
    "valenceLowMaxValue":       33,
    "valenceMidMaxValue":       66,
    "decalcifyTimeoutDays":     30
  }
}
```

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| schema | `docs/c2-infrastructure/schemas/corpus.schema.json` | The phrase-corpus schema. |
| schema | `Warden.Contracts/SchemaValidation/corpus.schema.json` | Embedded mirror. |
| code | `Warden.Contracts/SchemaValidation/Schema.cs` (modified) | Add `SchemaVersions.Corpus = "0.1.0"`. |
| data | `docs/c2-content/dialog/corpus-starter.json` | ~200 hand-authored fragments per Design notes. |
| code | `APIFramework/Components/DialogHistoryComponent.cs` | Per Design notes. |
| code | `APIFramework/Components/RecognizedTicComponent.cs` | Per Design notes. |
| code | `APIFramework/Components/Tags.cs` (modified) | Add any new tags if needed. |
| code | `APIFramework/Systems/Dialog/DialogCorpusService.cs` | Singleton — loads `corpus-starter.json` at boot, validates against schema, exposes filtered queries by register and context. |
| code | `APIFramework/Systems/Dialog/DialogContextDecisionSystem.cs` | Per Design notes. |
| code | `APIFramework/Systems/Dialog/DialogFragmentRetrievalSystem.cs` | Per Design notes. |
| code | `APIFramework/Systems/Dialog/DialogCalcifySystem.cs` | Per Design notes. |
| code | `APIFramework/Systems/Dialog/PendingDialogQueue.cs` | Singleton queue between decision and retrieval systems. |
| code | `APIFramework/Systems/Dialog/SpokenFragmentEvent.cs` | Event record (added to `ProximityEventBus`). |
| code | `APIFramework/Systems/Spatial/ProximityEventBus.cs` (modified) | Add `OnSpokenFragment` event. |
| code | `APIFramework/Components/EntityTemplates.cs` (modified) | Add `WithDialogHistory(...)` helper. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modified) | Register `DialogCorpusService` (loads corpus at boot), `PendingDialogQueue` (singleton), three new systems in correct phase order. |
| code | `SimConfig.json` (modified) | Add the `dialog` section. |
| code | `APIFramework.Tests/Systems/Dialog/DialogCorpusServiceTests.cs` | Corpus loads; invalid corpus throws with structured error; filter queries return correct subsets. |
| code | `APIFramework.Tests/Systems/Dialog/DialogFragmentRetrievalSystemTests.cs` | (1) Speaker with `casual` register + `lashOut` context picks from casual lashOut fragments only. (2) Recency penalty applied within window. (3) Calcify bias applied when fragment is calcified. (4) Tie-break by id ascending. (5) No matching fragment → no event emitted. (6) Determinism: same seed → same pick. |
| code | `APIFramework.Tests/Systems/Dialog/DialogCalcifySystemTests.cs` | (1) Fragment used 8 times in same context → calcified. (2) Fragment used 8 times in spread contexts → not calcified. (3) Listener hears calcified fragment 5 times → tic recognized. |
| code | `APIFramework.Tests/Systems/Dialog/DialogDeterminismTests.cs` | Two runs over 5000 ticks with same seed produce byte-identical fragment streams. | unit-test |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-1.10.A.md` | Completion note. Standard template. Enumerate (a) corpus fragment count and distribution, (b) which trigger paths produce dialog moments, (c) what's deferred (per-listener tracking, action-selection integration, dialog-history projection). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `corpus.schema.json` validates a well-formed corpus file. | unit-test |
| AT-02 | `corpus-starter.json` validates clean against the schema and contains ≥ 180 fragments. | unit-test |
| AT-03 | `corpus-starter.json` covers every (register, context) combination at least once for the eight high-priority contexts (`greeting, agreement, refusal, complaint, share, lashOut, deflect, acknowledge`). | unit-test |
| AT-04 | `DialogCorpusService.QueryByRegisterAndContext("casual", "lashOut")` returns ≥ 2 fragments. | unit-test |
| AT-05 | `DialogFragmentRetrievalSystem` selects a fragment matching speaker register and decided context for a triggered NPC. | unit-test |
| AT-06 | A fragment used within `recencyWindowSeconds` is never re-selected before another fragment unless none other fits. | unit-test |
| AT-07 | After 8 same-context uses, a fragment's `Calcified` flag is true. | unit-test |
| AT-08 | After fragment becomes calcified, future selections in that context show ≥ 30% bias toward it (over 1000 trials). | unit-test |
| AT-09 | A listener within conversation range during 5 calcified-fragment events from the same speaker has the fragment listed in `RecognizedTicsBySpeakerId[speakerId]`. | unit-test |
| AT-10 | `DialogContextDecisionSystem`: a `DriveSpike` candidate of `irritation` produces a `lashOut` context decision. | unit-test |
| AT-11 | `DialogContextDecisionSystem`: an NPC with no triggers emits no dialog moment. | unit-test |
| AT-12 | Determinism: two runs of `DialogDeterminismTests` over 5000 ticks produce byte-identical fragment streams. | unit-test |
| AT-13 | `Warden.Telemetry.Tests` and `Warden.Contracts.Tests` all pass. | build + unit-test |
| AT-14 | All existing `APIFramework.Tests` stay green. | build + unit-test |
| AT-15 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-16 | `dotnet test ECSSimulation.sln` — every existing test stays green; new tests pass. | build |

---

## Followups (not in scope)

- Per-listener fragment tracking — for Affair archetype's code-switching. Adds `Dictionary<(speakerId, listenerId), Dictionary<fragmentId, ...>>`. Memory cost; defer until needed.
- Action-selection integration: when a real action-selection layer lands, `DialogContextDecisionSystem` reads its output instead of using drive-spike heuristics.
- Dialog history projection on the wire: a future schema bump reserves `entities[].dialogHistory`; projector populates from `DialogHistoryComponent`.
- Corpus expansion tooling: a CLI that helps add fragments without breaking schema (Sonnet-callable in a small prompt).
- De-calcification: a fragment unused for 30+ sim-days loses its calcified status. Engine support for this is in the design, but may not need implementation until long-arc playtests show calcification getting stuck.
- Per-NPC custom corpora (an extension where the cast generator picks a personalized subset of the global corpus per NPC). The bible doesn't commit to this; revisit if differentiation feels weak.
- Fragment authoring during play: a debug tool that lets the developer add a fragment mid-session. Tooling, not engine.
