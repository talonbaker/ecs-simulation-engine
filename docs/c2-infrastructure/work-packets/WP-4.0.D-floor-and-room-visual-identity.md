# WP-4.0.D — Floor & Room Visual Identity

> **Wave 3 of the Phase 4.0.x foundational polish wave.** Per the 2026-05-02 brief restructure: "Floors that read as floors, walls that read as walls, doors that read as doors, room boundaries legible at a glance." Now dispatchable: A1-INT shipped the pixel-art look into production scenes 2026-05-03. This packet authors the floor/wall/door visual vocabulary *under the settled aesthetic*. Track 2 sandbox + companion `-INT` packet.

**Tier:** Sonnet
**Depends on:** WP-4.0.A (URP, merged), WP-4.0.A1 (pixel-art Renderer Feature, merged), WP-4.0.A1-INT (pixel-art live in production scenes, merged), WP-3.1.B / WP-3.1.C (room renderer + lighting visualization).
**Parallel-safe with:** WP-4.0.E (NPC readability — disjoint render surface), WP-4.0.H (particles — disjoint), WP-4.0.G1 (build mode undo/redo — disjoint), WP-PT.* (sandbox-first; production scenes unchanged in this packet).
**Timebox:** 150 minutes
**Budget:** $0.70
**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN (post-`-INT`):** does each floor type (carpet / linoleum / office tile) read as that material at typical camera altitudes? Are walls and doors visually distinct? Is a glance enough to identify what room I'm looking at?

---

## Goal

The play scene currently renders rooms as flat colored rectangles (per WP-3.1.B baseline). Floors don't communicate what they are; walls don't read as walls vs colored boundaries; doors are functional but visually generic. Under the new pixel-art shader, the gap is more visible — the player sees crisp pixel art of *colored rectangles*, which is worse than the previous approximation in some ways because the shader makes the lack of identity sharper.

This packet authors a **floor + wall + door visual vocabulary** designed for the pixel-art shader. Surface types (per UX bible §3.5 starting palette + world bible room categories):

**Floor materials (5 types):**
- `Carpet` — cubicle floor; muted gray-blue with subtle weave pattern at pixel-art resolution.
- `Linoleum` — kitchen / bathroom floor; pale yellow-green with subtle tile-line grid.
- `OfficeTile` — main hallway floor; off-white with subtle speckle pattern.
- `Concrete` — basement (when added), supply closet, mechanical rooms; gray with subtle aggregate texture.
- `Hardwood` — manager's-office-style premium room (used sparingly); warm brown with grain hint.

**Wall variants (3 types):**
- `CubicleWall` — 1.2m tall partial wall; muted gray-blue fabric appearance; doesn't reach ceiling; cube-tops visible from camera.
- `StructuralWall` — full-height structural wall; concrete-and-paint appearance; reaches ceiling.
- `WindowWall` — structural wall with window; lets light through (couples to existing lighting visualization from 3.1.C).

**Door variants (2 types for v0.2; more later):**
- `RegularDoor` — standard interior door; wood-look at pixel-art resolution; visible doorknob area; clearly hinged on one side.
- `RestroomDoor` — same physical shape as RegularDoor but with a restroom-symbol-pixel decal; readable at default altitude.

**Room-boundary affordances:**
- Subtle floor-trim tile (1-pixel-tall darker line) along where two floor materials meet, providing a "where does the kitchen start" visual anchor without explicit borders.
- Drop-ceiling indicator (rendered as a faint horizontal grid pattern at ceiling height when camera looks slightly upward — already partially implemented per `WallFadeController`).

After this packet:
- Each floor type has a dedicated material designed for the pixel-art shader.
- Each wall type has a dedicated material.
- Each door type has a dedicated material + identifying mark.
- Room boundaries are visually inferable from floor-material transitions.
- A `RoomVisualIdentityCatalog.json` content file maps room categories (from `RoomComponent.RoomKind`) to default floor/wall combinations — modder-extensible.
- Sandbox scene shows all floor types, all wall types, all door types side-by-side under pixel-art shader.

