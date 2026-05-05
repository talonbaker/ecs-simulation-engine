# APIFramework.Cast

Probabilistic name + title generator for the office sim's cast. Direct port of Talon's HTML/JS roster generator at `~/talonbaker.github.io/name-face-gen/data.js` (WP-4.0.M).

## What's in here

| Type | Purpose |
|:---|:---|
| `CastNameGenerator` | Public API. `Generate(rng / seed / <none>, gender?, forcedTier?)` returns a `CastNameResult`. Deterministic + seedable; the seam for the future loot-box hire mechanic. |
| `CastNameResult` | Record carrying `DisplayName`, `Tier`, `Gender`, `FirstName`, `Surname`, `Title`, `LegendaryRoot`, `LegendaryTitle`, `CorporateTitle`. |
| `CastNameTier` | Enum: `Common 55% / Uncommon 27% / Rare 12% / Epic 4% / Legendary 1.5% / Mythic 0.5%`. |
| `CastGender` | Enum: `Male / Female / Neutral`. |
| `CastNameData` | POCO mirror of the JSON catalog. |
| `CastNameDataLoader` | Loads `docs/c2-content/cast/name-data.json`. Lazy default cache; fail-closed validation. |
| `Internal/FusionBuilder` | Surname construction + the consonant-collapse cleanup pass (cleanFusion). |
| `Internal/TitleBuilder` | Per-tier title construction (modular rank/domain/function). |
| `Internal/TierRoller` | Cumulative threshold cascade for tier-roll → tier mapping. |

## Catalog

`docs/c2-content/cast/name-data.json` (~5 KB) — modder-extensible:
- `firstNames` (per gender)
- `fusionPrefixes` / `fusionSuffixes` (for fused surnames)
- `staticLastNames` (for vanilla / common tier)
- `legendaryRoots` (per gender) / `legendaryTitles` / `corporateTitles` (for higher tiers)
- `titleTiers` (rank / domain / function each split into mundane / silly / highStatus)
- `badgeFlair` / `departmentStamps` (preserved for the future badge-generation packet WP-4.0.M.1; not consumed by the v0.1 generator)

## Typical use

```csharp
var data = CastNameDataLoader.LoadDefault()!;
var gen  = new CastNameGenerator(data);

// Boot-time / non-deterministic:
var npc = gen.Generate(gender: CastGender.Female);

// Reproducible (tests, future loot-box reroll):
var rng = new Random(42);
var npc2 = gen.Generate(rng, gender: CastGender.Female);
```

## Reroll seam

`Generate(Random rng, ...)` is the deterministic-with-state seam. Future hire-screen mechanics (WP-4.0.N spec) wrap this with token-spending logic. To reroll, just call `Generate` again with a fresh `Random` — same seed = same name; different seed = different name.

## JS-vs-C# RNG note

`Math.random()` (V8) and `System.Random` (.NET) use different algorithms. Matched seeds will NOT produce identical names across the two implementations. **Behavioral equivalence** (same tier distributions, same logical structure per tier) is the bar — verified by `TierDistributionTests` (100k rolls within ±1.5% of declared rates).

## Consumers

- **`CastNamePool`** (`APIFramework.Bootstrap`) — auto-naming layer with collision retry against existing live NPCs. Used by WP-4.0.K author-mode NPC creation.
- **(future) `HireRerollService`** (WP-4.0.N spec) — token-spending wrapper for the loot-box hire mechanic.
- **(future) `CastBadgeGenerator`** (WP-4.0.M.1 spec) — visual badge flair per tier; reads `BadgeFlair` + `DepartmentStamps` blocks of the catalog.

## See also

- `docs/c2-infrastructure/MOD-API-CANDIDATES.md#MAC-017` — modder-extension surface.
- `APIFramework.Tests/Cast/` — 54 unit tests.
- `APIFramework.Tests/Cast/SampleRollFixtures/sample-1000-rolls.txt` — checked-in fixture of 1000 sample rolls (seed 42) for absurdity calibration audit.
