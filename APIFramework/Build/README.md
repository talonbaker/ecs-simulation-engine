# APIFramework.Build

Author-mode palette catalog + build-footprint substrate. Mostly Unity-side concerns (the actual build mode lives in `ECSUnity/Assets/Scripts/BuildMode/`); this namespace holds the **engine-side data layer** that the Unity tools consume.

## What's in here

| Type | Purpose |
|:---|:---|
| `BuildFootprintCatalog` | Per-prop occupancy footprint metadata (WP-4.0.C). Defines tile area, surface heights, stack-on-top compatibility per prop template. Loaded from `docs/c2-content/build/build-footprint-catalog.json`. |
| `BuildFootprintEntry` | Single catalog row. |
| `FootprintGeometry` | Helper for footprint-aware drop calculations (WP-4.0.G). |
| `AuthorModePaletteData` | POCO mirror of `docs/c2-content/build/author-mode-palette.json` (WP-4.0.J). The author-mode palette: 9 room kinds, 9 light source kinds, 4 aperture sizes. |
| `AuthorModeRoomEntry` / `AuthorModeLightSourceEntry` / `AuthorModeLightApertureEntry` | Per-row records of the palette. |
| `AuthorModePaletteLoader` | Loads `author-mode-palette.json`. Lazy default cache; fail-closed validation. |

## Catalogs

- `docs/c2-content/build/build-footprint-catalog.json` — per-prop occupancy substrate (Wave 1).
- `docs/c2-content/build/author-mode-palette.json` — author-mode palette (Wave 4). Modder-extensible: add a new room kind by extending `rooms[]` + the loader's `RoomKind` parser + the room visual identity catalog (MAC-014).

## Typical use

```csharp
// Footprint lookup (build mode v2 placement validation):
var footprintCatalog = BuildFootprintCatalog.LoadDefault();
var entry            = footprintCatalog.TryGet(templateId);
if (entry != null && FootprintGeometry.FitsAt(entry, tileX, tileY, em)) { ... }

// Author-mode palette (Unity side reads this to populate UI tabs):
var palette = AuthorModePaletteLoader.LoadDefault();
foreach (var room in palette.Rooms)
    Debug.Log($"{room.Label} ({room.RoomKind}) — {room.Tooltip}");
```

## Consumers

- **`ECSUnity/Assets/Scripts/BuildMode/BuildModeController`** — player-facing build mode placement validation against footprints.
- **`ECSUnity/Assets/Scripts/BuildMode/AuthorModeController`** — WARDEN-only author mode reads the palette catalog.

## Authoring contract

Both catalogs ship with the project; modders extend by editing the JSON files. Schema is documented inline via XML doc comments on the `*Data` POCO classes. Loaders are liberal in what they accept (additive fields are silently passed through; missing required fields fail-closed with a clear error message).

## See also

- `docs/c2-infrastructure/MOD-API-CANDIDATES.md#MAC-011` (BuildFootprintComponent) and `#MAC-015` (extended author-mode palette).
- `docs/c2-content/world-definitions/README.md` — level designer quickstart for the in-game authoring loop.
- `APIFramework.Tests/Build/` — palette loader tests.
