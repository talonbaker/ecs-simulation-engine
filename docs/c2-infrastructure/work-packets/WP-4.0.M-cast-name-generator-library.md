# WP-4.0.M — Cast Name Generator Library (port from JS spec)

> **Wave 4 of the Phase 4.0.x foundational polish wave — authoring loop, naming substrate.** Talon authored a full probabilistic name + title generator in HTML/JS at `C:\repos\webpages\talonbaker.github.io\name-face-gen\` (six tiers — common 55% / uncommon 27% / rare 12% / epic 4% / legendary 1.5% / mythic 0.5%; modular title builder; fusion grammar with consonant-collapse rules). This packet ports it verbatim to a C# library under `APIFramework/Cast/`, exposes a deterministic seedable API, and rewires WP-4.0.K's NPC auto-naming through it. Sets the substrate for the future "reroll for a better hire" loot-box mechanic. Pure engine library; no Unity, no rendering, no audio. Pairs with the WP-4.0.K NPC-authoring tool but is independently useful (boot-time cast generation, future hire mechanic, dev-console `roll-name` command).

**Tier:** Sonnet
**Depends on:** Nothing in the wave-4 chain (parallel-safe with WP-4.0.I and WP-4.0.J). **Should merge before WP-4.0.K** so K's NPC auto-naming consumes this library directly. (If K dispatches first, K uses a stub `CastNamePool` and a follow-up small INT swaps it for the M library — but in-order is cleaner.)
**Parallel-safe with:** WP-4.0.I (engine, disjoint), WP-4.0.J (Unity, disjoint), WP-4.0.L (docs, disjoint), wave-3 leftovers.
**Timebox:** 120 minutes
**Budget:** $0.50
**Feel-verified-by-playtest:** YES (sample rolls of 200 names per tier reviewed by Talon; the absurdity calibration is the gameplay surface)
**Surfaces evaluated by next PT-NNN:** does the tier distribution feel right after a few hundred rolls? Do the per-tier results land in the absurd-but-not-cringe sweet spot? Is the legendary / mythic tier actually exciting when it hits, or does it feel arbitrary?

---

## Goal

Talon's HTML/JS roster generator is the **canonical name + title source** for the office sim's cast. The system embodies the project's tone — vulgar-but-not-mean, absurd-but-grounded, deflating corporate self-seriousness through escalating titles — and the rarity-tier model gives every NPC hire a small drama (commonly: "another Bert Snell"; occasionally: "Executive VP Kratos, The Doom-Slayer"). The future "reroll for a better hire" mechanic is a natural extension of the same probability surface.

This packet **does not redesign**. It ports. The JS source files at `C:\repos\webpages\talonbaker.github.io\name-face-gen\data.js` are the source of truth. Port targets:

1. **Data**: `data.js#NAME_DATA` → `docs/c2-content/cast/name-data.json` (data-driven extension surface; consistent with MAC-001 / MAC-005 / MAC-013 / MAC-014 / MAC-016 pattern).
2. **Logic**: `generateName()`, `generateTitle()`, `cleanFusion()`, `buildSurname()`, `buildShortSurnameHalf()`, `maybeAddSuffix()` → `APIFramework/Cast/CastNameGenerator.cs` (and helpers).
3. **Tier model**: the six tiers with their drop rates → `CastNameTier` enum + thresholds in code, JSON-overridable.
4. **Public API**: deterministic + seedable; reroll-friendly.

