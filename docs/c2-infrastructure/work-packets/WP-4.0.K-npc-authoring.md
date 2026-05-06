# WP-4.0.K — NPC Authoring (Drop-an-Archetype Tool)

> **Wave 4 of the Phase 4.0.x foundational polish wave — authoring loop.** Per Talon's 2026-05-03 framing: a level designer needs to populate a scene with NPCs, not just rooms and props. This packet adds the NPC-authoring tool to the WP-4.0.J author-mode palette: select an archetype, click a tile inside a room, an NPC of that archetype spawns there with sane defaults. Round-trips through the WP-4.0.I writer's `npcSlots` serialization. Track 2 sandbox + companion `-INT` packet.

> **DO NOT DISPATCH UNTIL WP-4.0.I AND WP-4.0.J ARE MERGED** — depends on the writer's NPC-slot round-trip discipline (I) and the author-mode palette UI (J).

**Tier:** Sonnet
**Depends on:** WP-4.0.J (author mode + extended palette — required, this packet adds a fourth palette category to it), WP-4.0.I (world-definition writer — required for save round-trip), WP-4.0.M (cast name generator library — required for auto-naming; replaces the stub `CastNamePool` originally specified in this packet), WP-3.2.5 (per-archetype tuning JSONs — the source-of-truth for which archetypes exist), WP-3.0.4 (`IWorldMutationApi` — extended in this packet with NPC spawn/despawn).
**Parallel-safe with:** WP-4.0.L (docs).
**Timebox:** 120 minutes
**Budget:** $0.55
**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN:** Can a designer populate an empty office with 8 NPCs of varied archetypes in under 5 minutes? Do the spawned NPCs immediately exhibit their archetype-correct behavior (Donna-the-vent gossiping, the-newbie-being-lost, etc.)? Does save → reload preserve all NPC placements with their archetypes intact?

---

## Goal

Today, NPCs are spawned at boot from `world-definition.json#npcSlots` blocks (room id, position, archetype hint, name hint). To add an NPC to a scene at design time, Talon hand-edits the JSON and restarts. WP-4.0.J replaces hand-editing for rooms / lights / apertures; this packet completes the picture for NPCs.

The tool: a new `NpcArchetype` palette category in the WP-4.0.J author-mode palette. Each entry is an archetype from the per-archetype tuning catalog (WP-3.2.5 — the canonical archetype list). Select an archetype, click a tile inside a room, an NPC spawns there. Default name is auto-generated from the cast names options pool (`docs/c2-content/cast-names-options.json`); author can override after spawn via a small inline name field. The new NPC inherits all archetype-correct tuning from MAC-001 (chore bias, choke bias, slip bias, mass, memory persistence, etc.) — there is no ambiguity about "what kind of NPC is this" because archetype is the thing the engine already keys all behavior on.

Round-trip: the WP-4.0.I writer already serializes `npcSlots` from live NPCs (room, position, archetype, name). This packet's spawn path produces NPCs that the writer can serialize; reloaded scenes respawn them at the authored positions.

After this packet:
- A new `NpcArchetype` tab appears in the author-mode palette panel (after Room / LightSource / LightAperture).
- Each archetype from `docs/c2-content/tuning/archetype-*.json` shows as an entry with archetype label + brief description.
- Click an archetype, click a tile inside any room: an NPC of that archetype spawns there. Auto-name from name pool; author can override inline.
- An eraser-select on an authored NPC despawns it cleanly (no orphan relationships, since the NPC is fresh — no chronicle history yet).
- Save round-trip: scene with 5 authored NPCs → save → reload → 5 NPCs at same positions with same archetypes + names; their drives reset to defaults (per WP-4.0.I non-goal: in-flight state is not authored).
- Inline name editing: select an authored (or boot-loaded) NPC, the existing inspector popup (WP-3.1.E) gains a "name" field that is editable in author mode only.

The 30-NPCs-at-60-FPS gate holds: the standard `PerformanceGate30NpcWithAuthorModeActiveTests` from WP-4.0.J is extended to verify FPS with all 30 NPCs authored mid-session.

---

## Reference files

