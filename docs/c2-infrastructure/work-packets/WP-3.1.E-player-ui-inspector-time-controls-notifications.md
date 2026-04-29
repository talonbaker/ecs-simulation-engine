# WP-3.1.E — Player UI: Inspector, Time Controls, Notifications, Selection

> **DO NOT DISPATCH UNTIL WP-3.1.A IS MERGED.**
> This packet builds the bulk of the UX/UI bible's player-facing surfaces on top of the 3.1.A scaffold. The Unity project, `EngineHost`, camera, and input system must already exist. **Reads UX/UI bible v0.1 (`docs/c2-content/ux-ui-bible.md`) as the load-bearing contract.**
>
> **Soft prerequisite: WP-3.1.B silhouettes recommended but not strictly required.** The selection visual cue (halo + outline OR CRT-blinking-box) anchors to NPCs which are dots without 3.1.B and silhouettes with it. If 3.1.B has not merged, the cue still works on dots; visual polish improves once silhouettes land.

**Tier:** Sonnet
**Depends on:** WP-3.1.A (Unity scaffold), UX/UI bible v0.1
**Parallel-safe with:** WP-3.1.B (silhouettes, recommended-merged-first but not blocking), WP-3.1.C (lighting), WP-3.1.D (build mode — different verb space, different UI panel), WP-3.1.F (JSONL stream — backend, no UI overlap)
**Timebox:** 180 minutes (largest UI surface area)
**Budget:** $0.80

---

## Goal

The bulk of what the player *sees and operates*. After this packet:

- **Selection** works. Click an NPC → halo+outline appears under them and the inspector slides in from the right. Click an object → object inspector opens. Click a room → room inspector opens. Double-click → camera glides to focus on the target. The CRT-blinking-box selection cue is shipped as an alternative visual (settings toggle).
- **Inspector** has the three-tier disclosure (glance / drill / deep) per UX bible §3.1. Glance is icon-heavy, ~5 fields. Drill adds drives, willpower, schedule, task, stress, mask. Deep adds full vectors, relationships, memory entries, intent.
- **Time HUD** sits top-right with pause / ×1 / ×4 / ×16 buttons + clock readout. **No skip-to-morning, no time-zoom.** Those are creative-mode-only (UX §5.2).
- **Notification surface** is sparse and diegetic. v0.1 ships a *placeholder* — a small in-world manager-office overlay with a fax tray + phone + email indicator (per UX §3.2 leaning + §3.4 leaning) — that fires on player-direct order events. Final shape lands in v0.2 of the bible. The placeholder works for orders that the engine emits; the volume stays low.
- **Settings panel** opens via a small gear icon. Toggles: soften mode (UX §4.6), creative mode (UX §5.2 — free camera, skip-to-morning, time-zoom unlocks), audio sliders (per-channel), text scaling, sticky controls, color-blind palette.
- **Save/load UI** opens via a save icon. Named-slot file picker. Manual save / quick-save / autosave list. Confirm before load. (The engine save/load logic exists from earlier phases; this packet wires the UI.)
- **Chibi-emotion overlays** populate the slot 3.1.B ships. NPCs in elevated drive states show small icons near the head — anger lines, sweat drops, sleep-Z's, hearts (when applicable), etc.
- **Conversation visualization** (text streams between conversing NPCs, scaled by register) per UX §3.8. Quiet talk = small slow gray text; heated = larger faster colored.
- **Mature content** opt-out (soften toggle) wired and respected by the engine (death body fades after 1 game-hour; explicit content blurs).

This is the player's primary UI. Get it right; subsequent UI packets (3.1.G event log, 3.1.H dev console) are smaller in scope.

---

## Reference files