The 30-NPCs-at-60-FPS gate holds. URP material batching keeps cost bounded.

---

## Reference files

- `docs/UNITY-PACKET-PROTOCOL.md` — sandbox-first per Rule 2.
- `docs/c2-content/aesthetic-bible.md` — pixel-art-from-3D commitments; era-appropriate palette.
- `docs/c2-content/world-bible.md` — room category vocabulary (cubicle area, kitchen, bathroom, manager's office, supply closet, hallway).
- `docs/c2-content/ux-ui-bible.md` §1.6 (iconography), §2.1 (camera altitudes — typically 5-50m, default 15m).
- `docs/c2-infrastructure/MOD-API-CANDIDATES.md` — adds new MAC-014 (room visual identity catalog).
- `ECSUnity/Assets/Scripts/Render/RoomRectangleRenderer.cs` — read in full. The current "flat colored rectangle" baseline. This packet replaces flat-color rendering with material-based rendering.
- `ECSUnity/Assets/Scripts/Render/Lighting/RoomAmbientTintApplier.cs` — read for how rooms currently get color tint applied. Material-based rendering must coexist with lighting tint.
- `ECSUnity/Assets/Shaders/RoomTint.shader` (URP-migrated by 4.0.A) — read for the room-tint shader logic.
- `APIFramework/Components/RoomComponent.cs` — read for `RoomKind` enum (or whatever room-categorization exists today).
- `ECSUnity/Assets/Scripts/Build/BuildPaletteCatalog.cs` — read for how walls + doors are currently spawned via build mode (the same materials apply).
- `ECSUnity/Assets/Scripts/Render/Lighting/WallFadeController.cs` — read for wall-fade-on-occlusion behavior; new wall materials must continue to fade.

---

## Non-goals

- Do **not** add new room-category enum values. Use existing `RoomKind`s; add new ones in a future content packet if needed.
- Do **not** add procedural floor patterns (per-instance variation). v0.2 ships fixed materials per type; future packet for variation.
- Do **not** ship final hand-drawn art. This is placeholder-quality polish targeted at the pixel-art shader; final art is WP-4.1.2.
- Do **not** modify lighting calculations. New materials must respect existing lighting (URP's standard lit shader as base; room-tint applied per existing RoomTint shader).
- Do **not** add new build-palette items (carpet-as-placeable, etc.). v0.2 floor types are scene-authored, not player-placeable. Player-placeable floor tiles is a future build mode v3 feature.
- Do **not** modify camera behavior, occlusion, or fade. Wall fade continues to work via existing `WallFadeController`.
- Do **not** ship doors with animations (open/close swing). Functional state already exists; visual-only polish here.
- Do **not** add windows that show outside scenery. v0.2 `WindowWall` lets light through but doesn't render an outside view; future packet.
- Do **not** modify `WorldStateDto` schema unless absolutely necessary (room-material assignments are content, not runtime state).

---

## Design notes

### Material authoring

Each floor / wall / door material is a URP Lit material with a small custom texture. Textures are authored at low resolution (e.g., 64×64 or 128×128) to match the pixel-art shader's internal resolution — anything higher gets squashed by the shader's down-sampling pass anyway.

Texture style guidelines:
- **Carpet**: dithered-noise base in muted blue-gray; 1-pixel weave hints. ~64×64.
- **Linoleum**: pale yellow-green base with 8×8 tile-line grid in slightly darker shade. ~128×128 (so the tile lines align cleanly).
- **OfficeTile**: off-white with 1-pixel speckle (5% density, slightly darker). ~64×64.
- **Concrete**: gray with 2-pixel aggregate (medium-dark dots, 10% density). ~64×64.
- **Hardwood**: warm brown base with 1-pixel grain lines (vertical, varying spacing). ~64×128.
- **CubicleWall**: muted blue-gray fabric weave (similar palette to carpet but tighter weave). ~64×64.
- **StructuralWall**: off-white concrete-and-paint (smooth with subtle 2-pixel speckle). ~64×64.
- **WindowWall**: structural-wall base + a ~30% transparent strip along the upper portion (lets light through; rendered semi-transparent).
- **RegularDoor**: warm wood texture with a darker rectangular handle area at the right side. ~32×64.
- **RestroomDoor**: same as RegularDoor + a 16×16 restroom-symbol decal centered on the upper portion. ~32×64.

The Sonnet doesn't need to author final-quality textures — the bar is "reads as the intended material under pixel-art shader." Functional placeholders. WP-4.1.2 takes them to final.

### `RoomVisualIdentityCatalog.json`

New content file at `docs/c2-content/world-definitions/room-visual-identity.json`:

```jsonc
{
  "schemaVersion": "0.1.0",
  "roomCategories": [
    {
      "roomKind": "CubicleArea",
      "defaultFloorMaterial": "Carpet",
      "defaultWallMaterial": "CubicleWall",
      "defaultDoorMaterial": "RegularDoor",
      "trimMaterial": null
    },
    {
      "roomKind": "Kitchen",
      "defaultFloorMaterial": "Linoleum",
      "defaultWallMaterial": "StructuralWall",
      "defaultDoorMaterial": "RegularDoor",
      "trimMaterial": "tile-trim"
    },
    {
      "roomKind": "Bathroom",
      "defaultFloorMaterial": "Linoleum",
      "defaultWallMaterial": "StructuralWall",
      "defaultDoorMaterial": "RestroomDoor",
      "trimMaterial": "tile-trim"
    },
    {
      "roomKind": "Hallway",
      "defaultFloorMaterial": "OfficeTile",
      "defaultWallMaterial": "StructuralWall",
      "defaultDoorMaterial": "RegularDoor",
      "trimMaterial": null
    },
    {
      "roomKind": "ManagerOffice",
      "defaultFloorMaterial": "Hardwood",
      "defaultWallMaterial": "StructuralWall",
      "defaultDoorMaterial": "RegularDoor",
      "trimMaterial": "wood-trim"
    },
    {
      "roomKind": "SupplyCloset",
      "defaultFloorMaterial": "Concrete",
      "defaultWallMaterial": "StructuralWall",
      "defaultDoorMaterial": "RegularDoor",
      "trimMaterial": null
    },
    {
      "roomKind": "MechanicalRoom",
      "defaultFloorMaterial": "Concrete",
      "defaultWallMaterial": "StructuralWall",
      "defaultDoorMaterial": "RegularDoor",
      "trimMaterial": null
    }
  ],
  "materials": {
    "Carpet": "Materials/Floor_Carpet.mat",
    "Linoleum": "Materials/Floor_Linoleum.mat",
    // ...
  }
}
```

`RoomVisualIdentityLoader` reads at boot; `RoomRectangleRenderer` (or its successor) consults the catalog when rendering a room.

### Trim tiles

Floor-material transitions get a 1-pixel-tall darker trim tile. Implementation: a thin `Quad` mesh at the floor seam, rendered with a darker variant of the upstream floor's color. Rendered automatically by a `RoomBoundaryTrimRenderer` MonoBehaviour that queries adjacent rooms and emits trim quads where materials differ.

For v0.2, only emit trim where two named-floor types meet (skip transitions between same-type rooms — no need for trim between two CubicleAreas).

### Sandbox scene

`Assets/_Sandbox/floor-room-identity.unity`:
- A 60×30 grid divided into 8 zones, each rendering a different floor type:
  - Zone 1: Carpet (10×8 tiles)
  - Zone 2: Linoleum (10×8 tiles)
  - Zone 3: OfficeTile (10×8 tiles)
  - Zone 4: Concrete (10×8 tiles)
  - Zone 5: Hardwood (10×8 tiles)
  - Zones 6-8: empty (for future wall/door comparison rows)
- Walls separating zones 1-5: alternating CubicleWall and StructuralWall.
- Doors between zones 1-2, 2-3, 3-4: alternating RegularDoor and RestroomDoor.
- Trim tiles where floor types meet.
- Camera defaults to 15m altitude looking at the central zone; Talon can pan/zoom.
- A control panel UI:
  - "Toggle pixel-art" — A/B compare under-shader vs over-shader.
  - "Toggle lighting" — flip directional light + room ambient tint to see materials in different conditions.
  - Per-altitude buttons (8m / 15m / 25m / 40m) for camera-snap.

Test recipe walks Talon through floor identity → wall identity → door identity → trim verification → lighting interaction.

### Performance

URP material batching is the safety. With ~5 floor materials + 3 wall materials + 2 door materials = 10 unique materials per scene, URP's SRP Batcher handles this efficiently. Verify with the existing FPS-gate test variants.

### Sandbox vs integration

- **WP-4.0.D (this packet):** sandbox scene + materials + catalog + script changes. Production scenes unchanged.
- **WP-4.0.D-INT** (companion, drafted later): apply catalog to PlaytestScene + MainScene rooms; Talon's hands wire the catalog into existing room renderer.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| material | `ECSUnity/Assets/Materials/Floor_Carpet.mat` (new) | URP Lit material + carpet texture. |
| material | `ECSUnity/Assets/Materials/Floor_Linoleum.mat` (new) | URP Lit material + linoleum texture. |
| material | `ECSUnity/Assets/Materials/Floor_OfficeTile.mat` (new) | URP Lit material + tile texture. |
| material | `ECSUnity/Assets/Materials/Floor_Concrete.mat` (new) | URP Lit material + concrete texture. |
| material | `ECSUnity/Assets/Materials/Floor_Hardwood.mat` (new) | URP Lit material + hardwood texture. |
| material | `ECSUnity/Assets/Materials/Wall_Cubicle.mat` (new) | URP Lit material + cubicle-wall texture. |
| material | `ECSUnity/Assets/Materials/Wall_Structural.mat` (new) | URP Lit material + concrete-paint texture. |
| material | `ECSUnity/Assets/Materials/Wall_Window.mat` (new) | URP Lit material + window-strip variant. |
| material | `ECSUnity/Assets/Materials/Door_Regular.mat` (new) | URP Lit material + door texture. |
| material | `ECSUnity/Assets/Materials/Door_Restroom.mat` (new) | URP Lit material + door + restroom decal. |
| texture | `ECSUnity/Assets/Textures/floor_*.png` (10 files, additive) | The texture sources. Authored at low resolution per Design notes. |
| code | `ECSUnity/Assets/Scripts/Render/RoomVisualIdentityLoader.cs` (new) | Loads JSON catalog at boot. |
| code | `ECSUnity/Assets/Scripts/Render/RoomBoundaryTrimRenderer.cs` (new) | Emits trim quads where floor types differ. |
| code | `ECSUnity/Assets/Scripts/Render/RoomRectangleRenderer.cs` (modification) | Reads catalog; assigns material per room kind instead of flat color. |
| data | `docs/c2-content/world-definitions/room-visual-identity.json` (new) | The catalog. |
| scene | `ECSUnity/Assets/_Sandbox/floor-room-identity.unity` | Sandbox per Rule 4. |
| doc | `ECSUnity/Assets/_Sandbox/floor-room-identity.md` | 10-15 minute test recipe. |
| test | `APIFramework.Tests/Render/RoomVisualIdentityCatalogJsonTests.cs` | JSON validation. |
| test | `ECSUnity/Assets/Tests/Edit/RoomVisualIdentityLoaderTests.cs` | Loader behavior. |
| test | `ECSUnity/Assets/Tests/Play/RoomMaterialAssignmentTests.cs` | Each room kind gets correct material. |
| test | `ECSUnity/Assets/Tests/Play/PerformanceGate30NpcWithRoomMaterialsTests.cs` | FPS gate holds. |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Add MAC-014 (room visual identity catalog). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | All 10 materials compile and render correctly under URP. | Editor check |
| AT-02 | All 5 floor types visually distinct at default 15m altitude under pixel-art shader. | manual visual |
| AT-03 | All 3 wall types visually distinct. | manual visual |
| AT-04 | Both door types visually distinct (RestroomDoor decal readable at 15m). | manual visual |
| AT-05 | Trim tiles render at floor-material transitions but NOT at same-material transitions. | manual visual + unit |
| AT-06 | Catalog JSON validates; missing room kinds fall back to a default (configurable). | unit-test |
| AT-07 | Each `RoomKind` value gets the correct material from the catalog when rendered. | unit-test |
| AT-08 | Lighting (directional + room ambient tint) interacts correctly with new materials. | manual visual |
| AT-09 | Wall fade on camera occlusion still works for new wall materials. | manual visual |
| AT-10 | 30 NPCs in PlaytestScene with new materials hold ≥ 60 FPS. | play-mode test |
| AT-11 | All Phase 0–3 + Phase 4.0.A/A1/A1-INT/B/C/F/G tests stay green. | regression |
| AT-12 | `dotnet build` warning count = 0; all tests green. | build + test |
| AT-13 | MAC-014 added to `MOD-API-CANDIDATES.md`. | review |

---

## Mod API surface

This packet introduces **MAC-014: Room visual identity catalog (JSON-driven)**. Append to `MOD-API-CANDIDATES.md`:

> **MAC-014: Room visual identity catalog**
> - **What:** JSON catalog mapping `RoomKind` to floor/wall/door materials + trim. Modders adding a new room category (e.g., "Server Room", "Reception") add an entry; modders authoring custom material packs (e.g., a "concrete brutalist" material pack) reference custom materials in catalog overrides.
> - **Where:** `docs/c2-content/world-definitions/room-visual-identity.json`; `ECSUnity/Assets/Scripts/Render/RoomVisualIdentityLoader.cs`.
> - **Why a candidate:** Visual mods + room-type mods are common categories. Data-driven extension consistent with MAC-001 / MAC-005 / MAC-013.
> - **Stability:** fresh (lands with WP-4.0.D).
> - **Source packet:** WP-4.0.D.

The materials themselves are also Mod API-friendly (modders can drop in custom .mat files referenced by JSON), though the path-resolution mechanism for custom material packs is a future Mod API formalization concern.

---

## Followups (not in scope)

- `WP-4.0.D-INT` — apply catalog to PlaytestScene + MainScene. Talon's hands.
- New room kinds (Server Room, Reception, Conference Room). Future content packets.
- Per-instance floor variation (different cubicle floors look subtly different). Future depth.
- Animated doors (open/close swing). Future polish.
- Windows showing outside scenery. Future visual polish.
- Player-placeable floor tiles (build palette extension). Future build mode v3.
- Walls with damage states (cracks, holes). Future content for fire/disaster scenarios.
- Final hand-drawn material pass. WP-4.1.2.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: REQUIRED

Track 2 sandbox packet. Visual verification by Talon required.

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm worktree at `.claude/worktrees/sonnet-wp-4.0.d/` on branch `sonnet-wp-4.0.d` based on recent `origin/staging` (which now includes URP + pixel-art live).
1. Implement the spec.
2. Run all Unity tests + `dotnet test`. All must stay green.
3. Stage all changes including self-cleanup.
4. Commit on the worktree's feature branch.
5. Push the branch.
6. Stop. Notify Talon: `READY FOR VISUAL VERIFICATION — run Assets/_Sandbox/floor-room-identity.md (10-15 min recipe)`.

### Feel-verified-by-playtest acceptance flag

**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN (post-`-INT`):** does each floor type read as that material at typical camera altitudes? Are walls and doors visually distinct? Is a glance enough to identify what room I'm looking at?

### Cost envelope

Target: **$0.70**. Texture authoring + materials + catalog + script wiring. If cost approaches $1.20, escalate via `WP-4.0.D-blocker.md`.

Cost-discipline:
- Don't author final-quality textures — placeholder quality is the bar (WP-4.1.2 does final art).
- Use URP Lit shader as the material base for all 10 materials; don't author custom shaders.
- Reference existing room-rendering scripts; modify minimally.

### Self-cleanup on merge

Standard. Check for `WP-4.0.D-INT` as likely dependent.
