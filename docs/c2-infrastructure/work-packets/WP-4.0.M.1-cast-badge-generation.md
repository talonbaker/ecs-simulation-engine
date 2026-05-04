# WP-4.0.M.1 — Cast Badge Generation Port

> **Orthogonal extension to WP-4.0.M (cast name generator).** Completes the deferred portion of M's port by adding badge generation — the per-tier visual-flair layer (clearance, sticker, condition, signature, department stamp) from `~/talonbaker.github.io/name-face-gen/data.js#generateBadge`. Pure C# library extension; no UI consumer yet (badge data sits ready for the future hire-screen / inspector-tab UI). Independent of the Phase 4.2.x zone work — can land any time.

**Tier:** Sonnet (small)
**Depends on:** WP-4.0.M (cast name generator + name-data.json catalog including `badgeFlair` and `departmentStamps` blocks already present).
**Parallel-safe with:** Everything (pure additive library extension; no consumers to break).
**Timebox:** 60 minutes
**Budget:** $0.30
**Feel-verified-by-playtest:** NO (no UI consumer in this packet)

---

## Goal

Port `generateBadge(nameResult)` from the JS source. Each tier produces a different badge composition:

| Tier | Badge contents |
|:---|:---|
| Common | condition (Slightly Chewed, Coffee Stained...), access (Vending Machine Authorized, Restricted from the Good Chairs...), note (one office sin) |
| Uncommon | condition, sticker (I Survived the 3:00 PM Meeting...), access |
| Rare | condition, note, sticker |
| Epic | sticker + departmentStamp (auto-derived from title text — "Synergy Dept." stamp if title contains "synergy") |
| Legendary / Mythic | clearance (Omega-Level...), legacy (Founding Member of the Watercooler Council...), signature (Triple-Laminated...), departmentStamp |

The badge is computed from a `CastNameResult` (output of `CastNameGenerator.Generate`) plus the same RNG. Same deterministic-seedable pattern as the parent.

After this packet:
- `CastBadgeGenerator.Generate(rng, CastNameResult)` returns a `CastBadgeResult`.
- The badge is structured (no UI rendering yet); Unity-side consumers (future hire-screen) format it into the inspector / hire-card UI.
- Badges are stable: same seed + same NameResult → same badge.
- `findDeptStamp(title)` heuristic ported (looks for substring matches against `departmentStamps[].id` in the title).

---

## Reference files