- `docs/c2-content/ux-ui-bible.md` — **read in full.** Every section relevant. Especially §1 (axioms), §2 (verbs), §3.1 (inspector), §3.2 (notifications — open question), §3.4 (save/load), §3.6 (time HUD), §3.7 (audio — partially this packet, partially future), §3.8 (chibi + environmental + conversation iconography), §4.1 (HUD allocation), §4.6 (soften toggle), §5 (game modes).
- `docs/c2-infrastructure/work-packets/WP-3.1.A-unity-scaffold-and-baseline-render.md` — what's on disk: camera, input system, base canvas (if any).
- `docs/c2-infrastructure/work-packets/WP-3.1.B-silhouette-renderer-and-animator.md` — the chibi slot (`ChibiEmotionSlot.Show(IconKind)`) this packet populates.
- `docs/c2-content/world-bible.md` — narrative tone; mature content baselines; named anchors.
- `docs/c2-content/cast-bible.md` — silhouettes; archetype names; relationships display priority.
- `docs/c2-content/dialog-bible.md` — corpus fragments. Conversation visualization may surface fragments as floating text.
- `APIFramework/Components/SocialDrivesComponent.cs`, `WillpowerComponent.cs`, `StressComponent.cs`, `MoodComponent.cs`, `SocialMaskComponent.cs`, `WorkloadComponent.cs`, `ScheduleComponent.cs`, `RelationshipComponent.cs`, `PersonalMemoryComponent.cs`, `RelationshipMemoryComponent.cs`, `IntendedActionComponent.cs` — what the inspector reads.
- `APIFramework/Systems/Narrative/NarrativeEventBus.cs` — the source of player-facing notifications (orders coming in fire as a narrative event the UI subscribes to).
- `Warden.Telemetry/Projectors/*` — `WorldStateDto.npcs[]` (mood, drives, etc.), `WorldStateDto.events[]` (recent narrative events for the notification surface).

---

## Non-goals

- Do **not** lock the notification surface design. v0.2 of the bible revisits this. Ship a placeholder that works for sparse player-direct orders.
- Do **not** ship the player-facing event log / chronicle reader. WP-3.1.G.
- Do **not** ship the developer console. WP-3.1.H.
- Do **not** ship multi-select. v0.1 single-select per UX §2.2.
- Do **not** ship the floor-switch verb. Single floor v0.1.
- Do **not** ship hardcore-mode toggle / functionality. UX §5.3 future.
- Do **not** ship a tutorial / first-launch experience.
- Do **not** modify any engine surface (Phase 0/1/2/3.0.x). UI is read-only on engine state; mutations route through `IWorldMutationApi` (build mode is 3.1.D, not this packet).
- Do **not** introduce a runtime LLM call. (SRD §8.1.)
- Do **not** retry, recurse, or "self-heal."

---

## Design notes

### Selection system

`SelectionController` (MonoBehaviour) owns the current selection.

- Single-click on a Unity collider with `SelectableTag` → set selection, fire `SelectionChanged` event.
- Double-click → set selection AND request camera glide to target (camera handles the smooth interpolation; bible §2.1).
- Click empty space → clear selection.

`SelectableTag` is added to:
- NPC silhouette parents (or NPC dots if 3.1.B not merged).
- Object renderers (chairs, desks, fridge, named anchors).
- Room rectangles.

### Selection visual cue

Two visualisations, ship both, expose toggle in settings:

**Default — halo + outline.**
- Halo: `SelectionHaloRenderer` MonoBehaviour. A flat circle on the ground tile under the selected entity, soft white, 0.8 alpha. Created on selection, destroyed on deselection.
- Outline: `SelectionOutlineRenderer`. Adds a thin white outline around the selected entity's silhouette (or dot). Implementation: outline shader (separate material with `_OutlineWidth` + `_OutlineColor`).

**Alternative — CRT-blinking-box.**
- A flat rectangular outline framing the selected entity, blinking at terminal-cursor cadence (~500ms on, 500ms off). Color: phosphor green (`#00FF00` muted to `#3CB371`).
- Setting toggle in settings panel: `selectionVisualMode = "halo+outline" | "crt-blink"`.

