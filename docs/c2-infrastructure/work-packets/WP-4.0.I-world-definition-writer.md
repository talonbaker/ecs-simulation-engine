# WP-4.0.I — World Definition Writer (Runtime Save-As)

> **Wave 4 of the Phase 4.0.x foundational polish wave — authoring loop.** Per Talon's 2026-05-03 framing: "I don't have a good way to get items into the world without a predefined scene. I need to be able to see the scene in the game preview." The substrate already exists: `WorldDefinitionLoader` + `WorldDefinitionDto` boot-load JSON scenes from `docs/c2-content/world-definitions/`. This packet completes the round-trip — adds `WorldDefinitionWriter` so the *current* world state can be serialized back into the same JSON format. Authored scenes hot-reload via the existing loader; no recompile required.

**Tier:** Sonnet
**Depends on:** WP-3.2.0 (save/load round-trip hardening — proves serialization discipline), WP-3.0.4 (`IWorldMutationApi` — the structural mutation contract authored scenes are built with), WP-4.0.G (build mode v2 — the in-game placement loop that produces world state worth saving).
**Parallel-safe with:** WP-4.0.J (author-mode toggle/palette — depends on this packet's writer existing but disjoint files), WP-4.0.K (NPC authoring — disjoint), WP-4.0.L (docs — disjoint).
**Timebox:** 120 minutes
**Budget:** $0.55
**Feel-verified-by-playtest:** NO (engine packet; J/K/L provide the user-visible surfaces this enables)

---

## Goal

Today, world-definition JSONs at `docs/c2-content/world-definitions/*.json` are **read-only at runtime**: `WorldDefinitionLoader` parses them at boot and spawns rooms / lights / NPC slots, but there is no inverse path. To author a new scene, Talon hand-edits JSON, restarts, observes, repeats. This is the recompile-equivalent friction that blocks creator workflow.

This packet adds `WorldDefinitionWriter` — a pure C# serializer that walks the current `WorldStateDto` (or directly the live ECS world) and emits a JSON file in the **exact same shape** the loader consumes. Round-trip discipline: `Load(file) → mutate → Write(file)` produces a byte-equivalent (or semantically-equivalent) file.

Initial scope intentionally narrow per Talon: **topology + props + NPC roster + lighting**. NOT in this packet: in-flight simulation state (drives, memories, mid-action NPCs, schedules). That's "save game" territory and lives in a future packet that builds on this one.

After this packet:
- `WorldDefinitionWriter.WriteToFile(path, worldState)` produces a valid `world-definition.json` from the live world.
- Round-trip test: load `playtest-office.json` → write to a temp file → load that file → resulting world entities match.
- Schema-validated output: writer emits `schemaVersion` matching the current loader's accepted versions; `WorldDefinitionLoader` validates it on next load.
- WARDEN-only `WorldDefinitionWriterCommand` in the dev console: `save-world <name>` writes the current world to `docs/c2-content/world-definitions/<name>.json`.
- Hot-reload path: `reload-world <name>` clears the current world and re-loads from disk via `WorldDefinitionLoader`. (Existing loader is reused; no Unity-side scene reload required — the engine ticks continuously through the swap.)

The 30-NPCs-at-60-FPS gate is irrelevant here (writer is dev-tool, not per-frame). Writer cost target: serialize a 50-room / 100-NPC-slot / 200-prop world in under 100ms (well under any interactive threshold).

---

## Reference files

- `docs/c2-infrastructure/PHASE-3-HANDOFF.md` (if present) and `docs/c2-infrastructure/PHASE-4-KICKOFF-BRIEF.md` — wave-context.
- `docs/c2-infrastructure/MOD-API-CANDIDATES.md` — adds MAC-016 (world-definition file format as Mod API surface, this packet introduces).
- `docs/c2-content/world-definitions/playtest-office.json` — the canonical example. Read in full. Writer output must round-trip this file.
- `docs/c2-content/world-definitions/office-starter.json` — second example; second round-trip test target.
- `APIFramework/Bootstrap/WorldDefinitionDto.cs` — read in full. The writer's serialization target shape.
- `APIFramework/Bootstrap/WorldDefinitionLoader.cs` — read in full. The writer is its inverse; behavior must be symmetric.
- `APIFramework/Bootstrap/WorldDefinitionInvalidException.cs` — the writer must produce JSON that this validator accepts.
- `APIFramework/Mutation/IWorldMutationApi.cs` — read for the structural mutation contract; mutations performed in author mode (J/K) flow through here and are observable in the resulting world state the writer serializes.
- `APIFramework/Core/SimulationBootstrapper.cs` — read for boot-time service registration (writer registers as a service for dev-console + author-mode access).
- `APIFramework.Tests/Bootstrap/WorldDefinitionLoaderTests.cs` (assumed-existing) — the loader test pattern; new round-trip tests mirror.

---

## Non-goals

- Do **not** serialize in-flight simulation state. Drives, stress, memories, in-progress actions, schedule cursors, dialog history are NOT written. (That's save-game, future packet.)
- Do **not** modify the existing JSON schema. Writer emits the format the loader already accepts; if the format needs extension (e.g., for build-mode-placed props that don't currently appear in any world-definition file), bump `schemaVersion` minor and add the new section.
- Do **not** modify `WorldDefinitionLoader` semantics. If a load test passes today, it must still pass.
- Do **not** add author-mode UI in this packet. The dev-console command is the only user surface here; the proper UI lands in WP-4.0.J.
- Do **not** wire the writer into `WorldStateDto` save/load (the v0.5 schema). Those are separate concerns: `WorldStateDto` is runtime state snapshot; `WorldDefinitionDto` is authored-scene definition. Don't conflate.
- Do **not** strip the writer at ship. World-definition authoring is dev-time-only by intent (modders use it, not players), but the writer itself is pure C# and lives in `APIFramework`. The dev-console command is `#if WARDEN`-gated; the writer class isn't (cheap, no telemetry surface).
- Do **not** ship undo/redo for the writer. The build mode itself owns undo/redo (WP-4.0.G); the writer just snapshots. If author wants to "go back," they reload the previous file.
- Do **not** introduce new dependencies. `System.Text.Json` (already in use by loader) is the only serializer.

---

## Design notes

### Writer architecture

Pure C# class in `APIFramework/Bootstrap/WorldDefinitionWriter.cs`:

```csharp
namespace APIFramework.Bootstrap;

public sealed class WorldDefinitionWriter
{
    private readonly World _world;
    private readonly JsonSerializerOptions _opts;

    public WorldDefinitionWriter(World world)
    {
        _world = world;
        _opts = new JsonSerializerOptions {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>Serializes the current world to a WorldDefinitionDto and writes it as JSON.</summary>
    public void WriteToFile(string absolutePath, string worldId, string worldName, int seed = 0)
    {
        var dto = BuildDto(worldId, worldName, seed);
        var json = JsonSerializer.Serialize(dto, _opts);
        File.WriteAllText(absolutePath, json);
    }

    /// <summary>Serializes to a string (used by tests, dev-console preview).</summary>
    public string WriteToString(string worldId, string worldName, int seed = 0)
    {
        var dto = BuildDto(worldId, worldName, seed);
        return JsonSerializer.Serialize(dto, _opts);
    }

    internal WorldDefinitionDto BuildDto(string worldId, string worldName, int seed)
    {
        return new WorldDefinitionDto {
            SchemaVersion    = WorldDefinitionLoader.CurrentSchemaVersion,
            WorldId          = worldId,
            Name             = worldName,
            Seed             = seed,
            Floors           = SerializeFloors(),
            Rooms            = SerializeRooms(),
            LightSources     = SerializeLightSources(),
            LightApertures   = SerializeLightApertures(),
            NpcSlots         = SerializeNpcSlots(),
            ObjectsAtAnchors = SerializeAnchorObjects(),
        };
    }

    // Per-section serializers walk the ECS, filter by tag (RoomTag, LightSourceTag, NpcTag, etc.),
    // and emit DTO entries. Uses ComponentStore<T> for efficient typed access.
    private FloorDefinitionDto[]   SerializeFloors()         { /* ... */ }
    private RoomDefinitionDto[]    SerializeRooms()          { /* ... */ }
    private LightSourceDefDto[]    SerializeLightSources()   { /* ... */ }
    private LightApertureDefDto[]  SerializeLightApertures() { /* ... */ }
    private NpcSlotDto[]           SerializeNpcSlots()       { /* ... */ }
    private AnchorObjectDto[]      SerializeAnchorObjects()  { /* ... */ }
}
```

### NPC slot serialization

The existing `npcSlots` block in world-definitions is a *spawn-time hint* (room, position, archetype, name). Live NPCs at runtime have far more state (drives, relationships, etc.) — but per non-goals, the writer emits **only the slot-equivalent**: room, current position (rounded to integer tile), archetype, name. Round-trip semantics:

- Loaded slot → spawned NPC at boot.
- Writer recovers slot from the live NPC: `roomId = NPC's current room; x,y = floor(position); archetypeHint = NPC's ArchetypeComponent.Kind; nameHint = NPC's NameComponent.Name`.
- Re-load → respawn at last-known position with same archetype/name. Drives reset to defaults.

This is the right behavior for level authoring: "where do NPCs start" is authored; "how they evolve" is simulation.

### Schema extension: structural props

The existing world-definitions don't include arbitrary structural props placed via build mode (cubicle desks, walls placed mid-session, etc.). For round-trip completeness we need a new section:

```json
"placedProps": [
  {
    "id": "prop-desk-001",
    "templateId": "00000020-0000-0000-0000-000000000001",
    "x": 5, "y": 7,
    "rotation": 0,
    "roomId": "cubicle-main"
  }
]
```

Where `templateId` matches the existing `BuildPaletteCatalog.PaletteEntry.TemplateId`. The loader needs a small extension to spawn from this section using existing build-mode prop instantiation. **Schema bump: 0.1.0 → 0.2.0** (additive minor; loader still accepts 0.1.0 files by treating `placedProps` as empty).

### Dev-console commands

WARDEN-only commands in `Warden.DevConsole` (or wherever existing console commands live):

- `save-world <name>` — writes `docs/c2-content/world-definitions/<name>.json` from current world. Refuses to overwrite existing files unless `--force` is given.
- `reload-world <name>` — clears the current world and re-loads `<name>.json` via `WorldDefinitionLoader`. Engine ticks pause for the duration of the swap (one tick max), then resume.
- `list-worlds` — lists all `world-definition` files currently on disk with timestamps.

Path resolution rule: `<name>` is resolved against `docs/c2-content/world-definitions/` (the existing canonical directory). Absolute paths and `..` segments are rejected (defense-in-depth — even though this is dev-mode-only, no need to allow path traversal).

### Round-trip test discipline

The acceptance test that proves this works:

```csharp
[Fact]
public void RoundTrip_PlaytestOffice_PreservesAllSections()
{
    // 1. Load the canonical file.
    var loader = new WorldDefinitionLoader(...);
    var w1 = loader.LoadFromFile("docs/c2-content/world-definitions/playtest-office.json");

    // 2. Write the current world to a temp file.
    var writer = new WorldDefinitionWriter(w1);
    var tmp = Path.GetTempFileName();
    writer.WriteToFile(tmp, worldId: "round-trip", worldName: "Round Trip", seed: 20260101);

    // 3. Load the written file into a fresh world.
    var w2 = loader.LoadFromFile(tmp);

    // 4. Compare key counts + structural identity.
    Assert.Equal(CountRooms(w1), CountRooms(w2));
    Assert.Equal(CountLightSources(w1), CountLightSources(w2));
    Assert.Equal(CountNpcs(w1), CountNpcs(w2));
    AssertRoomBoundsEquivalent(w1, w2);
    AssertNpcArchetypesEquivalent(w1, w2);
}
```

Two test variants: `playtest-office.json` and `office-starter.json`. Both must round-trip cleanly.

### Failure modes

- **Missing required field on entity**: e.g., a room without a `RoomComponent.Bounds`. Writer throws `WorldDefinitionWriterException` with the offending entity ID. (Should never happen in a well-formed world; this is defense-in-depth.)
- **Unknown enum value**: e.g., a light source with a `LightSourceKind` not representable in the JSON enum strings. Writer throws with the enum value. Add new strings to the loader's parser when the enum grows.
- **File path issues**: writer uses `File.WriteAllText`; standard IO exceptions propagate. Dev-console wraps them with a friendly error.

### Performance

Writer is dev-tool. 100-NPC / 200-prop / 50-room world serializes in well under 100ms (`System.Text.Json` is fast; the bottleneck is the ECS walk). No optimization needed.

### What this enables

Once this packet lands, the workflow is:

1. Boot game with `playtest-office.json` (or any starter scene, even an empty-but-valid one).
2. Use existing build mode (4.0.G) to place props.
3. (After WP-4.0.J merges:) Use author mode to draw rooms, place lights, place NPC slots.
4. `> save-world my-test-scene`
5. Restart, use `> reload-world my-test-scene` (or set as boot scene).
6. Iterate.

No recompile. No hand-editing JSON unless desired. The JSON file is human-readable and modder-extensible — exactly the "standard way" for community contributions Talon called for.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Bootstrap/WorldDefinitionWriter.cs` (new) | Writer class with per-section serializers. |
| code | `APIFramework/Bootstrap/WorldDefinitionDto.cs` (modification) | Add `PlacedPropDto[] PlacedProps { get; set; }`; add `PlacedPropDto` record. |
| code | `APIFramework/Bootstrap/WorldDefinitionLoader.cs` (modification) | Bump `CurrentSchemaVersion` to `0.2.0`; accept `placedProps` (additive); spawn placed props via existing template-instantiation path. |
| code | `APIFramework/Bootstrap/WorldDefinitionWriterException.cs` (new) | Specific exception for writer failures. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modification) | Register `WorldDefinitionWriter` as a service. |
| code | `Warden.DevConsole/Commands/SaveWorldCommand.cs` (new, WARDEN-gated) | `save-world <name> [--force]`. |
| code | `Warden.DevConsole/Commands/ReloadWorldCommand.cs` (new, WARDEN-gated) | `reload-world <name>`. |
| code | `Warden.DevConsole/Commands/ListWorldsCommand.cs` (new, WARDEN-gated) | `list-worlds`. |
| data | `docs/c2-content/world-definitions/playtest-office.json` (modification) | Add empty `"placedProps": []` block (forward compat). |
| data | `docs/c2-content/world-definitions/office-starter.json` (modification) | Same. |
| test | `APIFramework.Tests/Bootstrap/WorldDefinitionWriterTests.cs` (new) | Per-section serialization correctness. |
| test | `APIFramework.Tests/Bootstrap/WorldDefinitionRoundTripTests.cs` (new) | Load → write → load equivalence for both canonical files. |
| test | `APIFramework.Tests/Bootstrap/WorldDefinitionSchemaBumpTests.cs` (new) | 0.1.0 files still load (no `placedProps` block); 0.2.0 files load with `placedProps`. |
| test | `APIFramework.Tests/Bootstrap/WorldDefinitionWriterExceptionTests.cs` (new) | Failure modes. |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` (modification) | Add MAC-016 (world-definition file format as Mod API surface). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `WorldDefinitionWriter.WriteToString` emits a JSON document that parses back into a `WorldDefinitionDto`. | unit-test |
| AT-02 | Round-trip of `playtest-office.json`: load → write → load yields equivalent rooms (count, ids, bounds). | round-trip-test |
| AT-03 | Round-trip of `playtest-office.json`: light sources, apertures, NPC slots, anchor objects all preserved. | round-trip-test |
| AT-04 | Round-trip of `office-starter.json`: same coverage. | round-trip-test |
| AT-05 | `placedProps` round-trips: a build-mode-placed cubicle desk loaded by W2 spawns at the same tile as in W1. | round-trip-test |
| AT-06 | Schema 0.1.0 files (without `placedProps`) load successfully and produce empty placed-props collection. | back-compat-test |
| AT-07 | Writer rejects a malformed world (e.g., room without bounds) with `WorldDefinitionWriterException` naming the offending entity. | unit-test |
| AT-08 | `save-world` dev-console command refuses to overwrite an existing file without `--force`. | integration-test |
| AT-09 | `reload-world` swaps the current world without crashing the engine; tests run after reload pass. | integration-test |
| AT-10 | `save-world` rejects path traversal (`..`) and absolute paths. | unit-test |
| AT-11 | Writer cost: 100-NPC / 200-prop / 50-room world serializes in < 100ms on the test rig. | perf-test |
| AT-12 | All Phase 0–3 + Phase 4.0.A–H tests stay green. | regression |
| AT-13 | `dotnet build` warning count = 0; all tests green. | build + test |
| AT-14 | MAC-016 added to `MOD-API-CANDIDATES.md`. | review |

---

## Mod API surface

This packet introduces **MAC-016: World-definition file format (load + write round-trip)**. Append to `MOD-API-CANDIDATES.md`:

> **MAC-016: World-definition file format**
> - **What:** JSON schema (now v0.2.0) describing a complete authorable scene — floors, rooms, light sources, light apertures, NPC slots, anchor objects, placed props. `WorldDefinitionLoader` spawns from JSON; `WorldDefinitionWriter` serializes the current world back to JSON. Round-trip-validated. Modders authoring custom scenes (or scene packs) add JSON files under `docs/c2-content/world-definitions/`; the existing dev-console `reload-world` command makes them live without recompile.
> - **Where:** `APIFramework/Bootstrap/WorldDefinitionLoader.cs`; `APIFramework/Bootstrap/WorldDefinitionWriter.cs`; `APIFramework/Bootstrap/WorldDefinitionDto.cs`; `docs/c2-content/world-definitions/*.json`.
> - **Why a candidate:** The clearest modder surface in the project. A "custom office" mod is a single JSON file; a "scene pack" mod is a directory of them. Pattern is consistent with MAC-001 / MAC-005 / MAC-013 / MAC-014 — data-driven, schema-validated, registered through file discovery.
> - **Stability:** stabilizing (loader stable since Phase 1; writer + round-trip lands with WP-4.0.I; second consumer = author-mode UI in WP-4.0.J).
> - **Source packets:** Phase 1 substrate (loader); WP-4.0.I (writer + schema bump 0.2.0).

---

## Followups (not in scope)

- WP-4.0.J — author-mode UI (extended palette: rooms, lights, apertures). First consumer of the writer's UX path.
- WP-4.0.K — NPC authoring in author mode. Uses NPC-slot serialization.
- WP-4.0.L — authoring documentation + ledger entries (FF-016, MAC-015).
- Future packet: in-flight simulation state save (drives, memories, in-flight actions). Builds on this writer's discipline; couples to existing `WorldStateDto` v0.5.
- Future packet: scene diff / merge tooling. Two scene files, see what changed. Useful for community PR review.
- Future packet: scene validation CLI (`ECSCli validate-scene <path>`). Headless, no Unity. CI-runnable for community contributions.
- Future packet: scene thumbnails (auto-render top-down preview when a scene is saved). UX polish.

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: NOT required

Engine packet. No new user-visible UI surface (dev-console commands only, behind WARDEN). `dotnet test` green + round-trip tests passing is the gate.

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm worktree at `.claude/worktrees/sonnet-wp-4.0.i/` on branch `sonnet-wp-4.0.i` based on recent `origin/staging`.
1. Implement the spec.
2. Run `dotnet test`. All must stay green.
3. Stage all changes including self-cleanup.
4. Commit on the worktree's feature branch.
5. Push the branch.
6. Stop. Notify Talon: `READY FOR REVIEW — engine packet, no visual verification needed; round-trip tests are the gate`.

### Feel-verified-by-playtest acceptance flag

**Feel-verified-by-playtest:** NO

Engine substrate. WP-4.0.J / K provide the user-visible surface that earns the playtest evaluation.

### Cost envelope

Target: **$0.55**. Writer + DTO extension + 3 dev-console commands + 4 test files. If cost approaches $0.90, escalate via `WP-4.0.I-blocker.md`.

Cost-discipline:
- Reuse `System.Text.Json` (no new serializer dependency).
- Don't refactor the loader — additive changes only (accept new optional `placedProps` block).
- Don't ship undo/redo in the writer; reload-from-disk is the recovery path.

### Self-cleanup on merge

Standard. Check for `WP-4.0.J` and `WP-4.0.K` as likely dependents.