Out of scope for this packet (deferred): badge generation (stickers, clearance, legacy, signature, department stamp — these are visual badge-card flair, not engine-relevant; ship when there's a UI surface that wants them); face generation (`face-config.js` / `face.js` — explicitly excluded per Talon).

After this packet:
- `APIFramework/Cast/CastNameGenerator.cs` — deterministic seedable name + title generator.
- `APIFramework/Cast/CastNameTier.cs` — `Common | Uncommon | Rare | Epic | Legendary | Mythic` enum.
- `APIFramework/Cast/CastNameResult.cs` — record carrying display name, tier, gender, all per-tier sub-fields (legendary root, corp title, surname, etc.) the generator produces.
- `docs/c2-content/cast/name-data.json` — the ported `NAME_DATA` block; schema-validated; modder-extensible.
- `Warden.DevConsole/Commands/RollNameCommand.cs` (WARDEN) — `> roll-name [--gender m|f|n] [--tier <name>] [--count N]` for sampling output.
- `WP-4.0.K`'s NPC auto-naming consumes `CastNameGenerator` directly. The original `cast-names-options.json` is deprecated (kept on disk as a reference doc; no code path reads it after this packet ships).
- `playtest-office.json` and `office-starter.json` continue to use their hardcoded `nameHint` values; the generator is invoked when no hint is provided (boot-time spawn) or when an authored NPC needs an auto-name (runtime via WP-4.0.K).
- The deterministic seed path makes the generator unit-testable: same seed → same sequence → same name.
- Reroll affordance is one method call (`Generate(seed)` with a fresh seed), so the future loot-box mechanic adds spend/cost wrappers without touching the generator.

The 30-NPCs-at-60-FPS gate is irrelevant (generator is one-shot per spawn, not per-frame). Per-name cost target: < 0.1ms (well within any spawn budget).

---

## Reference files

- `C:\repos\webpages\talonbaker.github.io\name-face-gen\data.js` — **source of truth**. Read in full. Port `NAME_DATA` to JSON, port `generateName` + `generateTitle` + helpers to C#. The JS is ~782 lines; the C# port is comparable. Behavior must match for matched seeds (within RNG-equivalence across JS `Math.random` vs .NET `Random` — see Design notes).
- `C:\repos\webpages\talonbaker.github.io\name-face-gen\app.jsx` — the React app. Read selected sections to confirm public-API shape (how the JS app calls `generateName`, what fields it consumes from the result). Tier metadata (label, color, glow) is UI flair — ignore for the C# port; tier IS the engine-relevant artifact.
- `C:\repos\webpages\talonbaker.github.io\name-face-gen\index.html` — confirms script load order; informational only.
- `docs/c2-content/cast-names-options.json` — the early-stage prose-spec predecessor. After this packet, this file is informational/historical; mark with a deprecation header noting "superseded by docs/c2-content/cast/name-data.json (WP-4.0.M)" — do not delete (audit-trail discipline).
- `docs/c2-content/cast-bible.md` — read for archetype-correct naming intuition. Cast bible is canonical for "who lives in this office"; the generator is the engine for "what do new hires arrive named."
- `docs/c2-content/aesthetic-bible.md` — tone alignment.
- `docs/c2-content/world-bible.md` — ditto.
- `docs/c2-infrastructure/MOD-API-CANDIDATES.md` — adds MAC-017 (cast-name-data catalog as Mod API surface).
- `APIFramework/Bootstrap/CastGenerator.cs` — read for boot-time NPC spawn (what consumes name strings today). New library exposes a method `CastGenerator` and `CastNamePool` (from K) call.
- `APIFramework.Tests/Cast/` — new directory; tests land here.
- `docs/c2-infrastructure/work-packets/WP-4.0.K-npc-authoring.md` — already drafted in this wave; this packet's interfaces are what K's `CastNamePool` calls instead of inventing its own pool.

---

## Non-goals

- Do **not** port badge generation. `generateBadge()` and the `badge_flair` / `department_stamps` data blocks stay JS-only for now; revisit when there's an in-game badge UI to consume them. (The JSON data file may include the badge sub-objects as additive content for round-trip preservation, but C# doesn't expose a `GenerateBadge` method in this packet.)
- Do **not** port face generation. `face-config.js` / `face.js` are explicitly excluded.
- Do **not** port the audio reveal (`playRevealChord`) — that's UI flair.
- Do **not** ship the loot-box reroll economy (token costs, rarity-weight modifiers, "favored archetype" boosts). Ship the substrate so reroll is trivially a `Generate(newSeed)` call; the economy is a future packet.
- Do **not** modify the existing world-definition `npcSlots` shape. NPCs spawned from a slot continue to honor `nameHint` if present; the generator is invoked when no hint is supplied.
- Do **not** introduce a new RNG dependency. Use `System.Random` with a seed; document the JS-vs-C# RNG-equivalence caveat (see Design notes).
- Do **not** make the generator depend on any other engine system. It's a standalone library: data in (JSON catalog), result out (typed record). No `World`, no `EntityId`, no `IWorldMutationApi` — those are caller concerns.
- Do **not** add Unity-side rendering for tier flair (color, glow, particle reveal). The C# library returns a `Tier` enum; the future inspector/UI packet consumes that enum and renders accordingly. This packet is engine-only.
- Do **not** localize. English-only; the absurdist tone doesn't translate cleanly anyway.
- Do **not** ship per-archetype name affinity beyond what the JS source supports (currently none — names pull from gender pools without archetype filtering). If Talon wants archetype-affinity later, that's a content edit to the JSON catalog (add `archetypeAffinityHint` field to first-name entries) plus a future small packet to consume it.
- Do **not** alter the tier drop rates (55% / 27% / 12% / 4% / 1.5% / 0.5%). Those are calibrated by Talon. They are JSON-overridable for tuning packets later, but ship-default matches the JS source.