### Inspector

`InspectorPanel` (UI Toolkit document, slides in from right).

**Glance tier (default):**
- Name (e.g., "Donna").
- Activity (e.g., "Heading to women's bathroom").
- Mood — one chibi icon + one-word label (e.g., 😤 frustrated; 😴 exhausted). Pull from existing `MoodComponent` axes.
- One contextual fact (e.g., "Overdue: 1 task" or "In conversation with Frank").
- A magnifying-glass "drill" button.

**Drill tier (one click deeper):**
- Top 3 drives with bar-graph values (irritation, affection, suspicion, etc.).
- Willpower (current / max).
- Schedule block (e.g., "AtDesk until 12:00").
- Active task or none.
- Stress (acute / chronic with source breakdown).
- Mask state (felt vs performed gap if any).

**Deep tier (one more click):**
- Full drive vector.
- Inhibition vector.
- Personality (Big Five values).
- Sortable relationships list.
- Persistent memory entries (recent N).
- Current/pending intended action.

The panel reads `WorldStateDto.npcs[selectedId]` per render frame.

### Object inspector

When selection is an object (chair, fridge, microwave, named anchor):
- Name (e.g., "The Microwave (first-floor breakroom)").
- Description (from named-anchor data).
- Current state (e.g., "Stained, 4 days overdue for cleaning").
- Who interacts (top-3 NPCs by interaction count).
- Persistent state (the smell, the notes).

### Room inspector

When selection is a room:
- Name and category.
- Current occupants.
- Lighting state.
- Named anchors in room.
- Persistent stains / hazards.

### Time HUD

`TimeHudPanel` (UI Toolkit, top-right corner, always visible).

Contents:
- Time readout: "Tuesday 2:47 PM"
- Day-of-week + day-number badge
- Speed buttons: ⏸ ▶ ▶▶ ▶▶▶ (pause, ×1, ×4, ×16). Click to set; number keys 1-4 also work; spacebar pauses.
- **No skip-to-morning, no time-zoom buttons in default mode.** Creative-mode-only — when creative mode is enabled in settings, two extra buttons appear: ⏭ (skip-to-morning) and ⏩ (variable-rate slider).

### Notification surface (v0.1 placeholder)