- `~/talonbaker.github.io/name-face-gen/data.js` — `generateBadge` + `findDeptStamp` functions are the source-of-truth port targets.
- `APIFramework/Cast/CastNameGenerator.cs` (from M) — the parent. Same patterns, namespacing, and JS-vs-C#-RNG-equivalence caveat apply.
- `APIFramework/Cast/CastNameData.cs` (from M) — already has `BadgeFlair` and `DepartmentStamps` POCOs (preserved during M's port specifically for this).
- `APIFramework/Cast/CastNameResult.cs` (from M) — input to badge generation.

---

## Non-goals

- Do **not** ship badge UI rendering. Pure data layer.
- Do **not** modify the catalog. Data already exists from M's port.
- Do **not** modify `CastNameGenerator`. Badges are computed AFTER name generation, not as part of it.

---

## Design notes

### Public API

```csharp
namespace APIFramework.Cast;

public sealed record CastBadgeResult(
    string?              Title,                // mirrored from CastNameResult.Title for convenience
    string?              Condition,
    string?              Note,
    string?              Access,
    string?              Sticker,
    string?              Clearance,
    string?              Legacy,
    string?              Signature,
    DepartmentStampDto?  DepartmentStamp);

public sealed class CastBadgeGenerator
{
    private readonly CastNameData _data;
    public CastBadgeGenerator(CastNameData data) { _data = data; }
    public CastBadgeResult Generate(Random rng, CastNameResult name);
}
```

### Per-tier branches (direct port of JS)

```csharp
public CastBadgeResult Generate(Random rng, CastNameResult name)
{
    var bf = _data.BadgeFlair ?? new BadgeFlairDto();
    var mf = bf.Mundane     ?? new BadgeMundaneDto();
    var hf = bf.HighStatus  ?? new BadgeHighStatusDto();
    var stamps = _data.DepartmentStamps ?? Array.Empty<DepartmentStampDto>();

    return name.Tier switch
    {
        CastNameTier.Common    => new CastBadgeResult(name.Title, Pick(mf.Conditions, rng), Pick(mf.OfficeSins, rng), Pick(mf.Access, rng), null, null, null, null, null),
        CastNameTier.Uncommon  => new CastBadgeResult(name.Title, Pick(mf.Conditions, rng), null, Pick(mf.Access, rng), Pick(mf.Stickers, rng), null, null, null, null),
        CastNameTier.Rare      => new CastBadgeResult(name.Title, Pick(mf.Conditions, rng), Pick(mf.OfficeSins, rng), null, Pick(mf.Stickers, rng), null, null, null, null),
        CastNameTier.Epic      => new CastBadgeResult(name.Title, null, null, null, Pick(mf.Stickers, rng), null, null, null,
                                                    FindDeptStamp(name.Title, stamps) ?? PickStamp(stamps, rng)),
        CastNameTier.Legendary or CastNameTier.Mythic => new CastBadgeResult(name.Title, null, null, null, null,
                                                    Pick(hf.Clearance, rng), Pick(hf.Legacy, rng), Pick(hf.Signature, rng),
                                                    FindDeptStamp(name.Title, stamps) ?? PickStamp(stamps, rng)),
        _ => throw new InvalidOperationException($"Unknown tier {name.Tier}")
    };
}

private static DepartmentStampDto? FindDeptStamp(string? title, DepartmentStampDto[] stamps)
{
    if (string.IsNullOrEmpty(title)) return null;
    var lower = title.ToLowerInvariant();
    foreach (var s in stamps)
        if (lower.Contains(s.Id, StringComparison.Ordinal)) return s;
    return null;
}

private static string? Pick(string[] arr, Random rng)
    => arr.Length == 0 ? null : arr[rng.Next(arr.Length)];

private static DepartmentStampDto? PickStamp(DepartmentStampDto[] arr, Random rng)
    => arr.Length == 0 ? null : arr[rng.Next(arr.Length)];
```

### Determinism

Same pattern as `CastNameGenerator`. Same seed → same badge. Reroll = fresh RNG.

### Optional combined API

Convenience method on `CastNameGenerator` for the common case "give me a name + matching badge":

```csharp
public (CastNameResult Name, CastBadgeResult Badge) GenerateWithBadge(Random rng, CastGender? gender = null);
```

Internally calls `Generate(rng, gender)` then `_badgeGen.Generate(rng, name)` — same RNG so the sequence is deterministic across name + badge.

### Performance

Per-call cost: handful of array picks. Sub-microsecond. No concern.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Cast/CastBadgeResult.cs` (new) | Output record. |
| code | `APIFramework/Cast/CastBadgeGenerator.cs` (new) | Per-tier branches + FindDeptStamp helper. |
| code | `APIFramework/Cast/CastNameGenerator.cs` (modification) | Add convenience `GenerateWithBadge` overload. |
| test | `APIFramework.Tests/Cast/CastBadgeGeneratorTests.cs` (new) | Per-tier badge structure tests + determinism. |
| test | `APIFramework.Tests/Cast/CastBadgeFindDeptStampTests.cs` (new) | Stamp-from-title heuristic. |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Update MAC-017 (note: badgeFlair + departmentStamps blocks now consumed by CastBadgeGenerator). |

---

## Acceptance tests

| ID | Assertion |
|:---|:---|
| AT-01 | Common-tier badge has Condition + Note + Access; no Sticker/Clearance/Legacy/Signature/DeptStamp. |
| AT-02 | Uncommon: Condition + Sticker + Access. |
| AT-03 | Rare: Condition + Note + Sticker. |
| AT-04 | Epic: Sticker + DeptStamp (heuristic-found if title contains keyword, else random stamp). |
| AT-05 | Legendary / Mythic: Clearance + Legacy + Signature + DeptStamp. |
| AT-06 | Same seed + same name = same badge (determinism). |
| AT-07 | DeptStamp heuristic finds "Synergy Dept." for a title containing "synergy". |
| AT-08 | All Phase 0–3 + Phase 4.0.A–L tests stay green. |
| AT-09 | `dotnet build` warning count = 0; all tests green. |

---

## Mod API surface

Updates **MAC-017** (cast name data catalog). The `badgeFlair` and `departmentStamps` blocks (preserved during M's port) now have a real consumer. Modders extending the catalog can add custom badge flair + stamps without code changes.

---

## Followups (not in scope)

- Hire-screen UI consuming badges (Unity-side; future packet).
- Inspector-tab badge view (right-click NPC → "Badge" tab).
- Per-archetype badge bias (the-vent always gets a "loud" sticker; the-old-hand always has the "Founding Member" legacy).

---

## Completion protocol

Standard. **Cost target $0.30.** Pure additive — no risk of regression.
