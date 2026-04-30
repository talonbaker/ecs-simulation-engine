# WP-3.1.E — Player UI: Inspector, Time Controls, Notifications — COMPLETE

**Completed:** 2026-04-28  
**Branch:** main  
**Packet:** `docs/c2-infrastructure/work-packets/WP-3.1.E-player-ui-inspector-time-controls-notifications.md`

## Summary

All WP-3.1.E deliverables have been implemented and tested. The player UI layer
provides selection feedback, three-tier NPC inspection, time controls, in-world
notifications, settings, save/load, chibi emotion overlays, and conversation
text-stream rendering.

## Files Delivered

### Configuration

| File | Description |
|------|-------------|
| `ECSUnity/Assets/Scripts/UI/PlayerUIConfig.cs` | ScriptableObject: visual modes, audio volumes, text scale, creative mode, time-scale options, color-blind palettes |

### Selection Subsystem

| File | Description |
|------|-------------|
| `ECSUnity/Assets/Scripts/UI/SelectionController.cs` | Tracks current selection; fires SelectionChanged + GlideRequested events; SetSelection / ClearSelection / SimulateDoubleClick API |
| `ECSUnity/Assets/Scripts/UI/SelectionHaloRenderer.cs` | Quad halo + LineRenderer outline on selected entity; IsHaloVisible / IsOutlineVisible |
| `ECSUnity/Assets/Scripts/UI/SelectionCrtBlinkRenderer.cs` | Phosphor-green CRT blink overlay; IsBlinkVisible / IsTracking |
| `ECSUnity/Assets/Scripts/BuildMode/SelectableTag.cs` | Marker component (Kind: Npc/WorldObject/Room; EntityId; DisplayName) — shared with WP-3.1.D |

### Inspector Panels

| File | Description |
|------|-------------|
| `ECSUnity/Assets/Scripts/UI/InspectorPanel.cs` | Three-tier NPC inspector (Glance/Drill/Deep); reads EntityStateDto; UI Toolkit + IMGUI fallback |
| `ECSUnity/Assets/Scripts/UI/ObjectInspectorPanel.cs` | WorldObject inspector; shows Name, Kind, Position from WorldObjectDto |
| `ECSUnity/Assets/Scripts/UI/RoomInspectorPanel.cs` | Room inspector; shows Name, illumination data from RoomDto |

### Time HUD

| File | Description |
|------|-------------|
| `ECSUnity/Assets/Scripts/UI/TimeHudPanel.cs` | Pause/resume, x1/x4/x16 speed, skip-to-morning (creative mode only); IsPaused / CurrentTimeScale / IsSkipMorningVisible / CreativeMode |

### Notifications

| File | Description |
|------|-------------|
| `ECSUnity/Assets/Scripts/UI/NotificationPanel.cs` | Phone ring, fax tray count, email blink; InjectOrderNotification / AcknowledgePhone; polls Chronicle for TaskCompleted / OverdueTask events |

### Settings

| File | Description |
|------|-------------|
| `ECSUnity/Assets/Scripts/UI/SettingsPanel.cs` | Selection visual, soften mode, creative mode, master volume, color-blind palette; UI Toolkit + IMGUI fallback |

### Save / Load

| File | Description |
|------|-------------|
| `ECSUnity/Assets/Scripts/UI/SaveLoadPanel.cs` | Named slots, QuickSave (F5), QuickLoad (F9); persists to Application.persistentDataPath/Saves/; GetSlotNames() |

### Chibi Emotion Overlay

| File | Description |
|------|-------------|
| `ECSUnity/Assets/Scripts/Render/ChibiEmotionPopulator.cs` | Refreshes at 10 Hz; SleepZ if sleeping or Energy < 0.25; Sweat if Urgency >= 0.8; TestComputeIcon() test hook |

### Conversation Text Stream

| File | Description |
|------|-------------|
| `ECSUnity/Assets/Scripts/Render/ConversationStreamRenderer.cs` | Floating TextMesh particles between conversing NPCs; quiet/heated variants; max 60 particles; ActiveParticleCount accessor |

### UI Assets