A small **manager-office overlay panel** anchored to the top-left or bottom-right of the screen (UX §3.2 leans hybrid; v0.1 leans manager's-office). Shows three diegetic indicators:

- **Phone** — rings (visual + audio cue) when an order narrative event fires. Player can click to "answer" (opens the order detail in a small modal — yes, technically a modal, but it's *the* exception per UX §4.4 because it represents a phone call, which is itself a moment of attention; bible v0.1 acknowledges this as a single permitted modal).
- **Fax tray** — fills (visual stack of pages grows) as orders accumulate. Click to view the queue.
- **Email indicator** — blinks when a new email arrives. Click to view.

Volume is **sparse**. v0.1's `OrderGenerator` (already in the engine from Phase 2 task generation, but reframed for player-orders) emits at most 1-2 player-direct orders per game-day. Final shape from the v0.2 bible recalibration.

The notification surface honors UX §1.5 (stress and lull from simulation rhythm): missed orders cascade through stress / mood / schedule disruption naturally; the UI does not punish the player.

### Settings panel

`SettingsPanel` (UI Toolkit, opens via gear icon).

Toggles + sliders:
- **Selection visual:** halo+outline (default) / CRT-blinking-box.
- **Soften mode:** off (default) / on. Engine respects: deceased entities fade after `softenedDeathFadeTicks` (default ~3000 ticks ≈ 1 game-hour); explicit content blurs.
- **Creative mode:** off (default) / on. Unlocks free camera, skip-to-morning, time-zoom, spawn-anything build palette.
- **Audio:** per-channel sliders (master, ambient, NPCs, UI). Default master 70%; ambient 60%; NPCs 80%; UI 50%.
- **Text scaling:** small / default (1.0×) / large (1.25×).
- **Sticky controls:** off (default) / on.
- **Color-blind palette:** default / deuteranopia / protanopia / tritanopia.
- **First-launch audio prompt:** done indicator. (Set on first run.)

### Save/load UI

`SaveLoadPanel` (UI Toolkit, opens via save icon).

- List of named save slots (manual saves) with timestamp + game-day.
- Autosave list: end-of-day (rotating 2 slots), build-mode-checkpoint (1 slot per night), periodic-autosave (1 rotating slot, 5-min cadence).
- Buttons: Save (creates a new slot, prompts for name), Load (with confirmation prompt), Delete.
- Quick-save: F5 hotkey. Quick-load: F9 hotkey (most recent autosave).

The engine's save/load already exists (WorldStateDto JSON round-trip per SRD §8.2). This packet wires UI to it.

### Chibi-emotion overlay population

For each NPC, read `MoodComponent` and `SocialDrivesComponent`. Compute the dominant emotion icon to display (or none). Logic:

- `MoodComponent.PanicLevel ≥ 0.5` → `IconKind.Sweat` (multiple drops if very high).
- `MoodComponent.GriefLevel ≥ 0.4` → `IconKind.Anger` (no — for grief, use a different icon; the slot vocab includes both Anger lines AND Sad-droopy. Verify with cast bible).
- `SocialDrivesComponent.Irritation ≥ 70` → `IconKind.Anger`.
- `SocialDrivesComponent.Affection (toward target in conversation range) ≥ 80` → `IconKind.Heart`.
- `EnergyComponent.Energy < 25` → `IconKind.SleepZ`.
- `IsChokingTag OR ChokingComponent` → `IconKind.Exclamation` (panic exclamation; Sweat layered).

Multiple icons can stack on a single NPC if relevant, but at most 2 to avoid clutter. Update per render frame.

### Conversation visualization

`ConversationStreamRenderer` watches `WorldStateDto.dialog[]` (or equivalent — the spoken-fragment stream from Phase 1.10.A). For each active conversation:
- Spawn a text-stream particle effect between the two participants.
- Sample fragments from the `DialogCorpusComponent` text content.
- Render as small floating gray letters/words rising upward, fading after 2-3 seconds.
- Scale by intensity:
  - Quiet (low register, low magnitude) → small (8pt), slow rise, gray.
  - Heated (high register, high magnitude) → larger (12-16pt), faster rise, color-shifted (red for anger, blue for sad).
- Mask-slip fragments → brief sharp `!?` punctuation at the moment of slip.

### Tests

- `SelectionClickNpcTests.cs` — click NPC silhouette/dot → selection set; halo+outline appears; inspector slides in.
- `SelectionDoubleClickGlideTests.cs` — double-click → camera glides to target.
- `SelectionVisualToggleTests.cs` — switch settings to CRT-blink → halo+outline replaced with blinking box.
- `InspectorGlanceTests.cs` — selected NPC; glance tier shows name, activity, mood, contextual fact.
- `InspectorDrillTests.cs` — click drill → drives, willpower, schedule, task, stress, mask.
- `InspectorDeepTests.cs` — click deep → full vectors, relationships, memory.
- `ObjectInspectorTests.cs` — click microwave → object inspector with name, description, persistent state.
- `RoomInspectorTests.cs` — click room → room inspector.
- `TimeHudPauseTests.cs` — click pause → engine ticks halted; spacebar same.
- `TimeHudSpeedTests.cs` — click ×4 → engine tick rate increases (verify via FixedUpdate frequency or clock advancement).
- `TimeHudNoSkipMorningTests.cs` — in default mode, no skip-to-morning button visible.
- `TimeHudCreativeModeTests.cs` — enable creative mode in settings → skip-to-morning button appears; click → clock advances to next 8 AM.
- `NotificationPhoneRingTests.cs` — emit a player-order narrative event → phone visually rings; click → opens order detail.
- `NotificationFaxTrayTests.cs` — multiple orders → fax tray fills.
- `SettingsSoftenToggleTests.cs` — enable soften mode → deceased entity fades after `softenedDeathFadeTicks`.
- `SettingsCreativeModeToggleTests.cs` — enable creative mode → camera altitude clamps removed; skip-to-morning visible; spawn-anything palette in build mode.
- `SettingsAudioSlidersTests.cs` — adjust master volume → AudioListener volume reflects.
- `SettingsColorBlindPaletteTests.cs` — switch to deuteranopia → UI palette swaps.
- `SaveLoadManualSaveTests.cs` — click Save, name slot → file written.
- `SaveLoadLoadTests.cs` — click Load on existing slot → engine state restored.
- `SaveLoadQuickSaveLoadTests.cs` — F5 / F9 hotkeys.
- `ChibiEmotionPanicTests.cs` — NPC with `IsChokingTag` → Sweat + Exclamation icons in slot.
- `ChibiEmotionIrritationTests.cs` — NPC with `Irritation ≥ 70` → Anger icon.
- `ConversationStreamQuietTests.cs` — two NPCs in conversation with low register → small slow gray text.
- `ConversationStreamHeatedTests.cs` — two NPCs with high register → large fast color-shifted text.
- `PerformanceGate30NpcWithFullUiTests.cs` — 30 NPCs + all UI active: FPS gate preserved.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `ECSUnity/Assets/Scripts/UI/SelectionController.cs` | Selection state owner. |
| code | `ECSUnity/Assets/Scripts/UI/SelectionHaloRenderer.cs` | Halo+outline cue. |
| code | `ECSUnity/Assets/Scripts/UI/SelectionCrtBlinkRenderer.cs` | CRT-blink cue. |
| code | `ECSUnity/Assets/Scripts/UI/InspectorPanel.cs` | Three-tier inspector. |
| code | `ECSUnity/Assets/Scripts/UI/ObjectInspectorPanel.cs` | Object inspector. |
| code | `ECSUnity/Assets/Scripts/UI/RoomInspectorPanel.cs` | Room inspector. |
| code | `ECSUnity/Assets/Scripts/UI/TimeHudPanel.cs` | Time HUD. |
| code | `ECSUnity/Assets/Scripts/UI/NotificationPanel.cs` | Manager-office overlay. |
| code | `ECSUnity/Assets/Scripts/UI/SettingsPanel.cs` | Settings. |
| code | `ECSUnity/Assets/Scripts/UI/SaveLoadPanel.cs` | Save/load. |
| code | `ECSUnity/Assets/Scripts/Render/ChibiEmotionPopulator.cs` | Drives the chibi slot from 3.1.B. |
| code | `ECSUnity/Assets/Scripts/Render/ConversationStreamRenderer.cs` | Floating text streams. |
| code | `ECSUnity/Assets/Scripts/UI/PlayerUIConfig.cs` | Tunables. |
| asset | `ECSUnity/Assets/UI/Inspector.uxml` + `.uss` | Inspector layout. |
| asset | `ECSUnity/Assets/UI/TimeHud.uxml` + `.uss` | Time HUD layout. |
| asset | `ECSUnity/Assets/UI/Notification.uxml` + `.uss` | Notification overlay. |
| asset | `ECSUnity/Assets/UI/Settings.uxml` + `.uss` | Settings panel. |
| asset | `ECSUnity/Assets/UI/SaveLoad.uxml` + `.uss` | Save/load panel. |
| asset | `ECSUnity/Assets/Sprites/ChibiIcons/*.png` | Chibi emotion icon sprites (placeholder art). |
| asset | `ECSUnity/Assets/Sprites/UIIcons/*.png` | CRT-style UI icons (gear, save, magnifier, phone, fax, email). |
| code | `SimConfig.cs` (modified) | Add `PlayerUIConfig` + `SoftenConfig`. |
| config | `SimConfig.json` (modified) | New sections. |
| test | `ECSUnity/Assets/Tests/Play/SelectionClickNpcTests.cs` | (and ~25 others, per Tests above) |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-3.1.E.md` | Completion note. SimConfig defaults. UX bible §3.2 placeholder shape (what was built; what stays open for v0.2). Performance measurements with full UI active. Selection visual final selection (which becomes default). Chibi icon sprite art status (placeholder vs final). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | Click NPC → selection set; halo+outline appears under NPC; inspector slides in. | play-mode test |
| AT-02 | Double-click NPC → camera glides smoothly to target. | play-mode test |
| AT-03 | Settings switch to CRT-blink → halo+outline gone, blinking box appears. | play-mode test |
| AT-04 | Inspector glance tier shows name, activity, mood, one fact. | play-mode test |
| AT-05 | Click drill → drives, willpower, schedule, task, stress, mask visible. | play-mode test |
| AT-06 | Click deep → full vectors + relationships + memory. | play-mode test |
| AT-07 | Object click → object inspector. Room click → room inspector. | play-mode test |
| AT-08 | Time HUD pause → engine halts. ×4 → tick rate increases. | play-mode test |
| AT-09 | In default mode, no skip-to-morning visible. | play-mode test |
| AT-10 | Enable creative mode → skip-to-morning + time-zoom appear; clicking skip-to-morning advances clock to next 8 AM. | play-mode test |
| AT-11 | Order narrative event → phone rings; fax tray fills; email blinks. | play-mode test |
| AT-12 | Soften toggle ON → deceased entities fade after `softenedDeathFadeTicks`. | integration test |
| AT-13 | Audio sliders adjust per-channel volume. | play-mode test |
| AT-14 | Color-blind palette swaps UI palette. | play-mode test |
| AT-15 | Save / load: write slot, restart, load slot → state restored byte-identical (or to within float-precision tolerance for non-deterministic Unity-side state). | integration test |
| AT-16 | F5 / F9 quick-save / quick-load. | play-mode test |
| AT-17 | Chibi icons populate based on NPC state (Panic, Anger, Sweat, SleepZ, Heart). | play-mode test |
| AT-18 | Conversation streams render between conversing NPCs; quiet small gray, heated large color-shifted. | play-mode test |
| AT-19 | **Performance gate.** 30 NPCs + full UI: min ≥ 55, mean ≥ 58, p99 ≥ 50. | play-mode test |
| AT-20 | All Phase 0/1/2/3.0.x and 3.1.A tests stay green. | regression |
| AT-21 | `dotnet build` warning count = 0; `dotnet test` all green. | build + test |
| AT-22 | Unity Test Runner: all tests pass. | unity test runner |

---

## Followups (not in scope)

- **v0.2 bible revisit of notification model (Q4).** This packet's manager-office overlay is a placeholder. Final shape lands in v0.2 once challenge-surface decision settles.
- **Multi-select.** Future polish.
- **Player embodiment / direct intervention (Q5/Q6).** UX bible open questions; future.
- **Hardcore-mode toggle wiring.** Future.
- **First-launch audio prompt UI.** First-time-installation polish; future.
- **Accessibility audit pass.** Full keyboard nav, screen reader. Future.
- **Tutorial / onboarding.** Out of scope per UX §6.
- **Subtitle option for SpeechFragments.** UX §4.5 lists; small follow-up.
- **Chibi icon final art.** Placeholder ships; final art is content/art-pipeline.
- **Inspector for relationship-pair entities.** Currently inspector is per-NPC; selecting a relationship row in the deep tier could open a per-pair inspector. Future.
- **Compare two NPCs side by side.** Multi-select adjacent. Future.