---

## Design notes

### Library layout

```
APIFramework/Cast/
├── CastNameGenerator.cs         // public API + Generate methods
├── CastNameTier.cs               // enum
├── CastNameResult.cs             // record
├── CastNameDataLoader.cs         // JSON catalog loader
├── CastNameData.cs               // POCO mirror of name-data.json
├── Internal/
│   ├── FusionBuilder.cs          // buildSurname + buildShortSurnameHalf + cleanFusion
│   ├── TitleBuilder.cs           // generateTitle equivalent
│   └── TierRoller.cs             // tier threshold resolution
```

### Public API

```csharp
namespace APIFramework.Cast;

public sealed class CastNameGenerator
{
    private readonly CastNameData _data;

    public CastNameGenerator(CastNameData data) { _data = data; }

    /// <summary>
    /// Generates a name. Random seed; non-deterministic (use seeded overload for tests).
    /// </summary>
    public CastNameResult Generate(CastGender? gender = null, CastNameTier? forcedTier = null)
        => Generate(Random.Shared, gender, forcedTier);

    /// <summary>
    /// Generates a name from the given seed (deterministic).
    /// Reroll is just calling this with a fresh seed.
    /// </summary>
    public CastNameResult Generate(int seed, CastGender? gender = null, CastNameTier? forcedTier = null)
        => Generate(new Random(seed), gender, forcedTier);

    /// <summary>
    /// Generates a name using the supplied Random instance — for tests + future loot-box wrappers
    /// that want to control RNG state (e.g., "use the player's session-RNG so rerolls share state").
    /// </summary>
    public CastNameResult Generate(Random rng, CastGender? gender = null, CastNameTier? forcedTier = null);
}

public enum CastGender { Male, Female, Neutral }
public enum CastNameTier { Common, Uncommon, Rare, Epic, Legendary, Mythic }

public sealed record CastNameResult(
    string         DisplayName,            // the headline string; "Bert Snell" / "Executive VP Kratos, The Doom-Slayer"
    CastNameTier   Tier,
    CastGender     Gender,
    string         FirstName,
    string?        Surname,                // null for ultra (mythic uses corp+root+title)
    string?        Title,                  // null below uncommon's chance threshold
    string?        LegendaryRoot,          // populated for legendary + mythic
    string?        LegendaryTitle,         // populated for divine-style legendary + mythic
    string?        CorporateTitle          // populated for epic / hybrid-legendary / mythic
);
```

### Loot-box hooks (forward-looking, no code in this packet)

The `Generate(Random rng, ...)` overload is the seam the future reroll mechanic uses. A reroll-economy wrapper looks roughly like:

```csharp
// FUTURE PACKET — sketch only, do not implement here
public sealed class HireRerollService {
    private readonly CastNameGenerator _gen;
    private readonly EconomyService _econ;
    public CastNameResult Reroll(EntityId hirer, Random rng, int costTokens) {
        _econ.Spend(hirer, costTokens);
        return _gen.Generate(rng);   // possibly with bias overrides if perks tilt the tier curve
    }
}
```