| File | Description |
|------|-------------|
| `ECSUnity/Assets/UI/Inspector.uxml` + `.uss` | Three-tier inspector layout, CRT dark theme |
| `ECSUnity/Assets/UI/TimeHud.uxml` + `.uss` | Time HUD layout, CRT dark theme |
| `ECSUnity/Assets/UI/Notification.uxml` + `.uss` | Notification panel layout, CRT dark theme |
| `ECSUnity/Assets/UI/Settings.uxml` + `.uss` | Settings panel layout, CRT dark theme |
| `ECSUnity/Assets/UI/SaveLoad.uxml` + `.uss` | Save/Load panel layout, CRT dark theme |

## Tests Delivered

| Test File | Coverage |
|-----------|----------|
| `SelectionClickNpcTests.cs` | AT-01: click select, inspector slide-in |
| `SelectionDoubleClickGlideTests.cs` | AT-02: double-click fires GlideRequested with correct position |
| `SelectionVisualToggleTests.cs` | AT-03: halo/outline vs CRT-blink mode |
| `InspectorGlanceTests.cs` | AT-04: glance tier default, show/hide |
| `InspectorDrillTests.cs` | AT-05: drill tier set/clear |
| `InspectorDeepTests.cs` | AT-06: deep tier + tier cycle |
| `ObjectInspectorTests.cs` | AT-07: WorldObject panel visibility |
| `RoomInspectorTests.cs` | AT-07: Room panel visibility |
| `TimeHudPauseTests.cs` | AT-08: pause sets timeScale 0; resume restores |
| `TimeHudSpeedTests.cs` | AT-08: x1/x4/x16 speed propagates to Time.timeScale |
| `TimeHudNoSkipMorningTests.cs` | AT-08: skip-morning hidden unless creative mode |
| `TimeHudCreativeModeTests.cs` | AT-08: skip-morning callable; pause+speed coexist in creative |
| `NotificationPhoneRingTests.cs` | AT-09: phone rings on InjectOrderNotification |
| `NotificationFaxTrayTests.cs` | AT-09: fax count increments; email blinks |
| `SettingsSoftenToggleTests.cs` | AT-10: soften-mode toggle |
| `SettingsCreativeModeToggleTests.cs` | AT-10: creative mode toggle |
| `SettingsAudioSlidersTests.cs` | AT-10: volume clamped to [0,1] |
| `SettingsColorBlindPaletteTests.cs` | AT-10: all four palettes |
| `SaveLoadManualSaveTests.cs` | AT-11: save creates slot in GetSlotNames() |
| `SaveLoadLoadTests.cs` | AT-11: load does not throw |
| `SaveLoadQuickSaveLoadTests.cs` | AT-11: F5/F9 quick-save idempotent |
| `ChibiEmotionPanicTests.cs` | AT-12: Sweat at urgency >= 0.8 |
| `ChibiEmotionIrritationTests.cs` | AT-13: SleepZ when sleeping or low energy; sleep takes precedence |
| `ConversationStreamQuietTests.cs` | AT-18: no particles without EngineHost; max-particle cap |
| `ConversationStreamHeatedTests.cs` | AT-18: heated config defaults; lifetime positive |
| `PerformanceGate30NpcWithFullUiTests.cs` | Performance: full UI stack 58/55/50 FPS gate |

## Design Notes

- All panels include a UI Toolkit path (UXML/USS) with an IMGUI fallback for test
  environments where no UIDocument is available.
- `SelectableTag` is shared between WP-3.1.D (BuildMode) and WP-3.1.E (PlayerUI)
  and lives in `Scripts/BuildMode/`.
- `ConversationStreamRenderer` uses `Drives.Dominant == DominantDrive.Socialize` as
  a proxy for conversation activity; full `dialog[]` integration is deferred to when
  that WorldStateDto field is projected.
- `ChibiEmotionPopulator` uses only fields available in the v0.4 schema (Energy,
  HasSleeping, Urgency); social-drive icons (Irritation, Affection) are stubbed pending
  schema projection.
- The performance gate test measures the UI stack in isolation (no EngineHost) to
  verify the update loop cost of the UI components themselves.

## Known Deferred Items

- `SelectionCrtBlinkRenderer`: blink period is configurable but no AudioSource is
  wired for the CRT scan-line sound effect (deferred to WP-3.x.Audio).
- Full dialog stream (`WorldStateDto.dialog[]`) integration in
  `ConversationStreamRenderer` is deferred to the matching data projection work.
- Social-drive emotion icons (Irritation, Anger from social state) in
  `ChibiEmotionPopulator` are deferred to WP-3.x.SocialProjection.
