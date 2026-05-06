# World Definitions

This directory holds **scene files** for the ECS Simulation Engine. A scene is a JSON document describing rooms, lights, windows, props, and NPC spawn slots. The simulation boots from one of these files; you can also author + reload them at runtime via the in-game author mode.

## Quickstart for level designers

1. **Boot the game** in WARDEN mode. (Use the dev launcher; standard player builds don't expose author mode.)
2. **Press `Ctrl+Shift+A`** to enter author mode. The `AuthorModeController` MonoBehaviour responds to this hotkey when present in the scene; check the console for the `[AuthorMode] ACTIVATED` log.
3. **Use the author API.** As of the wave-4 substrate ship (2026-05-03), the author-mode controller exposes the engine API as Unity-side methods (`DrawRoom`, `PlaceLightSource`, `TuneLightSource`, `PlaceAperture`, `EraseRoom`, `EraseLight`, `SpawnNpc`, `EraseNpc`, `RenameNpc`, `SaveWorld`, `ResolveAutoName`). The full **palette panel UI** (rectangle-drag for rooms, point-place for lights, NPC archetype tabs) is forthcoming — wire your own UI buttons against the controller methods until then.
4. **Save**: call `AuthorModeController.SaveWorld("my-scene", "My Scene", seed: 0)` to write `docs/c2-content/world-definitions/my-scene.json`. Path traversal and absolute paths are rejected; the scene name must be a simple file name.
5. **Reload**: restart the game with the new scene as the boot scene, OR (when the dev-console reload command lands) call `> reload-world my-scene` in the dev console.

## What's saved, what's not

A scene captures **structure** — rooms, lights, windows, NPC spawn slots (room + tile + archetype + name hint), and anchor objects. It does **not** capture in-flight simulation state — drives, memories, in-progress actions, schedule cursors. Reloaded NPCs spawn at authored positions with default drives and zero memory. (That's a future feature; see FF-016 in `docs/future-features.md`.)

## Existing scenes

- `playtest-office.json` — the canonical 5-NPC playtest office (used by `PlaytestScene.unity`).
- `office-starter.json` — minimal starter scene; useful as a template.

## File format

The schema is documented inline via XML doc comments on `APIFramework/Bootstrap/WorldDefinitionDto.cs`. Top-level structure:

```jsonc
{
  "schemaVersion": "0.1.0",
  "worldId":       "my-scene",
  "name":          "My Scene",
  "seed":          20260101,
  "floors":           [ /* { id, name, floorEnum } */ ],
  "rooms":            [ /* { id, name, category, floorId, bounds, initialIllumination, namedAnchorTag?, description?, smellTag?, notesAttached? } */ ],
  "lightSources":     [ /* { id, kind, state, intensity, colorTemperatureK, position, roomId } */ ],
  "lightApertures":   [ /* { id, position, roomId, facing, areaSqTiles } */ ],
  "npcSlots":         [ /* { id, roomId?, x, y, archetypeHint?, nameHint? } */ ],
  "objectsAtAnchors": [ /* { id, roomId, description, physicalState } */ ]
}
```

The format follows the project's data-driven extension pattern — see `docs/c2-infrastructure/MOD-API-CANDIDATES.md#MAC-016`.

### Enum vocabularies

- `floorEnum`: `basement`, `first`, `top`, `exterior`
- `category` (RoomCategory): `breakroom`, `bathroom`, `cubicleGrid`, `office`, `conferenceRoom`, `supplyCloset`, `itCloset`, `hallway`, `stairwell`, `elevator`, `parkingLot`, `smokingArea`, `loadingDock`, `productionFloor`, `lobby`, `outdoor`
- `kind` (LightKind): `overheadFluorescent`, `deskLamp`, `serverLed`, `breakroomStrip`, `conferenceTrack`, `exteriorWall`, `signageGlow`, `neon`, `monitorGlow`
- `state` (LightState): `on`, `off`, `flickering`, `dying`
- `facing` (ApertureFacing): `north`, `east`, `south`, `west`, `ceiling`
- `physicalState` (AnchorObjectPhysicalState): `present`, `present-degraded`, `present-greatly-degraded`, `absent`
- `archetypeHint`: any id from `docs/c2-content/archetypes/archetypes.json` (e.g., `the-vent`, `the-newbie`, `the-hermit`)

## Contributing a community scene

The "community scene" workflow is forthcoming and will be documented in the comprehensive `docs/AUTHORING-GUIDE.md` (authored by Opus post-Editor-verification, when the palette UI has been wired and used in anger). For now: open a PR with your `<name>.json` file in this directory; reviewers will run it locally to validate.

## Where to look for context

- **Cast** (who lives in the office): `docs/c2-content/cast-bible.md`
- **World** (what offices look like in this world): `docs/c2-content/world-bible.md`
- **Aesthetic** (visual style, era, materials): `docs/c2-content/aesthetic-bible.md`
- **UX/UI** (player experience): `docs/c2-content/ux-ui-bible.md`
- **Future scope** (in-game scene authoring, comprehensive guide, loot-box reroll): `docs/future-features.md#FF-016`
- **Mod API surface** (what's modder-extensible today, what graduates later): `docs/c2-infrastructure/MOD-API-CANDIDATES.md`

## Comprehensive guide

The full author / mod-author guide is at `docs/AUTHORING-GUIDE.md` (forthcoming). This README is the v0.1 quickstart while that guide is in flight.