The seam is enough; nothing more in this packet.

### Tier roller

```csharp
internal static class TierRoller {
    // Cumulative thresholds matching JS source.
    // common 0.55, uncommon 0.82, rare 0.94, epic 0.98, legendary 0.995, mythic 1.0
    public static CastNameTier Roll(double r) =>
        r < 0.55  ? CastNameTier.Common :
        r < 0.82  ? CastNameTier.Uncommon :
        r < 0.94  ? CastNameTier.Rare :
        r < 0.98  ? CastNameTier.Epic :
        r < 0.995 ? CastNameTier.Legendary :
                    CastNameTier.Mythic;
}
```

JSON-overridable for tuning packets: thresholds live in `name-data.json#tierThresholds` with the above defaults.

### `cleanFusion`

Direct port of the JS regex collapsing (3+ same consonants → 2; specific awkward-cluster fixes). Use `Regex` with same patterns; case-insensitive matching.

### `generateTitle`

Direct port of the JS `generateTitle(tier)` switch on tier. Per-tier branches:
- Common → null.
- Uncommon → 15% chance of a single rank token.
- Rare → 40% chance; 50/50 between rank-only and rank+function.
- Epic → 80% chance; 40/60 between rank+domain+function and rank+function.
- Legendary → always rank+domain+function (high-status tier).
- Mythic → rank+domain+function with `of <domain2>` flourish.

All token pools come from `name-data.json#titleTiers` (rank/domain/function each split into mundane/silly/high_status).

### JSON catalog: `docs/c2-content/cast/name-data.json`

Direct port of `NAME_DATA`:

```jsonc
{
  "schemaVersion": "0.1.0",
  "tierThresholds": {
    "common": 0.55, "uncommon": 0.82, "rare": 0.94,
    "epic": 0.98, "legendary": 0.995, "mythic": 1.0
  },
  "firstNames": {
    "male":    [ /* port from data.js */ ],
    "female":  [ /* port from data.js */ ],
    "neutral": [ /* port from data.js */ ]
  },
  "fusionPrefixes":  [ /* port */ ],
  "fusionSuffixes":  [ /* port */ ],
  "staticLastNames": [ /* port */ ],
  "legendaryRoots": {
    "male": [ /* port */ ], "female": [ /* port */ ], "neutral": [ /* port */ ]
  },
  "legendaryTitles":  [ /* port */ ],
  "corporateTitles":  [ /* port */ ],
  "titleTiers": {
    "rank":     { "mundane": [...], "silly": [...], "highStatus": [...] },
    "domain":   { "mundane": [...], "silly": [...], "highStatus": [...] },
    "function": { "mundane": [...], "silly": [...], "highStatus": [...] }
  },
  "badgeFlair": { /* additive — preserved for future badge-generation packet */ },
  "departmentStamps": [ /* additive — preserved */ ]
}
```

`badgeFlair` and `departmentStamps` are deserialized into `CastNameData` but the C# library does not consume them (yet). Round-trip preservation only.

### JS-vs-C# RNG equivalence (and why we don't try to match exactly)

`Math.random()` (V8) and `System.Random` (.NET) use different algorithms. There is no clean way to make a given seed produce identical sequences across the two languages. **We don't try.** Equivalence is *behavioral* (same tier distributions over large N; same logical structure per tier) not *bit-exact* (same name for seed=42).

This means: the JS sandbox at `name-face-gen/` is the spec for *behavior*; the C# port matches behavior, not raw output. Document this in the library's XML doc.

### Catalog loader

`CastNameDataLoader.LoadFromFile(path)` reads + validates `name-data.json`. Fail-closed on missing required fields per SRD §4.1; warn-and-default on missing optional sub-blocks (e.g., `badgeFlair` may be absent in modder-stripped catalogs). Loader is registered as a service in `SimulationBootstrapper`; loaded once at boot, used everywhere.

### Dev-console command

`> roll-name [--gender m|f|n] [--tier <name>] [--count N]` — WARDEN-only. Prints N rolls (default 1) with tier + display name. Useful for absurdity-calibration QA after content edits.

