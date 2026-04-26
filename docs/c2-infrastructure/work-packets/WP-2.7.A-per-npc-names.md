# WP-2.7.A — Per-NPC Name Generation

**Tier:** Sonnet
**Depends on:** WP-1.8.A (cast generator)
**Parallel-safe with:** WP-2.8.A, WP-2.9.A, WP-2.3.B (different file footprints)
**Timebox:** 45 minutes
**Budget:** $0.20

---

## Goal

Cast generator currently spawns NPCs without names. The world bible implies real names — Donna, Frank, Greg. This packet ships a deterministic name generator that draws from a culturally-appropriate early-2000s American office name pool, attaches `IdentityComponent.Name` to each spawned NPC, and guarantees uniqueness per save.

After this packet, every NPC has a name the projector can surface and tools/tests can reference. Names persist with the save (per SRD §8.2 — `IdentityComponent` is part of the world state).

---

## Reference files

- `docs/c2-content/world-bible.md` — setting (early-2000s American office) constrains naming era and demographic.
- `docs/c2-content/cast-bible.md` — archetype catalog. The Vent → Donna, the Hermit → Greg, the Affair → Frank are the bible's named exemplars; this packet's pool should plausibly produce those when the seed permits.
- `docs/c2-infrastructure/00-SRD.md` §8.2 (save/load reuses WorldStateDto — `IdentityComponent.Name` becomes part of the saved world).
- `APIFramework/Components/IdentityComponent.cs` — already exists with `Name` and `Value` fields. **No modification needed**; this packet just populates it at spawn.
- `APIFramework/Bootstrap/CastGenerator.cs` — the spawn site. Modify `SpawnSingle` (or whatever the per-NPC spawn method is named) to read a name from the pool and attach an `IdentityComponent`.
- `APIFramework/Components/CastSpawnComponents.cs` — `NpcArchetypeComponent.ArchetypeId`. The pool may bias names per archetype (e.g., the Founder's Nephew tends toward generationally newer names); v0.1 keeps it simple — uniform sampling.
- `APIFramework/Core/SeededRandom.cs` — RNG source. The cast generator already takes a seed; threading the same seed through name selection keeps reproduction deterministic.
- `APIFramework/Components/Tags.cs` — `NpcTag` marks NPCs.
- `Warden.Telemetry/TelemetryProjector.cs` — already projects `IdentityComponent.Name` into the entity DTO if present. Confirm during reading; modify only if needed.
- `Warden.Contracts/Telemetry/EntityDto.cs` (or similar) — entity DTO surface to confirm names flow out.

## Non-goals

- Do **not** modify `IdentityComponent`. It already has `Name`; just populate it.
- Do **not** add per-archetype name biases at v0.1. Uniform sampling from the pool is sufficient. (Future polish can add archetype-aware biasing.)
- Do **not** add nicknames, last names, or honorifics. v0.1 is first names only. The bible mentions Donna/Greg/Frank as full identifiers.
- Do **not** modify the projector (it already serializes `IdentityComponent`).
- Do **not** add player-facing rename functionality. Names are deterministic from seed.
- Do **not** introduce a NuGet dependency.
- Do **not** retry, recurse, or "self-heal" on test failure. Fail closed per SRD §4.1.
- Do **not** add a runtime LLM dependency anywhere. (SRD §8.1.)
- Do **not** include any test that depends on `DateTime.Now`, `System.Random`, or wall-clock timing.

---

## Design notes

### The name pool

`docs/c2-content/cast/name-pool.json`:

```jsonc
{
  "schemaVersion": "0.1.0",
  "firstNames": [
    "Donna", "Frank", "Greg", "Karen", "Bob", "Linda", "Steve", "Susan",
    "Mark", "Dave", "Jennifer", "Mike", "Kevin", "Patricia", "Brian",
    "Sandra", "Kyle", "Megan", "Rick", "Nicole", "Tony", "Heather",
    "Jeff", "Tracy", "Doug", "Cheryl", "Ed", "Tina", "Wayne", "Carol",
    "Ron", "Pam", "Phil", "Diane", "Hank", "Lisa", "Carl", "Beth",
    "Tom", "Maria", "Jim", "Christine", "Ray", "Holly", "Pete",
    "Gloria", "Larry", "Joan", "Stan", "Margaret"
  ]
}
```

The Sonnet authors a list of ~50 culturally appropriate early-2000s American office first names. Mix of ages and genders. The bible's named exemplars (Donna, Greg, Frank) MUST be present so a fresh world plausibly reproduces them when seeded.

### Selection algorithm

```csharp
// in CastGenerator, per NPC:
var pool = NamePoolLoader.Load();           // cached, loaded once
var available = pool.FirstNames.Except(usedNames).ToList();
if (available.Count == 0)
    throw new InvalidOperationException(
        "Name pool exhausted. Add more entries to name-pool.json or reduce cast size.");
var idx = rng.Next(available.Count);        // SeededRandom
var name = available[idx];
usedNames.Add(name);
entity.Add(new IdentityComponent(name));
```

`usedNames` is a `HashSet<string>` scoped to the current `SpawnAll` call — names are unique within the cast, but a different save (different seed) is free to reuse.

### Determinism

`SeededRandom` advances deterministically; processing NPCs in `EntityIntId`-ascending order keeps name assignments stable across runs.

### Data file

`docs/c2-content/cast/name-pool.json`. Mirrors the pattern of `archetypes.json` and `archetype-schedules.json`. New folder `cast/` since this is the first per-NPC content data file.

### Tests

- `NamePoolLoaderTests.cs` — JSON loads, ≥30 entries, all unique strings, all non-empty.
- `CastGeneratorNameAssignmentTests.cs` — given seed S and a 5-NPC cast, all 5 NPCs have distinct `IdentityComponent.Name` populated from the pool. Same seed produces same names; different seeds produce different names.
- `CastGeneratorNameExhaustionTests.cs` — requesting more NPCs than the pool contains throws `InvalidOperationException` with a descriptive message.
- `NamePoolBibleExemplarsTests.cs` — assert `Donna`, `Greg`, `Frank` are all present in the pool (the bible's named exemplars must be reproducible).
- `IdentityProjectionTests.cs` — projector serialises `IdentityComponent.Name` to the entity DTO.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Bootstrap/CastGenerator.cs` (modified) | Add name selection to per-NPC spawn; load pool once; track used names within the spawn call. |
| code | `APIFramework/Bootstrap/NamePoolLoader.cs` | Loader for `name-pool.json`. Cached. |
| data | `docs/c2-content/cast/name-pool.json` | ~50 first names; must include Donna, Greg, Frank. |
| code | `APIFramework.Tests/Bootstrap/NamePoolLoaderTests.cs` | JSON validation. |
| code | `APIFramework.Tests/Bootstrap/CastGeneratorNameAssignmentTests.cs` | Determinism + uniqueness. |
| code | `APIFramework.Tests/Bootstrap/CastGeneratorNameExhaustionTests.cs` | Pool exhaustion failure mode. |
| code | `APIFramework.Tests/Bootstrap/NamePoolBibleExemplarsTests.cs` | Bible names present. |
| code | `Warden.Telemetry.Tests/IdentityProjectionTests.cs` (new or extended) | Projector emits names. |
| doc | `docs/c2-infrastructure/work-packets/_completed/WP-2.7.A.md` | Completion note. List the final name count, exemplars confirmed, any naming choices the Sonnet made. |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `name-pool.json` loads cleanly; ≥ 30 entries; all unique non-empty strings. | unit-test |
| AT-02 | Pool contains "Donna", "Greg", "Frank". | unit-test |
| AT-03 | Cast generator with seed S, 5 NPCs → all NPCs have `IdentityComponent.Name` from the pool; all 5 names are distinct. | unit-test |
| AT-04 | Same seed reproduces the same name assignments. | unit-test |
| AT-05 | Different seeds produce different name assignments (with high probability — verify across 10 seeds). | unit-test |
| AT-06 | Requesting more NPCs than pool size throws `InvalidOperationException` with descriptive message. | unit-test |
| AT-07 | Projector populates `EntityDto.Name` (or whatever the field is) from `IdentityComponent.Name`. | unit-test |
| AT-08 | All Wave 1, Wave 2, Wave 3 acceptance tests stay green. | regression |
| AT-09 | `dotnet build ECSSimulation.sln` warning count = 0. | build |
| AT-10 | `dotnet test ECSSimulation.sln` — all green. | build + unit-test |

---

## Followups (not in scope)

- **Per-archetype name biasing** — the Founder's Nephew leans newer ("Tyler", "Kayden"); the Old Hand leans older ("Margaret", "Stan"). Add `archetypeNameWeights` per archetype.
- **Last names** — for player-facing identification at higher densities (15+ NPCs).
- **Cultural diversity sweep** — review the pool for a broader American demographic mix appropriate to the bible's setting.
- **Name reuse across saves** — currently each save's name set is independent. Cross-save name diversity could be a polish concern.
