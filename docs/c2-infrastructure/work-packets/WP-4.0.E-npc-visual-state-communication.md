# WP-4.0.E — NPC Visual State Communication

> **Wave 3 of the Phase 4.0.x foundational polish wave.** Per the 2026-05-02 brief restructure: "Animation states from 3.2.6 are defined but not reading. Polish pass: emotion cues, activity legibility, two-second-glance answer to 'what is this person doing right now.'" Now dispatchable: A1-INT shipped the pixel-art look into production scenes 2026-05-03; this packet polishes NPC visual communication *under that aesthetic*. Track 2 sandbox + companion `-INT` packet.

**Tier:** Sonnet
**Depends on:** WP-4.0.A (URP, merged), WP-4.0.A1 (pixel-art Renderer Feature, merged), WP-4.0.A1-INT (pixel-art live in production scenes, merged), WP-3.2.6 (silhouette animation states), WP-3.1.B (silhouette renderer + animator).
**Parallel-safe with:** WP-4.0.D (floor identity — disjoint render surface), WP-4.0.H (particles — disjoint surface), WP-4.0.G1 (build mode undo/redo — disjoint), WP-PT.* (does not modify production scenes; sandbox-first).
**Timebox:** 150 minutes
**Budget:** $0.70
**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN (post-`-INT`):** can Talon answer "what is this NPC doing right now" in two seconds without clicking? Are the chibi emotion cues visible at intended camera altitude under pixel-art shader? Are state transitions (e.g., walking → coughing fit) visually clear?

---

## Goal

NPCs currently animate through their existing state vocabulary (Idle, Walking, Eating, Drinking, Working, Crying, CoughingFit, Heimlich from WP-3.2.6) but the states don't read clearly to a player watching the office. Talon's diagnostic from 2026-05-02: "NPCs barely display anything meaningful." The pixel-art shader (Chunky 320×180 internal resolution) further compresses the per-NPC pixel budget.

This packet is a **polish + legibility pass** on the existing NPC visual surface. Three concerns, in order of weight:

1. **Activity legibility.** A glance at an NPC tells the player what activity that NPC is doing. Sitting-at-desk-and-working is visually distinct from sitting-at-desk-and-eating. Standing-and-talking is distinct from standing-and-thinking. Walking-with-purpose is distinct from wandering-idle.
2. **Emotion cue visibility under pixel-art.** Chibi emotion vocabulary from UX bible §1.6 / §3.8 (anger lines, red-face flush, green-face nausea, sweat drops, sleep-Z's, hearts, sparkles, exclamation, question mark) must remain visible at intended camera altitudes when the pixel-art shader is on. Cues that are unreadable get redesigned (larger, higher contrast, or repositioned).
3. **State transition clarity.** When an NPC transitions states (walking → coughing fit, eating → choking, working → standing-up-irritated), the transition reads cleanly rather than as a single-frame snap.

After this packet:
- The eight existing animation states from 3.2.6 read distinctly at intended camera altitudes.
- Six chibi emotion cues are visible: anger lines, sweat drops (most-used pair), sleep-Z's, red-face flush, green-face nausea, plus one event-cue (exclamation OR sparkles OR question mark — whichever reads best as a "something just happened" beat).
- NPC state transitions use 2-3-frame interpolation (not snap) where it improves legibility.
- A `NpcVisualStateCatalog.json` content file describes per-state visual treatment (frame timing, accent color, cue affinity) — modder-extensible per MAC-004.
- Sandbox scene demonstrates all states + cues side-by-side under pixel-art shader for at-a-glance comparison.

The 30-NPCs-at-60-FPS gate holds. Chibi cue rendering is sprite-based and batched; the cost is bounded.

---

## Reference files

- `docs/UNITY-PACKET-PROTOCOL.md` — read in full. Sandbox-first per Rule 2.
- `docs/c2-content/ux-ui-bible.md` §1.6 (iconography vocabulary), §3.8 (emotional & environmental iconography in detail). The chibi cue families and the "subtle, near the head, NPC continues their activity" rule are load-bearing.
- `docs/c2-content/aesthetic-bible.md` — chibi-anime + pixel-art commitments.
- `docs/c2-content/cast-bible.md` — silhouette catalog locked in 3.1.B; per-archetype distinguishing marks.
- `docs/c2-infrastructure/MOD-API-CANDIDATES.md` — MAC-004 (animation state vocabulary). This packet bumps MAC-004 from *fresh* to *stabilizing* by adding a second consumer (the polish layer) and a content catalog.
- `ECSUnity/Assets/Scripts/Render/NpcSilhouetteRenderer.cs` — read in full. The per-NPC sprite + animation playback pipeline.
- `ECSUnity/Assets/Scripts/Render/SilhouetteAnimator.cs` (or equivalent) — read for state machine + frame timing.
- `ECSUnity/Assets/Scripts/Render/ChibiEmotionPopulator.cs`, `ChibiEmotionSlot.cs` — read for the existing chibi cue rendering pipeline. **This is the surface this packet polishes.**
- `ECSUnity/Assets/Sprites/Silhouettes/` — read the existing sprite inventory. New cue sprites land here; new state sprites if needed.
- `ECSUnity/Assets/Settings/SilhouetteAssetCatalog.asset` — the asset catalog. New cue assets register here.
- `APIFramework/Components/AnimationStateComponent.cs` — read for the state enum.
- `APIFramework/Components/ChibiEmotionCueComponent.cs` (or whichever component drives cue selection) — read for the cue-selection logic.

---

## Non-goals

- Do **not** add new animation states beyond the existing eight. WP-4.1.x or future packets handle expansion (Sleeping, Vomiting, Mourning, Conspiring per UX bible §6).
- Do **not** modify the silhouette catalog itself (per-archetype body / hair / item silhouette inventory). That's content; this packet is presentation polish.
- Do **not** add per-archetype variation in cue rendering. v0.2 is uniform; per-archetype variation is future depth.
- Do **not** ship a final art-pipeline pass. This is polish on the existing placeholder silhouette + chibi sprites; final hand-drawn art is WP-4.1.2.
- Do **not** modify pathfinding or movement timing. This is purely visual.
- Do **not** modify the engine's animation state machine logic. The state component is the source of truth; this packet polishes how the host *renders* state changes.
- Do **not** add audio cues coupled to state transitions. Audio is the SoundTriggerBus's job (WP-3.2.1); coordinate with whatever audio ships next, don't duplicate.
- Do **not** modify the Inspector or notification panel. Those are HUD; this packet is diegetic-side.
- Do **not** add UI-overlay state labels (the WARDEN-only introspection overlay from WP-4.0.F handles dev-mode "what is this NPC doing" — this packet ensures the player can answer it WITHOUT the overlay).
- Do **not** localize cue text or descriptions. Cues are visual only.

---

## Design notes

### Activity legibility per state

Each existing state gets a per-state visual checklist that the Sonnet validates in the sandbox:

| State | Recognition cue (must be visible at altitude 12-25) |
|:---|:---|
| `Idle` | Subtle weight shift; standing pose; no motion-blur |
| `Walking` | Forward lean + arm swing; clearly directional |
| `Eating` | Hand-to-mouth motion; visible food item; head tilt |
| `Drinking` | Hand-to-mouth with cup silhouette; head tilt |
| `Working` | Seated at workstation; subtle arm motion (typing/writing); slight head bob |
| `Crying` | Head down; hand to face OR shoulder shake; sweat-drop or tear cue active |
| `CoughingFit` | Body shake; hand to mouth; cough cue (sound trigger AND visual particle) |
| `Heimlich` | Two NPCs proximate; rescuer behind patient; arm-around-torso pose |

The Sonnet doesn't ship new sprite frames where the existing ones suffice; it ships:
- Frame timing adjustments (per-state frame durations in the animator)
- Pose tweaks where existing frames don't read distinctly
- Where genuinely needed, ONE additional frame per state (max 8 new frames total across all states)

### Chibi emotion cue visibility under pixel-art

The pixel-art shader at Chunky 320×180 is the test condition. Each chibi cue gets validated at three altitudes (close: 8m, default: 15m, far: 25m):

| Cue | Acceptable at 8m? | Acceptable at 15m? | Acceptable at 25m? |
|:---|:---|:---|:---|
| Anger lines (forehead) | yes | yes (default target) | acceptable to fade to invisible |
| Sweat drops | yes | yes | acceptable to fade |
| Sleep-Z's | yes | yes | yes — must stay visible (low-priority background ambient) |
| Red-face flush | yes | yes | yes |
| Green-face nausea | yes | yes | yes |
| Hearts | yes | yes | acceptable to fade (rare, earned) |
| Sparkles | yes | yes | acceptable to fade |
| Exclamation | yes | yes | yes — must stay visible (event beat) |
| Question mark | yes | yes | acceptable to fade |

For cues that fail at default 15m altitude under the pixel-art shader, the fix is one or more of:
- Larger sprite (+25% to +50%)
- Higher contrast (saturated palette accent vs background)
- Repositioned (moved further from the silhouette so it doesn't visually merge)
- Animated (single-pixel pulse or fade-in to draw attention)

### State transition smoothing

Currently transitions are likely instant (frame snap). For visually significant transitions (walking → CoughingFit, eating → standing-irritated, etc.), add 2-3 intermediate frames. The transition lookup is per-pair (from-state, to-state); only authored for visually significant pairs, default to snap for the rest.

The animator's transition table lives in `SilhouetteAnimator.cs` (or wherever the state machine lives). Add a small `TransitionFrameCatalog` keyed by `(fromState, toState)` returning a list of intermediate frame indices.

### `NpcVisualStateCatalog.json`

New content file at `docs/c2-content/animation/visual-state-catalog.json`:

```jsonc
{
  "schemaVersion": "0.1.0",
  "states": [
    {
      "stateId": "Idle",
      "frameDurationMs": 200,
      "accentColor": "#a0a0a0",
      "cueAffinity": ["sleep-z", "question-mark"],
      "description": "Standing weight shift; subtle motion."
    },
    {
      "stateId": "Walking",
      "frameDurationMs": 120,
      "accentColor": "#c0c0c0",
      "cueAffinity": [],
      "description": "Forward lean + arm swing; clearly directional."
    },
    {
      "stateId": "Eating",
      "frameDurationMs": 250,
      "accentColor": "#d4a875",
      "cueAffinity": ["sparkles", "exclamation"],
      "description": "Hand-to-mouth motion; visible food item; head tilt."
    },
    // ...all 8 states
  ],
  "cues": [
    {
      "cueId": "anger-lines",
      "spriteAsset": "cue_anger_lines.png",
      "anchorOffset": [0.0, 1.6, 0.0],
      "fadeAltitudeStart": 25.0,
      "fadeAltitudeEnd": 35.0,
      "minScaleMult": 1.0
    },
    {
      "cueId": "sweat-drop",
      "spriteAsset": "cue_sweat_drop.png",
      "anchorOffset": [0.3, 1.5, 0.0],
      "fadeAltitudeStart": 25.0,
      "fadeAltitudeEnd": 35.0,
      "minScaleMult": 1.25
    },
    // ...all cues
  ],
  "transitions": [
    {
      "from": "Walking",
      "to": "CoughingFit",
      "intermediateFrames": [12, 13, 14],
      "totalDurationMs": 360
    },
    {
      "from": "Eating",
      "to": "CoughingFit",
      "intermediateFrames": [8, 9],
      "totalDurationMs": 240
    },
    // ...other significant pairs
  ]
}
```

The catalog is Mod API-friendly (JSON-driven, schema-versioned, additive). Modders adding a sneeze state and a sneeze cue extend the catalog without code changes. This is a key reason this packet bumps MAC-004 from *fresh* to *stabilizing* — second consumer of the animation state vocabulary, with a data-driven extension surface.

### Sandbox scene

`Assets/_Sandbox/npc-visual-state.unity`:
- Eight NPCs lined up in a row, one per state. Labels above each (sandbox debug labels, not in-game UI).
- A second row: same eight NPCs but with various chibi cues active (each NPC has a different cue).
- A camera that lets Talon zoom from 8m to 25m altitude to validate cue visibility at each level.
- A control panel UI (sandbox-only) with buttons:
  - "Play state transitions" — cycles each NPC through Idle → Walking → state → Idle to validate transition smoothness.
  - "Toggle pixel-art" — enables/disables the production renderer feature (via the sandbox renderer-data variant from A1) so Talon can A/B compare under-shader vs over-shader rendering.
  - Per-altitude buttons (8m / 15m / 25m) for camera-snap.

Test recipe walks Talon through each state + cue + transition systematically.

### Performance

Sprite-based rendering is cheap. With 30 NPCs × ~2 active cues each = 60 sprite slots — well within URP's batching budget. Verify with the existing FPS-gate test variants.

### Sandbox vs integration

- **WP-4.0.E (this packet):** sandbox scene + new content catalog + script changes. Production scenes unchanged.
- **WP-4.0.E-INT** (companion, drafted later): integrate the new catalog and adjusted scripts into PlaytestScene + MainScene. Talon's Inspector hands wire the catalog asset into the scene's `SilhouetteAnimator` controller.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `ECSUnity/Assets/Scripts/Render/SilhouetteAnimator.cs` (modification) | Loads transition catalog; applies per-state frame timing; supports intermediate-frame transitions. |
| code | `ECSUnity/Assets/Scripts/Render/ChibiEmotionPopulator.cs` (modification) | Reads cue catalog for fade-altitude, scale-mult, anchor-offset. |
| code | `ECSUnity/Assets/Scripts/Render/NpcVisualStateCatalogLoader.cs` (new) | Loads the JSON catalog at boot. |
| data | `docs/c2-content/animation/visual-state-catalog.json` | The catalog. All 8 states, ~9 cues, ~6 significant transitions. |
| sprites | `ECSUnity/Assets/Sprites/Silhouettes/cue_*.png` (additive) | Adjusted/new chibi cue sprites where existing ones fail visibility validation. |
| asset | `ECSUnity/Assets/Settings/SilhouetteAssetCatalog.asset` (modification) | New cue assets registered. |
| scene | `ECSUnity/Assets/_Sandbox/npc-visual-state.unity` | Sandbox per Rule 4. |
| doc | `ECSUnity/Assets/_Sandbox/npc-visual-state.md` | 10-15 minute test recipe. |
| test | `APIFramework.Tests/Animation/VisualStateCatalogJsonTests.cs` | JSON validation. |
| test | `ECSUnity/Assets/Tests/Edit/NpcVisualStateCatalogLoaderTests.cs` | Catalog loads correctly; missing states/cues handled gracefully. |
| test | `ECSUnity/Assets/Tests/Play/AnimatorTransitionFramesTests.cs` | Transitions use catalog's intermediate frames. |
| test | `ECSUnity/Assets/Tests/Play/ChibiEmotionFadeAltitudeTests.cs` | Cue fades correctly at altitude bounds. |
| test | `ECSUnity/Assets/Tests/Play/PerformanceGate30NpcWithCuesTests.cs` | 30 NPCs with cues active hold ≥ 60 FPS. |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Bump MAC-004 from *fresh* to *stabilizing*; document this packet as second consumer + the catalog as the modder extension surface. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | All 8 states have catalog entries with valid frame timing + accent color. | unit-test |
| AT-02 | All 9 chibi cue families have catalog entries with sprite asset + fade-altitude. | unit-test |
| AT-03 | Catalog round-trips through JSON loader; missing optional fields fall back to defaults. | unit-test |
| AT-04 | Significant state transitions (per Design notes table) use intermediate frames per catalog. | unit-test |
| AT-05 | Each state reads distinctly at default 15m altitude under pixel-art shader. | manual visual |
| AT-06 | Each cue is visible per the per-altitude table in Design notes. | manual visual |
| AT-07 | State transitions read smoothly (no jarring snap on significant transitions). | manual visual |
| AT-08 | 30 NPCs with active cues hold ≥ 60 FPS. | play-mode test |
| AT-09 | Talon can answer "what is this NPC doing right now" in two seconds for each of the 8 states without clicking, in the sandbox scene. | manual visual |
| AT-10 | All Phase 0–3 + Phase 4.0.A/A1/A1-INT/B/C/F/G tests stay green. | regression |
| AT-11 | `dotnet build` warning count = 0; `dotnet test` + Unity test runner all green. | build + test |
| AT-12 | MAC-004 entry in `MOD-API-CANDIDATES.md` updated with second-consumer note. | review |

---

## Mod API surface

This packet is the **second consumer of MAC-004** (animation state vocabulary). MAC-004 graduates from *fresh* (1 consumer in 3.2.6) to *stabilizing* (2 consumers — 3.2.6 substrate + this polish layer).

The new `visual-state-catalog.json` is itself a candidate Mod API extension surface — modders add new states + cues + transitions by extending the JSON. Append a new MAC entry (MAC-013: NPC visual state catalog JSON) to `MOD-API-CANDIDATES.md` reflecting this. Pattern is consistent with MAC-001 (per-archetype tuning) and MAC-005 (SimConfig) — data-driven extension.

The cue rendering pipeline (sprite-based, fade-altitude-aware, anchor-offset-driven) is now well-defined enough that future cue additions don't require code changes. This is the kind of incremental API maturation SRD §8.8 calls for.

---

## Followups (not in scope)

- `WP-4.0.E-INT` — integrate catalog + scripts into PlaytestScene + MainScene. Talon's hands.
- New animation states (Sleeping, Vomiting, Mourning, Conspiring per UX bible §6). Future content packets.
- Per-archetype variation in cue rendering (the Cynic gets darker sweat drops, the Vent gets larger anger lines). Future depth.
- Final hand-drawn art pipeline. WP-4.1.2.
- Conversation visualization specifics (text-stream rises between conversers per UX bible §3.8). Separate visual surface; future packet.
- Cue + audio coupling (a sweat drop appearing should optionally fire a soft "stress" sound trigger). Coordinate with audio packets.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

Track 2 sandbox packet. Visual verification by Talon required.

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm worktree at `.claude/worktrees/sonnet-wp-4.0.e/` on branch `sonnet-wp-4.0.e` based on recent `origin/staging` (which now includes URP + pixel-art + F + G).
1. Implement the spec.
2. Run all Unity tests + `dotnet test`. All must stay green.
3. Stage all changes including self-cleanup.
4. Commit on the worktree's feature branch.
5. Push the branch.
6. Stop. Notify Talon: `READY FOR VISUAL VERIFICATION — run Assets/_Sandbox/npc-visual-state.md (10-15 min recipe)`.

### Feel-verified-by-playtest acceptance flag

**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN (post-`-INT`):** can Talon answer "what is this NPC doing" in two seconds without the introspection overlay (WP-4.0.F)? Are cues visible at typical play camera altitudes? Do transitions read clean?

### Cost envelope

Target: **$0.70**. Sandbox + content catalog + per-cue tuning. If cost approaches $1.20, escalate via `WP-4.0.E-blocker.md`.

Cost-discipline:
- Don't author pixel-perfect new sprites — the existing sprite library plus modest tweaks (size, contrast) should cover most cue-visibility issues. New sprites only where absolutely needed.
- Don't iterate the catalog values endlessly — reasonable starting values; Talon iterates in playtest.

### Self-cleanup on merge

Standard. Check for `WP-4.0.E-INT` as likely dependent; retain spec if pending.