```
> roll-name --count 10
[common]    Bert Snell
[common]    Wally Honkey
[uncommon]  Penny Beefbottom
[common]    Bud Couch
[rare]      Cherry Wackerwick
[common]    Phyllis Dumbus
[epic]      Director of First Impressions Lance Picklebreath-Crud
[common]    Frank Buttson
[uncommon]  Otto Sludgesack
[common]    Doris Ploob
```

### Integration with WP-4.0.K

WP-4.0.K's `CastNamePool` is replaced. The K spec line "Add `APIFramework/Bootstrap/CastNamePool.cs` (new) — Live-name tracking + auto-pick" becomes:

> Add `APIFramework/Bootstrap/CastNamePool.cs` (new) — calls `CastNameGenerator.Generate(...)` for default names; tracks already-used names within the current world to avoid duplicates (re-rolls up to 5 times if the generated name collides with an existing live NPC; falls back to `<archetype>-<n>` if all rerolls collide — the rare event).

This packet adds an `[Optional NPC slot field] generatedTier` to the world-definition `npcSlots` — when a runtime-spawned NPC was generated (rather than handcoded), the slot records its tier so reload preserves the archetype-affinity *and* the legendary status. (Boot-loaded slots from `playtest-office.json` etc. omit `generatedTier` and the NPC has no tier — they are "above the system." Tier is only meaningful for randomly-generated names.)

Sonnet edits the K packet's relevant sections to reflect this dependency, OR if K dispatches first, K-INT swaps the stub for the M call. Either path works; in-order dispatch (M before K) is cleaner and saves the swap.

### Sample-roll fixtures for tone QA

Test fixture `APIFramework.Tests/Cast/SampleRollFixtures/sample-1000-rolls.txt` is generated as part of the test suite — 1000 rolls with seed 42, output dumped, checked into the repo. Talon reviews periodically for absurdity calibration; if the fixture changes (because catalog content changes), the diff is the audit trail. NOT auto-validated against an "expected" file; it's a reference output for human eyeballs.

### Performance

