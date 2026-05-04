# WP-4.0.J — Author Mode + Extended Build Palette

> **Wave 4 of the Phase 4.0.x foundational polish wave — authoring loop.** Per Talon's 2026-05-03 framing: "I need controls and means of level design and architecture." The existing build mode (4.0.G) places *gameplay* props (desks, chairs, walls, doors) one at a time. Author mode lifts gameplay-time restrictions and extends the palette to the **structural** elements that define a scene: rooms (rectangle drawing), light sources, light apertures (windows/skylights). Couples to WP-4.0.I's writer so authored scenes can be saved. Track 2 sandbox + companion `-INT` packet.

> **DO NOT DISPATCH UNTIL WP-4.0.I IS MERGED** — depends on `WorldDefinitionWriter` for save and the `placedProps` schema bump for round-trip.

**Tier:** Sonnet
**Depends on:** WP-4.0.I (world-definition writer + schema 0.2.0 — required), WP-4.0.G (build mode v2 + footprint substrate — required), WP-3.0.4 (`IWorldMutationApi` — extends with new room/light operations), WP-4.0.D (room visual identity — new rooms render with correct floor/wall materials).
**Parallel-safe with:** WP-4.0.K (NPC authoring — disjoint palette categories, additive to same UI), WP-4.0.L (docs).
**Timebox:** 180 minutes
**Budget:** $0.85
**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN:** Can Talon draw a new room and place lights inside it without leaving the running game? Does `save-world` then `reload-world` produce the same authored scene? Is the workflow legible enough that a community contributor could pick it up from the README?

---

## Goal

Today, "build mode" means: drag a desk to a free tile, place it. Useful at gameplay time, but insufficient for *level design* — a designer needs to draw rooms, place lighting, decide where windows go, sketch the topology before any furniture lands. Hand-editing `world-definition.json` is the current workaround; this packet replaces it with a live in-game tool.

This packet introduces **author mode** — a WARDEN-gated superset of build mode that:

1. **Lifts gameplay-time restrictions** that the player-facing build mode enforces (e.g., can't place during work hours per UX bible §3.5; can't place inside a locked room). In author mode all restrictions are lifted; the only constraints are physical (don't overlap solid props).
2. **Extends the palette** with three new categories beyond the existing four (Structural, Furniture, Props, NamedAnchor):
   - `Room` — rectangle-drag tool that creates a `RoomComponent` entity with chosen `RoomKind`. The new room picks up its floor/wall/door materials from the WP-4.0.D `RoomVisualIdentityCatalog`.
   - `LightSource` — point-place a light source (`overheadFluorescent`, `deskLamp`, `breakroomStrip`, …). After placement, an inline tuner UI lets author set kind, state (on/off/flickering/dying), intensity, color temperature.
   - `LightAperture` — point-place a window or skylight on a room boundary. After placement, an inline tuner UI lets author set facing direction + area.
3. **Adds an erase tool** that deletes any author-mode-placed entity (room, light, aperture, prop). Eraser respects undo/redo.
4. **Adds a "Save / Load / Reload" toolbar** wired to the WP-4.0.I dev-console commands (so author doesn't need to drop into the console for every save).

All mutations flow through `IWorldMutationApi` (extended in this packet with the room / light / aperture operations). Undo/redo from WP-4.0.G applies uniformly.

After this packet:
- Toggle author mode with `Ctrl+Shift+A` (WARDEN-only; no-op in retail builds).
- Author mode HUD shows: extended palette panel, save/load toolbar, mode banner ("AUTHOR MODE — recordings and gameplay-time gating disabled").
- A new room can be drawn by selecting `Room` category, picking a `RoomKind`, click-dragging a rectangle. Room appears with correct floor/wall/door materials (per 4.0.D).
- A light source can be placed by selecting kind from the LightSource sub-palette, clicking a tile. Inline tuner appears for state/intensity/temperature; sane defaults pre-filled.
- An aperture can be placed similarly.
- The eraser deletes any author-placed entity on click; confirms before deleting a room with contents.
- Save/Load/Reload toolbar buttons trigger the corresponding WP-4.0.I commands; status banner shows success/failure.
- The 30-NPCs-at-60-FPS gate holds during author mode (engine continues ticking; pause via existing pause control if desired).

---

## Reference files

- `docs/UNITY-PACKET-PROTOCOL.md` — sandbox-first per Rule 2.
- `docs/c2-infrastructure/MOD-API-CANDIDATES.md` — adds MAC-015 (extended build palette / author-mode tools as Mod API surface).
- `docs/c2-content/world-definitions/playtest-office.json` — the canonical scene this tool produces output equivalent to.
- `docs/c2-content/build-palette-catalog.json` — the existing palette JSON. Author mode's extended palette is layered on top; the existing player-facing palette is unchanged.
- `docs/c2-content/world-definitions/room-visual-identity.json` — WP-4.0.D's catalog. Author-drawn rooms read materials from here.
- `ECSUnity/Assets/Scripts/BuildMode/BuildModeController.cs` — read in full. Author mode is a controller-level mode flag; the existing controller hosts both modes.
- `ECSUnity/Assets/Scripts/BuildMode/BuildPaletteCatalog.cs` and `BuildPaletteUI.cs` — read in full. Extended categories layer in additively.
- `ECSUnity/Assets/Scripts/BuildMode/PlacementValidator.cs` — read in full. Author mode bypasses gameplay-time gating; physical-overlap validation still applies.
- `ECSUnity/Assets/Scripts/BuildMode/GhostPreview.cs` — read for ghost-rendering pattern; new tools (room-rect, light, aperture) extend with their own preview shapes.
- `APIFramework/Mutation/IWorldMutationApi.cs` — read in full. This packet extends with new operations.
- `APIFramework/Bootstrap/WorldDefinitionWriter.cs` (from WP-4.0.I) — for save toolbar wiring.
- `APIFramework/Components/RoomComponent.cs` — for `RoomKind` enum + bounds shape.
- `APIFramework/Components/Lighting/LightSourceComponent.cs` (or wherever it lives) — for light-source kind/state/intensity/temperature.

---

## Non-goals

- Do **not** make author mode available in retail builds. Strict WARDEN-only via `#if WARDEN` and Unity scripting define. Retail players never see the toggle.
- Do **not** ship undo/redo separately for author mode. Reuse WP-4.0.G's undo stack by implementing each new mutation as an `IUndoableMutation`.
- Do **not** ship an in-game JSON editor / inspector for the saved file. The file is human-readable; author edits in their text editor of choice if they want byte-level control. (`reload-world` picks up the changes.)
- Do **not** ship community-mod-package management (signing, dependency resolution, conflict detection). Far future.
- Do **not** ship NPC authoring in this packet. That's WP-4.0.K, additive in the same palette UI.
- Do **not** modify gameplay-time build-mode behavior. The player-facing build mode is unchanged; author mode is a superset.
- Do **not** add new visual styling to author mode beyond the mode banner + tinted ghost preview. Full author-tool polish is a future packet if the workflow proves out.
- Do **not** pause the simulation when author mode is active. Author can use the existing pause control if desired; default is "engine keeps ticking, NPCs keep moving around the new geometry."
- Do **not** introduce a new save-format. Round-trip is via WP-4.0.I's writer; this packet is purely UI + mutation primitives.

---

## Design notes

### Author-mode flag

Single field on `BuildModeController`:

```csharp
public bool IsAuthorMode { get; private set; }

public void ToggleAuthorMode() {
#if WARDEN
    IsAuthorMode = !IsAuthorMode;
    OnAuthorModeChanged?.Invoke(IsAuthorMode);
#endif
}
```

`PlacementValidator` consults this flag and skips gameplay-time gating when `true`. Physical-overlap validation still runs.

Keybinding `Ctrl+Shift+A` registered in the existing input map (WARDEN-only).

### Extended palette categories

Add to `PaletteCategory` enum:

```csharp
public enum PaletteCategory {
    Structural,
    Furniture,
    Props,
    NamedAnchor,
    Room,           // new
    LightSource,    // new
    LightAperture,  // new
    // NpcArchetype — added in WP-4.0.K
}
```

`BuildPaletteUI` renders the new categories as additional tabs (only visible in author mode). Each tab populates its sub-list from the new catalog file:

`docs/c2-content/build/author-mode-palette.json`:

```jsonc
{
  "schemaVersion": "0.1.0",
  "rooms": [
    { "label": "Cubicle Area",   "roomKind": "cubicleGrid", "tooltip": "Open-plan cubicle floor; carpet + cubicle walls." },
    { "label": "Manager Office", "roomKind": "office",      "tooltip": "Single-occupant office; hardwood + structural walls." },
    { "label": "Kitchen / Breakroom", "roomKind": "breakroom", "tooltip": "Linoleum floor; structural walls." },
    { "label": "Bathroom",       "roomKind": "bathroom",    "tooltip": "Linoleum floor; restroom doors." },
    { "label": "Hallway",        "roomKind": "hallway",     "tooltip": "Office tile floor; structural walls." },
    { "label": "Supply Closet",  "roomKind": "supplyCloset","tooltip": "Concrete floor; structural walls." },
    { "label": "Mechanical Room","roomKind": "mechanical",  "tooltip": "Concrete floor; structural walls." }
  ],
  "lightSources": [
    { "label": "Overhead Fluorescent", "kind": "overheadFluorescent", "defaultIntensity": 70, "defaultTempK": 4000, "defaultState": "on" },
    { "label": "Desk Lamp",            "kind": "deskLamp",            "defaultIntensity": 65, "defaultTempK": 3800, "defaultState": "on" },
    { "label": "Breakroom Strip",      "kind": "breakroomStrip",      "defaultIntensity": 58, "defaultTempK": 3900, "defaultState": "on" }
  ],
  "lightApertures": [
    { "label": "Window — Small (3 sq tiles)", "areaSqTiles": 3.0 },
    { "label": "Window — Medium (5 sq tiles)","areaSqTiles": 5.0 },
    { "label": "Window — Large (8 sq tiles)", "areaSqTiles": 8.0 }
  ]
}
```

### Tool implementations

**Room rectangle tool**. Click-and-drag from one tile to another draws a rectangle ghost; release commits. Validation: rectangle must not overlap an existing room. (Adjacent is fine — that's how rooms share walls.) On commit, calls `IWorldMutationApi.CreateRoom(roomKind, bounds, floorId)`. Returns the new room's id; selection auto-snaps to the new room so author can immediately tune name / smell tag / description via the inspector (existing inspector from WP-3.1.E extends to support room-property editing — additive change, no new UI surface).

**Light source tool**. Click a tile inside a room to place. Light spawns with default state from the catalog entry. Inline tuner UI appears: dropdowns for state (on/off/flickering/dying), sliders for intensity (0–100) and color temperature (2000–6500 K). "Apply" commits via `IWorldMutationApi.CreateLightSource(roomId, position, kind, state, intensity, tempK)`. "Cancel" removes the placed light.

**Light aperture tool**. Click a wall tile (must be on a room boundary) to place a window. Inline tuner UI: dropdown for facing direction (auto-inferred from clicked wall but overridable), slider for area. "Apply" commits via `IWorldMutationApi.CreateLightAperture(roomId, position, facing, areaSqTiles)`.

**Eraser tool**. A dedicated button on the toolbar. When active, hovering an entity highlights it red; clicking deletes via the appropriate `IWorldMutationApi.Despawn*` call. For rooms with contents, a confirmation dialog: "This room contains 3 props and 2 NPCs. Delete the room only (orphans go to floor parent), or delete contents too?"

### `IWorldMutationApi` extensions

Add to the interface:

```csharp
/// <summary>Spawns a new room entity with the given kind and bounds.</summary>
Guid CreateRoom(string roomKind, BoundsRect bounds, Guid floorId);

/// <summary>Despawns a room. Optional contents handling: orphan or cascade-delete.</summary>
void DespawnRoom(Guid roomId, RoomDespawnPolicy policy);

/// <summary>Spawns a light source in the named room at the given tile.</summary>
Guid CreateLightSource(Guid roomId, int tileX, int tileY, string kind, string state, int intensity, int colorTempK);

/// <summary>Mutates a light source's tunable properties in place.</summary>
void TuneLightSource(Guid lightId, string state, int intensity, int colorTempK);

/// <summary>Spawns a light aperture (window) on the boundary of a room.</summary>
Guid CreateLightAperture(Guid roomId, int tileX, int tileY, string facing, float areaSqTiles);

/// <summary>Despawns a light source or aperture.</summary>
void DespawnLight(Guid lightId);
```

Each implementation in `WorldMutationApi` emits the appropriate `StructuralChangeEvent` so the pathfinding cache invalidates correctly (rooms affect path costs; lights don't but they affect rendering).

### Save / Load / Reload toolbar

Three buttons in the author-mode HUD, each calling the WP-4.0.I dev-console handler programmatically:

- **Save** — opens a small modal: "Scene name: [____]". On confirm, calls `WorldDefinitionWriter.WriteToFile(...)`. Status banner: "Saved as <name>.json".
- **Load** — opens a small modal listing all `world-definition` files (using `list-worlds` data). On confirm, calls reload via the loader. Status banner: "Loaded <name>.json".
- **Reload** — calls reload of the currently-loaded scene, discarding any unsaved changes after a confirmation. Useful when author wants to revert.

### Sandbox scene

`Assets/_Sandbox/author-mode.unity`:
- An empty world (single floor, no rooms, no NPCs). Just a 40×30 floor grid.
- Author mode pre-toggled on at scene start.
- Camera at default 15m altitude, panable.
- Test recipe walks Talon through:
  1. Draw a room. Verify materials apply (couples to 4.0.D).
  2. Place a light source in the room. Verify lighting visualization updates (couples to 3.1.C).
  3. Place a window on the room boundary. Verify daylight enters (couples to 3.1.C).
  4. Erase the light source. Verify lighting goes back to ambient.
  5. Save scene as "sandbox-test".
  6. Reload scene. Verify same room, no light, no window (eraser persisted; pre-erase save didn't happen).
  7. Repeat saving with the light back and verify reload preserves it.
  8. Stress: draw 10 rooms in 2 minutes. Verify no perf regression.

### Sandbox vs integration

- **WP-4.0.J (this packet):** sandbox + extended palette + tools + IWorldMutationApi extensions + save/load toolbar + tests. Production scenes unchanged.
- **WP-4.0.J-INT** (companion, drafted later): wire author mode toggle into MainScene + PlaytestScene. Talon's hands.

### Performance

Author mode adds no per-frame cost when inactive (the toggle is a single bool check in PlacementValidator). When active, each tool's interaction is one-shot per click; no continuous overhead. Room creation involves room-entity spawn + room-visual-identity material assignment — same path as boot-time scene load, well under the per-frame budget.

The 30-NPCs-at-60-FPS gate is preserved with verification: a `PerformanceGate30NpcWithAuthorModeActiveTests` confirms that toggling author mode on/off with 30 NPCs and 5 rooms drawn does not drop below 60 FPS.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `ECSUnity/Assets/Scripts/BuildMode/BuildModeController.cs` (modification) | Add `IsAuthorMode` + toggle + keybind. |
| code | `ECSUnity/Assets/Scripts/BuildMode/PlacementValidator.cs` (modification) | Skip gameplay-time gating when author mode active. |
| code | `ECSUnity/Assets/Scripts/BuildMode/BuildPaletteCatalog.cs` (modification) | Extend `PaletteCategory` enum (Room / LightSource / LightAperture). |
| code | `ECSUnity/Assets/Scripts/BuildMode/BuildPaletteUI.cs` (modification) | Render new tabs only in author mode; load author-mode palette JSON. |
| code | `ECSUnity/Assets/Scripts/BuildMode/AuthorModePaletteCatalog.cs` (new) | ScriptableObject + JSON loader for the author-mode palette. |
| code | `ECSUnity/Assets/Scripts/BuildMode/Tools/RoomRectangleTool.cs` (new) | Room-drawing tool. |
| code | `ECSUnity/Assets/Scripts/BuildMode/Tools/LightSourceTool.cs` (new) | Light-placement tool + inline tuner. |
| code | `ECSUnity/Assets/Scripts/BuildMode/Tools/LightApertureTool.cs` (new) | Aperture-placement tool + inline tuner. |
| code | `ECSUnity/Assets/Scripts/BuildMode/Tools/EraserTool.cs` (new) | Erase any author-placed entity. |
| code | `ECSUnity/Assets/Scripts/BuildMode/UI/AuthorModeBanner.cs` (new) | Mode-active banner. |
| code | `ECSUnity/Assets/Scripts/BuildMode/UI/AuthorModeToolbar.cs` (new) | Save/Load/Reload buttons + status banner. |
| code | `APIFramework/Mutation/IWorldMutationApi.cs` (modification) | Add CreateRoom / DespawnRoom / CreateLightSource / TuneLightSource / CreateLightAperture / DespawnLight. |
| code | `APIFramework/Mutation/WorldMutationApi.cs` (modification) | Implement new operations + emit StructuralChangeEvent. |
| code | `APIFramework/Mutation/RoomDespawnPolicy.cs` (new) | Enum: OrphanContents, CascadeDelete. |
| code | `APIFramework/Mutation/Undo/CreateRoomUndoable.cs`, `DespawnRoomUndoable.cs`, `CreateLightSourceUndoable.cs`, `TuneLightSourceUndoable.cs`, `CreateLightApertureUndoable.cs`, `DespawnLightUndoable.cs` (new, 6 files) | Undo entries for the new mutations; integrate with WP-4.0.G's undo stack. |
| data | `docs/c2-content/build/author-mode-palette.json` (new) | The author-mode palette. |
| scene | `ECSUnity/Assets/_Sandbox/author-mode.unity` | Sandbox per Rule 4. |
| doc | `ECSUnity/Assets/_Sandbox/author-mode.md` | 15-20 minute test recipe. |
| test | `APIFramework.Tests/Mutation/CreateRoomTests.cs` | Room creation behavior + StructuralChangeEvent emission. |
| test | `APIFramework.Tests/Mutation/CreateLightSourceTests.cs` | Light source creation + tuning. |
| test | `APIFramework.Tests/Mutation/CreateLightApertureTests.cs` | Aperture creation. |
| test | `APIFramework.Tests/Mutation/RoomDespawnPolicyTests.cs` | OrphanContents vs CascadeDelete behavior. |
| test | `APIFramework.Tests/Mutation/AuthorModeUndoRedoTests.cs` | Undo + redo of each new mutation. |
| test | `ECSUnity/Assets/Tests/Edit/AuthorModePaletteCatalogJsonTests.cs` | JSON validation. |
| test | `ECSUnity/Assets/Tests/Play/AuthorModeToggleTests.cs` | Toggle on/off; gating bypass works. |
| test | `ECSUnity/Assets/Tests/Play/AuthorModeRoundTripTests.cs` | Draw room → save → reload → room still there. |
| test | `ECSUnity/Assets/Tests/Play/PerformanceGate30NpcWithAuthorModeActiveTests.cs` | FPS gate preserved. |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Add MAC-015 (extended build palette / author-mode tools as Mod API surface). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | Author mode toggles on/off via `Ctrl+Shift+A` (WARDEN only). | manual + integration |
| AT-02 | Author mode banner visible when active; hidden when inactive. | manual visual |
| AT-03 | Player-facing build mode behavior unchanged when author mode is off. | regression |
| AT-04 | Room rectangle tool draws a room of the chosen kind; new room renders with correct floor/wall materials per WP-4.0.D. | manual + integration |
| AT-05 | Room placement validates: cannot overlap an existing room; can be adjacent. | unit + manual |
| AT-06 | Light source tool places a light; inline tuner updates state/intensity/temperature; lighting visualization (3.1.C) reflects changes. | manual visual |
| AT-07 | Light aperture tool places a window on a room boundary; daylight enters per existing aperture behavior. | manual visual |
| AT-08 | Eraser deletes author-placed rooms / lights / apertures / props; for rooms with contents, prompts policy choice. | manual + unit |
| AT-09 | Each new mutation is undoable via the existing WP-4.0.G undo stack; redoable. | unit + manual |
| AT-10 | Save toolbar button writes a `world-definition.json` via WP-4.0.I writer. | integration |
| AT-11 | Load toolbar button reloads any saved scene cleanly. | integration |
| AT-12 | Reload toolbar button confirms before discarding changes. | manual |
| AT-13 | Round-trip: draw 3 rooms + 5 lights + 2 windows → save → reload → identical scene. | round-trip-test |
| AT-14 | Author mode active with 30 NPCs holds ≥ 60 FPS. | play-mode test |
| AT-15 | All Phase 0–3 + Phase 4.0.A–I tests stay green. | regression |
| AT-16 | `dotnet build` warning count = 0; all tests green. | build + test |
| AT-17 | MAC-015 added to `MOD-API-CANDIDATES.md`. | review |

---

## Mod API surface

This packet introduces **MAC-015: Extended build palette + author-mode tools (modder-extensible)**. Append to `MOD-API-CANDIDATES.md`:

> **MAC-015: Extended build palette / author-mode tools**
> - **What:** A modder-extensible palette of authoring tools — rectangle-drag rooms, point-place lights and apertures, with per-tool inline tuners. Catalog JSON (`docs/c2-content/build/author-mode-palette.json`) is the data extension surface; the underlying mutation contract is `IWorldMutationApi` (MAC-007). A modder adding a new room kind extends the palette JSON + the `RoomKind` parser + the room visual identity catalog (MAC-014) — three coordinated additions, all data-driven.
> - **Where:** `ECSUnity/Assets/Scripts/BuildMode/AuthorModePaletteCatalog.cs`; `ECSUnity/Assets/Scripts/BuildMode/Tools/*.cs`; `docs/c2-content/build/author-mode-palette.json`; `APIFramework/Mutation/IWorldMutationApi.cs` (extensions).
> - **Why a candidate:** Author-mode tools are the most direct community-contribution surface — a level designer authoring a custom office is the canonical mod use case Talon called out 2026-05-03. Palette is data-driven (consistent with MAC-001 / MAC-005 / MAC-013 / MAC-014 / MAC-016).
> - **Stability:** fresh (lands with WP-4.0.J; second consumer = NPC archetype tool in WP-4.0.K).
> - **Source packet:** WP-4.0.J.

This packet also extends **MAC-007 (`IWorldMutationApi`)** with six new operations. MAC-007's stability stays at *stabilizing* but accumulates a third consumer (after Phase 3.0.4 boot loader + WP-3.1.D build-mode controller + this packet's author-mode tools).

---

## Followups (not in scope)

- WP-4.0.J-INT — wire author mode toggle into PlaytestScene + MainScene. Talon's hands.
- WP-4.0.K — NPC authoring (drop-an-archetype palette tool). Same UI surface, additive.
- WP-4.0.L — author-mode docs (README, FF-016 ledger entry, mod-author quickstart).
- Future packet: scene templates / starter palettes ("Start from blank office", "Start from `playtest-office`", "Start from a shared community scene").
- Future packet: room-property editor (smell tag, description, named anchor) accessible inline. Today, those properties still need text-editor edits to the saved JSON.
- Future packet: scene-comparison / diff tool. Useful for community PR review of authored scenes.
- Future packet: in-game JSON inspector. Right-click any author-placed entity, see its serialized form. Educational + debugging value.
- Future packet: keyboard shortcuts for tool-switching (R for room, L for light, etc.). Polish.
- Future packet: snap-to-grid options + measurement overlay. Polish.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

Track 2 sandbox packet. Visual verification by Talon required.

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm worktree at `.claude/worktrees/sonnet-wp-4.0.j/` on branch `sonnet-wp-4.0.j` based on recent `origin/staging` (which now includes WP-4.0.I writer + schema 0.2.0).
1. Implement the spec.
2. Run all Unity tests + `dotnet test`. All must stay green.
3. Stage all changes including self-cleanup.
4. Commit on the worktree's feature branch.
5. Push the branch.
6. Stop. Notify Talon: `READY FOR VISUAL VERIFICATION — run Assets/_Sandbox/author-mode.md (15-20 min recipe)`.

### Feel-verified-by-playtest acceptance flag

**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN:** Can a designer draw a complete office (4 rooms, lights, windows) in under 10 minutes? Does the workflow feel like "level design" or like "wrestling with the tool"? Does save/load round-trip cleanly?

### Cost envelope

Target: **$0.85**. Three new tools + extended palette + 6 mutation primitives + 6 undoable wrappers + sandbox + tests. If cost approaches $1.40, escalate via `WP-4.0.J-blocker.md`.

Cost-discipline:
- Reuse the WP-4.0.G undo stack — don't reinvent.
- Use Unity's built-in IMGUI (or whatever the existing build-mode UI uses) for inline tuners; don't introduce new UI frameworks.
- Save/Load/Reload toolbar buttons are programmatic invocations of WP-4.0.I dev-console handlers; no parallel implementation.
- Don't ship final-quality author-mode UI styling; functional placeholder is the bar (final UI polish in a future packet if the workflow proves out).

### Self-cleanup on merge

Standard. Check for `WP-4.0.J-INT` and `WP-4.0.K` as likely dependents.