- `docs/UNITY-PACKET-PROTOCOL.md` — sandbox-first per Rule 2.
- `docs/c2-infrastructure/MOD-API-CANDIDATES.md` — bumps MAC-015 (extended build palette) with second consumer; touches MAC-001 stability assessment.
- `docs/c2-content/world-definitions/playtest-office.json#npcSlots` — the existing NPC-slot format. New tool produces NPCs that round-trip through this section.
- `docs/c2-content/tuning/archetype-*.json` — the archetype catalog. Each file is an entry in the new palette category.
- `docs/c2-content/cast-names-options.json` — names pool for auto-naming.
- `docs/c2-content/cast-bible.md` — read for archetype descriptions to populate palette tooltips. The cast bible is the canonical "who are these people" document.
- `APIFramework/Systems/Tuning/TuningCatalog.cs` — read for how archetype JSONs are loaded; new tool consults this catalog directly so it stays in sync with whatever archetypes ship.
- `APIFramework/Bootstrap/CastGenerator.cs` — read for how boot-time NPC slots become live NPCs (this packet's spawn path mirrors this).
- `APIFramework/Bootstrap/WorldDefinitionLoader.cs#NpcSlot` handling — read for boot-time spawn pattern; runtime spawn must be semantically equivalent.
- `APIFramework/Bootstrap/WorldDefinitionWriter.cs` (from WP-4.0.I) — already serializes live NPCs to `npcSlots`. This packet adds NPCs that writer needs to handle, but the writer code itself doesn't change.
- `APIFramework/Components/ArchetypeComponent.cs` — read for the archetype tag the new NPC must have.
- `APIFramework/Mutation/IWorldMutationApi.cs` — extended in this packet with NPC spawn/despawn.
- `ECSUnity/Assets/Scripts/UI/InspectorPopup.cs` (or wherever WP-3.1.E's inspector lives) — read for the inspector UI; small additive change adds the name field.
- `ECSUnity/Assets/Scripts/BuildMode/AuthorModePaletteCatalog.cs` (from WP-4.0.J) — extend to add `npcArchetypes` section.

---

## Non-goals

- Do **not** allow author-mode NPC creation outside author mode. Even in WARDEN builds, the new palette category is hidden when author mode is off.
- Do **not** ship in-flight state authoring (drives, memories, relationships, schedule cursors). Authored NPCs spawn with default drives and zero memory. (Future packet: scenario-state-overlay authoring, e.g. "this NPC starts already stressed at 80%".)
- Do **not** add new archetypes in this packet. The palette is generated from whatever archetype JSONs are present in `docs/c2-content/tuning/`. New archetypes are content packets.
- Do **not** ship cast-relationship authoring (e.g., "Sandra and Frank are having an affair"). Relationships are runtime-emergent from interactions per the cast bible. Authored scenes start with zero relationships; chronicle accumulates from there.
- Do **not** override schedules per-NPC at spawn. Authored NPCs use their archetype's default schedule. (Future packet if needed.)
- Do **not** modify `WorldDefinitionWriter` (WP-4.0.I). The writer already serializes the slot fields this packet's NPCs carry.
- Do **not** restrict spawn to designated `npcSlot` positions from the original world-definition. Author mode allows spawning anywhere a tile is walkable inside a room.
- Do **not** modify `CastGenerator` boot-time behavior. Boot-time NPC spawning continues to flow through the existing path; this packet is a parallel runtime-spawn path with the same semantics.
- Do **not** ship NPC-archetype filtering / search in the palette. The archetype list is short (~12 entries); a flat list is fine. (Future polish if the catalog grows past ~25.)

---

## Design notes

### Palette extension

Add to `PaletteCategory` (in WP-4.0.J's enum extension):

```csharp
public enum PaletteCategory {
    Structural, Furniture, Props, NamedAnchor,
    Room, LightSource, LightAperture,
    NpcArchetype,   // new in K
}
```

Add to `author-mode-palette.json` (extending WP-4.0.J's catalog file):

```jsonc
{
  // ... existing rooms / lightSources / lightApertures from J
  "npcArchetypes": {
    "discovery": "auto",   // means: enumerate archetype-*.json files at boot
    "fallbackEntries": []  // optional manual entries if discovery fails
  }
}
```

The palette UI's `NpcArchetype` tab populates from `TuningCatalog.AllArchetypes()` — the live archetype list. This guarantees the palette stays in sync with whatever archetypes ship, including new ones added in future content packets, without code or palette-JSON changes.

Per-entry display: archetype id (`the-vent`, `the-newbie`, etc.), one-line description from the cast bible, optional thumbnail (silhouette preview from MAC-013's catalog if available; placeholder otherwise).

### Spawn tool

`Assets/Scripts/BuildMode/Tools/NpcArchetypeSpawnTool.cs`:

1. User clicks an archetype in the palette → tool active, ghost preview shows a generic NPC silhouette with archetype-color tint.
2. User hovers a tile → ghost moves to that tile; validation:
   - Tile must be inside a room (not in a hallway-without-room or out of world).
   - Tile must be walkable (no solid prop at the tile).
   - Personal-space rule from WP-4.0.B applies — tile must be ≥ 1 tile from another NPC (warn if violated; allow with click-confirm if author insists).
3. User clicks → calls `IWorldMutationApi.CreateNpc(roomId, x, y, archetypeId, name)`. New NPC spawns; selection auto-snaps to it.
4. Inline name field appears in the inspector popup; author types; commits on Enter or click-away. Default is auto-generated from name pool (filtered to avoid duplicates).
5. Tool stays active for next placement (consistent with other author tools); Esc deactivates.

### Auto-naming (delegated to WP-4.0.M)

`CastNamePool` is a thin wrapper around `CastNameGenerator` (WP-4.0.M). When a new NPC needs an auto-name:

1. Call `CastNameGenerator.Generate(gender)` — returns a `CastNameResult` with display name + tier + sub-fields.
2. If the resulting `DisplayName` collides with an existing live NPC, reroll up to 5 times (each reroll is a fresh `Generate(...)` call with a new seed).
3. If 5 rerolls all collide (vanishingly rare in practice — pool space is enormous due to fusion grammar), fall back to `<archetypeId>-<n>` where n is the next available integer.

The returned `CastNameResult.Tier` is recorded on the NPC entity so it can be re-serialized into `npcSlots` (see WP-4.0.M's note on `npcSlots#generatedTier`); reload-time respawn picks up the same tier metadata for inspector / future hire-screen surfaces.

### `IWorldMutationApi` extension

```csharp
/// <summary>
/// Spawns a new NPC of the given archetype at the tile.
/// All archetype-derived components (tuning, behavior biases, schedule defaults)
/// applied per the existing TuningCatalog and CastGenerator semantics.
/// Returns the new NPC's entity id.
/// </summary>
Guid CreateNpc(Guid roomId, int tileX, int tileY, string archetypeId, string name);

/// <summary>
/// Despawns an NPC. Cascade-removes per-pair relationship edges referencing this NPC
/// (zero-sized at spawn for authored NPCs; cleanup is no-op then but the discipline holds
/// for despawning long-lived NPCs from boot-loaded scenes too).
/// </summary>
void DespawnNpc(Guid npcId);

/// <summary>Renames an existing NPC (author mode + retail; retail use is rare but the seam is general).</summary>
void RenameNpc(Guid npcId, string newName);
```

`WorldMutationApi.CreateNpc` reuses the existing `CastGenerator` spawn path for consistency with boot-time NPCs — same archetype application, same tuning override application, same default-drive seeding. The implementation factor here is to make `CastGenerator` callable as a runtime-spawn helper, not just a boot-time loop.

### Inspector popup name field

`InspectorPopup.cs` (WP-3.1.E) currently shows NPC info read-only. This packet adds:

- A `Name:` row that is **read-only in non-author mode**, **text-input in author mode**.
- A "Despawn (author)" button visible only in author mode, calling `IWorldMutationApi.DespawnNpc`.

Renaming through this UI calls `IWorldMutationApi.RenameNpc`. Despawn calls trigger an "Are you sure?" confirmation if the NPC has a non-empty chronicle (i.e., is a boot-loaded NPC with history). Authored NPCs (chronicle-empty) despawn without confirmation.

### Save / Load round-trip

The WP-4.0.I writer's `SerializeNpcSlots()` walks all live NPCs and emits one slot per NPC:

```json
{
  "id": "slot-<auto-generated-from-npc-id>",
  "roomId": "<NPC's current room id>",
  "x": <floor(NPC position x)>,
  "y": <floor(NPC position y)>,
  "archetypeHint": "<NPC's archetype id>",
  "nameHint": "<NPC's name>"
}
```

Boot-loaded and author-spawned NPCs are indistinguishable in serialization — both produce identical slot structures. The reload path uses the existing `CastGenerator` to respawn from slots; authored NPCs reappear at authored positions with their archetypes and names intact, drives reset to defaults.

This is the key integration with WP-4.0.I/J: no new file format, no schema bump, no special-case code for "authored vs boot-loaded" NPCs. The system is uniform.

### Sandbox scene

`Assets/_Sandbox/npc-authoring.unity`:
- Boots the empty `Assets/_Sandbox/author-mode.unity` scene from WP-4.0.J (or a near-equivalent — single floor, no rooms or NPCs).
- Author mode pre-toggled on.
- Test recipe (15-20 min):
  1. Draw a single room (cubicle area) using WP-4.0.J's room tool. (Validates J/K integration.)
  2. Place 4 NPCs of varied archetypes inside: `the-vent`, `the-newbie`, `the-cynic`, `the-old-hand`.
  3. Verify each spawns with auto-name + archetype-correct silhouette tint.
  4. Rename one (e.g., the-vent → "Donna").
  5. Despawn one. Verify NPC + slot disappear.
  6. Save scene as "npc-authoring-test".
  7. Reload. Verify 3 NPCs return at original positions with correct archetypes + names.
  8. Tick the simulation for ~30 seconds (un-pause). Verify each NPC exhibits archetype-correct behavior:
     - the-vent gossips when adjacent to another NPC.
     - the-newbie wanders / acts lost.
     - the-cynic refuses chores at the expected rate.
     - the-old-hand operates competently.
  9. Stress test: spawn 30 NPCs in 2 minutes, verify FPS gate.

### Performance

NPC spawn is one entity-create + N component-add operations (well under 1ms each). The `PerformanceGate30NpcWithAuthorModeActiveTests` from WP-4.0.J is extended to verify all 30 NPCs spawned mid-session retain ≥ 60 FPS.

### Sandbox vs integration

- **WP-4.0.K (this packet):** sandbox + NpcArchetypeSpawnTool + IWorldMutationApi extensions + inspector enhancements + tests. Production scenes unchanged.
- **WP-4.0.K-INT** (companion, drafted later): no separate scene wiring needed — author mode (WP-4.0.J) lights up the new palette category automatically once the catalog discovers archetypes. INT may be a no-op packet or fold into J's INT.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `ECSUnity/Assets/Scripts/BuildMode/BuildPaletteCatalog.cs` (modification) | Extend `PaletteCategory` with `NpcArchetype`. |
| code | `ECSUnity/Assets/Scripts/BuildMode/AuthorModePaletteCatalog.cs` (modification) | Add npcArchetypes section + auto-discovery from TuningCatalog. |
| code | `ECSUnity/Assets/Scripts/BuildMode/BuildPaletteUI.cs` (modification) | Render new tab; enumerate archetypes from TuningCatalog. |
| code | `ECSUnity/Assets/Scripts/BuildMode/Tools/NpcArchetypeSpawnTool.cs` (new) | Spawn tool with hover-validate + click-spawn. |
| code | `ECSUnity/Assets/Scripts/UI/InspectorPopup.cs` (modification) | Author-mode editable name field + despawn button. |
| code | `APIFramework/Mutation/IWorldMutationApi.cs` (modification) | Add CreateNpc / DespawnNpc / RenameNpc. |
| code | `APIFramework/Mutation/WorldMutationApi.cs` (modification) | Implement; reuse CastGenerator's spawn helper. |
| code | `APIFramework/Bootstrap/CastGenerator.cs` (modification) | Extract a `SpawnSingleNpc` helper that both boot loop and runtime spawn use. Pure refactor, no behavior change. |
| code | `APIFramework/Bootstrap/CastNamePool.cs` (new) | Thin wrapper over `CastNameGenerator` (M); reroll-on-collision (≤5x) + numeric fallback. |
| code | `APIFramework/Mutation/Undo/CreateNpcUndoable.cs`, `DespawnNpcUndoable.cs`, `RenameNpcUndoable.cs` (new, 3 files) | Undo entries per WP-4.0.G stack. |
| data | `docs/c2-content/build/author-mode-palette.json` (modification) | Add npcArchetypes block. |
| scene | `ECSUnity/Assets/_Sandbox/npc-authoring.unity` | Sandbox per Rule 4. |
| doc | `ECSUnity/Assets/_Sandbox/npc-authoring.md` | 15-20 minute test recipe. |
| test | `APIFramework.Tests/Mutation/CreateNpcTests.cs` | Spawn + tuning + drive defaults. |
| test | `APIFramework.Tests/Mutation/DespawnNpcTests.cs` | Cascade behavior. |
| test | `APIFramework.Tests/Mutation/RenameNpcTests.cs` | Rename behavior. |
| test | `APIFramework.Tests/Bootstrap/CastNamePoolTests.cs` | Auto-naming + duplicates avoidance + fallback. |
| test | `APIFramework.Tests/Bootstrap/CastGeneratorRuntimeSpawnTests.cs` | Runtime spawn matches boot spawn semantics for the same slot inputs. |
| test | `APIFramework.Tests/Mutation/NpcAuthoringUndoRedoTests.cs` | Undo/redo of spawn / despawn / rename. |
| test | `APIFramework.Tests/Bootstrap/NpcAuthoringRoundTripTests.cs` | Author 5 NPCs → write → load → 5 NPCs at same positions / archetypes / names. |
| test | `ECSUnity/Assets/Tests/Play/NpcArchetypeSpawnToolTests.cs` | Tool behavior in author mode (hidden in retail / non-author). |
| test | `ECSUnity/Assets/Tests/Play/PerformanceGate30NpcWithAuthorModeActiveTests.cs` (modification of J's variant) | FPS gate with 30 NPCs all author-spawned mid-session. |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Bump MAC-015 to add NpcArchetype tool as second consumer; bump MAC-001 (per-archetype tuning) toward *stable* (now consumed by author-mode palette in addition to engine systems). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | NpcArchetype palette tab visible only in author mode (WARDEN). | manual + integration |
| AT-02 | All archetypes present in `docs/c2-content/tuning/archetype-*.json` appear as palette entries automatically. | unit + manual |
| AT-03 | Spawn tool ghost preview shows on hover; validates room + walkability + personal-space. | manual + integration |
| AT-04 | Spawning an NPC inside a room creates an entity with the correct archetype tuning applied. | unit-test |
| AT-05 | Auto-name pulls a not-yet-taken name from the cast pool; falls back to `<archetype>-<n>` if exhausted. | unit-test |
| AT-06 | Inspector popup shows editable name field in author mode + read-only in player mode. | manual + integration |
| AT-07 | Despawn an authored NPC: entity removed, no orphan relationships (since chronicle empty). | unit + integration |
| AT-08 | Despawn a boot-loaded NPC with chronicle: confirmation dialog appears. | manual |
| AT-09 | Rename via inspector commits via `IWorldMutationApi.RenameNpc`; persists across save/reload. | round-trip-test |
| AT-10 | Round-trip: author 5 NPCs → save → reload → 5 NPCs at same positions / archetypes / names; drives reset. | round-trip-test |
| AT-11 | Authored NPCs exhibit archetype-correct behavior (verified via existing per-archetype behavior tests against authored NPCs). | integration |
| AT-12 | Spawn / despawn / rename undoable via WP-4.0.G undo stack. | unit-test |
| AT-13 | 30 author-spawned NPCs hold ≥ 60 FPS in author mode. | play-mode test |
| AT-14 | All Phase 0–3 + Phase 4.0.A–J tests stay green. | regression |
| AT-15 | `dotnet build` warning count = 0; all tests green. | build + test |
| AT-16 | MAC-015 bump + MAC-001 stability re-evaluation reflected in `MOD-API-CANDIDATES.md`. | review |

---

## Mod API surface

This packet is the **second consumer** of MAC-015 (extended build palette / author-mode tools, introduced in WP-4.0.J). Two consumers — the room/light/aperture tools and the NPC archetype tool — exercising the same palette extension pattern with disjoint mutation primitives is the case for bumping MAC-015 from *fresh* → *stabilizing*.

This packet also strengthens the case for **MAC-001 (per-archetype tuning JSONs)** to graduate. MAC-001 already had 7 engine-system consumers; this packet adds the author-mode palette as an 8th consumer of the *same* surface (the archetype list). When a modder adds an archetype JSON, the author palette picks it up automatically. That's exactly the modder workflow MAC-001 was always going to enable; this packet realizes it. **Recommend bumping MAC-001 from *stabilizing* → *stable* in the MOD-API-CANDIDATES.md update.**

This packet does **not** introduce any new MAC entries — it is composition-of-existing-surfaces. That is the correct shape per SRD §8.8: extension points are recognized over time, surfaces accrue consumers, formal API graduation comes when shape is settled. Three packets in this wave (I, J, K) all build on the same MAC-007 (`IWorldMutationApi`) — which is now strongly *stabilizing* and a primary candidate for the eventual Mod API graduation cohort.

---

## Followups (not in scope)

- WP-4.0.K-INT — sanity-check that NPC palette appears correctly in PlaytestScene + MainScene with author mode toggled. Likely a 5-minute Talon-hands check; may not need a separate packet.
- WP-4.0.L — authoring docs (interim README + ledger entries).
- Future packet: scenario-state authoring overlays (start an NPC at 80% stress, with a specific relationship history, mid-action state). Scope: opt-in per-NPC overrides on top of default-spawn.
- Future packet: NPC-clone tool (alt-drag a placed NPC to spawn another of the same archetype + similar name). Polish.
- Future packet: archetype-distribution authoring ("spawn 5 random NPCs from this archetype list within this room"). Useful for procedural / template scenes.
- Future packet: per-NPC schedule overrides at author time. Useful for narrative scenarios.
- Future packet: relationship-graph authoring (Sandra-Frank-affair, Bob-Karen-strained). Currently runtime-emergent; some scenarios may want pre-existing edges.
- Future packet: NPC-import-from-other-scene tool (copy a beloved cast member from one scene to another). Composition.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

Track 2 sandbox packet. Visual verification by Talon required.

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm worktree at `.claude/worktrees/sonnet-wp-4.0.k/` on branch `sonnet-wp-4.0.k` based on recent `origin/staging` (which now includes WP-4.0.I writer + WP-4.0.J author mode).
1. Implement the spec.
2. Run all Unity tests + `dotnet test`. All must stay green.
3. Stage all changes including self-cleanup.
4. Commit on the worktree's feature branch.
5. Push the branch.
6. Stop. Notify Talon: `READY FOR VISUAL VERIFICATION — run Assets/_Sandbox/npc-authoring.md (15-20 min recipe)`.

### Feel-verified-by-playtest acceptance flag

**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN:** Does the workflow "draw room → drop NPCs → save → see them act in character" feel coherent? Does archetype identity read at a glance from the palette tooltip + spawn behavior?

### Cost envelope

Target: **$0.55**. Spawn tool + IWorldMutationApi extensions + name pool + CastGenerator refactor + inspector enhancement + tests. If cost approaches $0.90, escalate via `WP-4.0.K-blocker.md`.

Cost-discipline:
- Reuse `CastGenerator` for spawn; the runtime-spawn helper is a refactor, not new logic.
- Inspector enhancement is small additive UI; don't redesign the popup.
- Don't ship per-NPC schedule overrides — that's a future packet.

### Self-cleanup on merge

Standard. Check for `WP-4.0.K-INT` (likely no-op or merged into J-INT) and `WP-4.0.L` as likely dependents.