Per-call cost: a handful of array picks + string concatenations + one regex pass. Well under 0.1ms. No budget concern.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Cast/CastNameGenerator.cs` (new) | Public API. |
| code | `APIFramework/Cast/CastNameTier.cs` (new) | Enum. |
| code | `APIFramework/Cast/CastGender.cs` (new) | Enum. |
| code | `APIFramework/Cast/CastNameResult.cs` (new) | Record. |
| code | `APIFramework/Cast/CastNameData.cs` (new) | POCO mirror. |
| code | `APIFramework/Cast/CastNameDataLoader.cs` (new) | JSON loader + validator. |
| code | `APIFramework/Cast/Internal/FusionBuilder.cs` (new) | Surname construction + cleanFusion. |
| code | `APIFramework/Cast/Internal/TitleBuilder.cs` (new) | Title construction per tier. |
| code | `APIFramework/Cast/Internal/TierRoller.cs` (new) | Tier threshold resolution. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modification) | Register `CastNameGenerator` as a service. |
| data | `docs/c2-content/cast/name-data.json` (new) | Ported NAME_DATA + tierThresholds + badgeFlair (additive, unused yet) + departmentStamps (additive, unused yet). |
| data | `docs/c2-content/cast-names-options.json` (modification) | Add deprecation header pointing to `docs/c2-content/cast/name-data.json` (WP-4.0.M); body unchanged for audit history. |
| code | `Warden.DevConsole/Commands/RollNameCommand.cs` (new, WARDEN-gated) | `roll-name` command. |
| test | `APIFramework.Tests/Cast/CastNameGeneratorTests.cs` | Per-tier output structure + determinism with seed. |
| test | `APIFramework.Tests/Cast/TierDistributionTests.cs` | Run 100k rolls; verify empirical distribution within 1.5% of declared thresholds for each tier (large-N). |
| test | `APIFramework.Tests/Cast/FusionBuilderTests.cs` | cleanFusion regex behavior (triple-consonant collapse, awkward-cluster fixes). |
| test | `APIFramework.Tests/Cast/TitleBuilderTests.cs` | Per-tier title chance + structure. |
| test | `APIFramework.Tests/Cast/CastNameDataLoaderTests.cs` | Catalog load + fail-closed on missing required + warn-default on missing optional. |
| test | `APIFramework.Tests/Cast/CastNameDataJsonTests.cs` | JSON schema validation; round-trip parse. |
| test | `APIFramework.Tests/Cast/RerollSeamTests.cs` | Calling Generate with the same seed yields the same result; with different seeds yields different results. |
| fixture | `APIFramework.Tests/Cast/SampleRollFixtures/sample-1000-rolls.txt` | 1000 rolls with seed 42 as a checked-in reference output. |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Add MAC-017 (cast-name-data catalog as Mod API surface). |

---

## Acceptance tests

| ID | Assertion | Verification |
|:---|:---|:---|
| AT-01 | `CastNameGenerator.Generate(42)` returns the same `CastNameResult` on repeated calls. | unit-test |
| AT-02 | `CastNameGenerator.Generate(42)` and `CastNameGenerator.Generate(43)` return different results (with overwhelming probability — verify Display strings differ). | unit-test |
| AT-03 | All six tiers reachable: forced-tier path returns a structurally-correct result for each tier. | unit-test |
| AT-04 | Common-tier result: `DisplayName = "{First} {StaticLastName}"`; `Title = null`; `Surname` populated. | unit-test |
| AT-05 | Uncommon-tier result: `DisplayName = "{First} {FusedSurname}"`. | unit-test |
| AT-06 | Rare-tier result: 15% of cases hyphenated; remaining single-fused; `Title` populated 40% of cases. | unit-test (run N=2000) |
| AT-07 | Epic-tier result: always carries `CorporateTitle`; surname is short; 30% hyphenated. | unit-test (run N=2000) |
| AT-08 | Legendary-tier result: 50/50 split between divine-style (root + title) and hybrid (corp + root + short surname). | unit-test (run N=2000) |
| AT-09 | Mythic-tier result: corp + root + ", " + epic title format. | unit-test (run N=500) |
| AT-10 | Tier distribution over 100k rolls within ±1.5% of declared thresholds. | distribution-test |
| AT-11 | `cleanFusion` collapses triple consonants and known awkward clusters per JS regex. | unit-test |
| AT-12 | Catalog loader fails closed on missing required block (e.g., `firstNames`). | unit-test |
| AT-13 | Catalog loader warns + defaults on missing optional block (e.g., `badgeFlair`). | unit-test |
| AT-14 | `roll-name` dev-console command produces N rolls and prints tier + display. | integration-test |
| AT-15 | `cast-names-options.json` has a deprecation header pointing at the new catalog; body preserved for audit. | manual |
| AT-16 | All Phase 0–3 + Phase 4.0.A–H tests stay green. | regression |
| AT-17 | `dotnet build` warning count = 0; all tests green. | build + test |
| AT-18 | MAC-017 added to `MOD-API-CANDIDATES.md`. | review |
| AT-19 | Sample-roll fixture (`sample-1000-rolls.txt`) is generated by the test suite and checked in. | manual |

---

## Mod API surface

This packet introduces **MAC-017: Cast name data catalog (probabilistic name + title generator)**. Append to `MOD-API-CANDIDATES.md`:

> **MAC-017: Cast name data catalog**
> - **What:** JSON catalog (`docs/c2-content/cast/name-data.json`) feeding the `CastNameGenerator` library. Six-tier probabilistic generator (common → mythic) producing first name + surname + optional title; data-driven, modder-extensible. A modder authoring a "fantasy office" mod replaces the catalog with their own pools (elves / dwarves / wizards) and the engine consumes them transparently. Tier thresholds are JSON-tunable — modders can tilt rarity curves without code changes.
> - **Where:** `docs/c2-content/cast/name-data.json`; `APIFramework/Cast/CastNameDataLoader.cs`; `APIFramework/Cast/CastNameGenerator.cs`.
> - **Why a candidate:** Names + titles are universal across mods. The tier system is the substrate the future loot-box / hire-reroll mechanic builds on — that mechanic itself becomes a modder surface (perks that bias the curve, archetype-favored rolls, etc.). Pattern is consistent with MAC-001 / MAC-005 / MAC-013 / MAC-014 / MAC-016 — data-driven extension.
> - **Stability:** fresh (lands with WP-4.0.M; second consumer = WP-4.0.K NPC palette).
> - **Source packet:** WP-4.0.M.
> - **Lineage:** ported from Talon's HTML/JS roster generator at `C:\repos\webpages\talonbaker.github.io\name-face-gen\` (data.js as source-of-truth).

---

## Followups (not in scope)

- **WP-4.0.M-INT (likely no-op or merged into K-INT)** — confirm K's `CastNamePool` consumes `CastNameGenerator`. If M dispatches before K (recommended order), K's spec already references M and no separate INT is needed.
- **Future packet — reroll / hire economy.** Spend tokens to reroll. Perks that bias the tier curve. Archetype-favored rolls. The seam is the `Generate(Random rng, ...)` overload; the wrapper holds the economy.
- **Future packet — badge generation.** Port `generateBadge()` to `CastBadgeGenerator`. Lands when there's an in-game badge UI (employee inspector tab? hire interview screen? bulletin board?) that consumes badges. The data is already in `name-data.json#badgeFlair` — preserved by this packet.
- **Future packet — face generation.** Port `face-config.js` + `face.js` to a Unity-side procedural-face generator. Couples to the silhouette / chibi rendering pipeline (MAC-013).
- **Future packet — archetype name affinity.** Add `archetypeAffinityHint` to first-name entries; the cast palette (WP-4.0.K) and runtime hire mechanic prefer archetype-appropriate names. Pure content + small loader extension.
- **Future packet — name uniqueness budget.** Currently `CastNamePool` rerolls up to 5x to avoid collision. With 30 NPCs over a long campaign, the pool eventually saturates — design a cleanup / generation-shift strategy (e.g., introduce new fusion suffixes mid-campaign).
- **Future packet — tier reveal UI.** Port the JS `playRevealChord` + `ParticleBurst` + `SlotName` reveal animations to Unity for the hire-screen drama. Couples to particle vocabulary (MAC-012) + sound triggers (MAC-003).

---

## Completion protocol (REQUIRED — read before merging)

### Visual verification: NOT required

Engine library packet. `dotnet test` green + Talon's review of `sample-1000-rolls.txt` for absurdity calibration is the gate.

The Sonnet executor's pipeline:

0. **Worktree pre-flight.** Confirm worktree at `.claude/worktrees/sonnet-wp-4.0.m/` on branch `sonnet-wp-4.0.m` based on recent `origin/staging`.
1. Read `C:\repos\webpages\talonbaker.github.io\name-face-gen\data.js` in full. Treat as source-of-truth spec.
2. Implement the spec.
3. Run `dotnet test`. All must stay green.
4. Generate `sample-1000-rolls.txt` (test suite produces it; ensure checked in).
5. Stage all changes including self-cleanup.
6. Commit on the worktree's feature branch.
7. Push the branch.
8. Stop. Notify Talon: `READY FOR REVIEW — engine library; please skim sample-1000-rolls.txt for absurdity calibration. Tier distribution test confirms empirical drop rates within ±1.5%.`.

### Feel-verified-by-playtest acceptance flag

**Feel-verified-by-playtest:** YES
**Surfaces evaluated by next PT-NNN:** does the tier distribution feel right after a few hundred rolls in actual play (new-hire flow once it ships)? Do epic / legendary / mythic tiers land in the absurd-but-not-cringe sweet spot? Is the corp-title escalation funny or grating?

### Cost envelope

Target: **$0.50**. Library + JSON port + 7 test files + dev-console command. If cost approaches $0.85, escalate via `WP-4.0.M-blocker.md`.

Cost-discipline:
- The JS source is the spec. Don't redesign — port verbatim; calibration is Talon's, not Sonnet's.
- Skip badge and face generation entirely (deferred per non-goals).
- Tests cover behavior + distribution; do not need bit-exact JS-equivalence.

### Self-cleanup on merge

Standard. Check for `WP-4.0.K` (consumer) and `WP-4.0.L` (docs reference).
