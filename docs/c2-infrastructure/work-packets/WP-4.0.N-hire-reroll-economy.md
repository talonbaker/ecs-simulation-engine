# WP-4.0.N — Hire Reroll Economy Substrate

> **Orthogonal extension to WP-4.0.M.** Realises the "reroll for a better hire" loot-box mechanic Talon designed in conversation with the cast name generator. M's `Generate(Random)` overload is the seam; this packet adds the economy wrapper — token spending per reroll, optional perks that bias the tier curve, audit log of reroll history per hire decision. Pure engine substrate; no UI consumer yet (the hire-screen is a future Unity packet). Independent of Phase 4.2.x zone work — can land any time.

**Tier:** Sonnet
**Depends on:** WP-4.0.M (cast name generator + tier system).
**Parallel-safe with:** Everything (no consumers, no shared mutation state).
**Timebox:** 120 minutes
**Budget:** $0.55
**Feel-verified-by-playtest:** YES (the "tension between rerolling for a better hire and accepting the current one" is the loop — needs playtest evidence to calibrate token costs)
**Surfaces evaluated by next PT-NNN:** Does the reroll cost feel meaningful (not trivially infinite, not painfully expensive)? Are perk-biased rerolls satisfying when they hit? Does the player ever choose to STOP rerolling, or is the optimal play "always reroll until Mythic"?

---

## Goal

Add a token-spending wrapper around `CastNameGenerator.Generate` so the project's future hire mechanic can:

1. **Charge a token** per reroll (configurable cost; v0.1 default 1 token per reroll).
2. **Apply player-perk biases** to tier thresholds (e.g., a "Lucky Hire" perk reduces all tier thresholds by X%, making rare+ tiers more likely).
3. **Cap rerolls per hire decision** (configurable; default 5).
4. **Audit history** — record each reroll's outcome (which tier, which name) so the player can review their decision after committing.
5. **Commit / cancel API** — once the player commits to a candidate, the audit log is finalized; the NPC is spawned via `IWorldMutationApi.CreateNpc`.

This packet ships the engine substrate. The actual hire-screen UI (the deck-building, Slay-the-Spire-style reveal animation) is future Unity work that consumes the substrate.

---

## Reference files

- `~/talonbaker.github.io/name-face-gen/app.jsx` — has the JS-side reveal animation + slot-machine UI for reference. Don't port the UI; do read for tone.
- `APIFramework/Cast/CastNameGenerator.cs` (from M) — the seam.
- `APIFramework/Cast/CastNameTier.cs` (from M) — tier model.
- `APIFramework/Cast/CastNameDataLoader.cs#TierThresholdsDto` (from M) — JSON-tunable thresholds; perks bias these.

---

## Non-goals

- Do **not** ship the hire-screen UI. Engine substrate only.
- Do **not** ship the player's economy / wallet. Tokens are an opaque counter in this packet (a `HireRerollWallet` service); how players earn tokens is FF-008 territory.
- Do **not** ship perks themselves. v0.1 supports perk-application as a callable API; the actual perk catalog is a future content packet.
- Do **not** modify `CastNameGenerator`. Pure additive wrapper.

---

## Design notes

### Public API

```csharp
namespace APIFramework.Hire;

public sealed class HireRerollService
{
    private readonly CastNameGenerator _gen;
    private readonly HireRerollWallet  _wallet;
    private readonly HireRerollConfig  _config;

    public HireRerollService(CastNameGenerator gen, HireRerollWallet wallet, HireRerollConfig config)
    { _gen = gen; _wallet = wallet; _config = config; }

    /// <summary>
    /// Begin a hire decision. Returns a HireSession the caller drives.
    /// </summary>
    public HireSession Begin(CastGender? gender = null, IReadOnlyList<HirePerk>? perks = null);
}

public sealed class HireSession
{
    public IReadOnlyList<HireRerollEntry> History { get; }
    public CastNameResult                 CurrentCandidate { get; private set; }
    public int                            RerollsUsed { get; private set; }
    public bool                           IsCommitted { get; private set; }

    /// <summary>Try to reroll. Returns false if wallet empty or cap reached. Charges a token on success.</summary>
    public bool TryReroll();

    /// <summary>Commit to the current candidate. Finalises history. Returns the candidate.</summary>
    public CastNameResult Commit();

    /// <summary>Cancel the session. No commit; history discarded; tokens NOT refunded (the rerolls happened).</summary>
    public void Cancel();
}

public sealed record HireRerollEntry(
    int            Index,
    CastNameResult Result,
    long           Tick);
```

### `HireRerollWallet`

```csharp
public sealed class HireRerollWallet
{
    public int Balance { get; private set; }
    public void Deposit(int tokens);
    public bool TrySpend(int tokens);
    public event Action<int>? OnBalanceChanged;
}
```

Simple counter. Future economy packets will populate it (per-game-day income, milestone rewards, etc.).

### `HireRerollConfig`

```csharp
public sealed class HireRerollConfig
{
    public int RerollCost      { get; init; } = 1;     // tokens per reroll
    public int MaxRerolls      { get; init; } = 5;     // per session
    public bool CapEnabled     { get; init; } = true;
}
```

### Perks bias the tier thresholds

```csharp
public abstract class HirePerk
{
    /// <summary>Modify the tier thresholds for this hire session. Returns a new TierThresholdsDto.</summary>
    public abstract TierThresholdsDto Apply(TierThresholdsDto baseThresholds);
}

// Example perks (catalog ships in a future content packet):
public sealed class LuckyHirePerk : HirePerk
{
    private readonly double _shift;   // e.g., 0.10 = thresholds shift down 10%, so rare+ more likely
    public LuckyHirePerk(double shift) { _shift = shift; }
    public override TierThresholdsDto Apply(TierThresholdsDto t)
    {
        // Shift cumulative thresholds down — common shrinks, rarer expands.
        return new TierThresholdsDto
        {
            Common    = t.Common    - _shift,
            Uncommon  = t.Uncommon  - _shift,
            Rare      = t.Rare      - _shift,
            Epic      = t.Epic      - _shift,
            Legendary = t.Legendary - _shift,
            Mythic    = t.Mythic   // stays at 1.0
        };
    }
}
```

Multiple perks compose: `appliedThresholds = perks.Aggregate(base, (t, p) => p.Apply(t))`.

### Generate path

```csharp
public HireSession Begin(CastGender? gender, IReadOnlyList<HirePerk>? perks)
{
    var thresholds = (perks?.Count ?? 0) == 0
        ? _gen.Data.TierThresholds
        : perks.Aggregate(_gen.Data.TierThresholds, (t, p) => p.Apply(t));

    var rng     = new Random();
    var initial = _gen.GenerateWithThresholds(rng, thresholds, gender);   // see below
    return new HireSession(initial, _gen, _wallet, _config, thresholds, gender, rng);
}
```

This requires extending `CastNameGenerator` with a `GenerateWithThresholds(Random, TierThresholdsDto, CastGender?)` overload. Additive on top of M, doesn't change existing API. Exists primarily for the perk-bias path.

### Audit history

Every reroll appends a `HireRerollEntry` to the session's history (with the reroll index, the candidate result, and the tick). On `Commit`, history is exposed to the caller and can be persisted (chronicle / audit log) for "look back at the candidates you considered" UI.

### Performance

Negligible. Each reroll is one `Generate` call (sub-microsecond) plus a wallet decrement.

### What this enables for the future

The hire-screen UI loops: spawn a session, present the candidate, player clicks "reroll" (tokens decrement) or "hire" (commit + spawn NPC via `IWorldMutationApi.CreateNpc`). Slot-machine reveal, tier-color flash, the whole loot-box experience — all UI side, fed by this substrate.

---

## Deliverables

| Kind | Path | Description |
|:---|:---|:---|
| code | `APIFramework/Hire/HireRerollService.cs` (new) | Top-level service. |
| code | `APIFramework/Hire/HireSession.cs` (new) | Per-hire session state machine. |
| code | `APIFramework/Hire/HireRerollWallet.cs` (new) | Token bank. |
| code | `APIFramework/Hire/HireRerollConfig.cs` (new) | Tunable knobs. |
| code | `APIFramework/Hire/HireRerollEntry.cs` (new) | Audit record. |
| code | `APIFramework/Hire/HirePerk.cs` (new) | Abstract base + LuckyHirePerk example. |
| code | `APIFramework/Cast/CastNameGenerator.cs` (modification) | Add `GenerateWithThresholds` overload + public `Data` accessor. |
| code | `APIFramework/Core/SimulationBootstrapper.cs` (modification) | Optionally register HireRerollService when config enabled. |
| test | `APIFramework.Tests/Hire/HireRerollServiceTests.cs` (new) | Session lifecycle (begin / reroll / commit / cancel). |
| test | `APIFramework.Tests/Hire/HireRerollWalletTests.cs` (new) | Balance / deposit / spend. |
| test | `APIFramework.Tests/Hire/HirePerkTests.cs` (new) | LuckyHirePerk shifts thresholds correctly. |
| test | `APIFramework.Tests/Hire/HireSessionCapTests.cs` (new) | MaxRerolls cap enforced. |
| test | `APIFramework.Tests/Hire/HireRerollDistributionTests.cs` (new) | With LuckyHirePerk applied, empirical distribution shifts toward rare+ (large-N). |
| ledger | `docs/c2-infrastructure/MOD-API-CANDIDATES.md` | Add MAC-020 (hire reroll economy substrate). |

---

## Acceptance tests

| ID | Assertion |
|:---|:---|
| AT-01 | `Begin(...)` returns a session with an initial candidate; RerollsUsed == 0. |
| AT-02 | `TryReroll` succeeds when wallet has tokens AND under cap; charges 1 token; advances candidate. |
| AT-03 | `TryReroll` fails (returns false; no token charged) when wallet empty. |
| AT-04 | `TryReroll` fails when MaxRerolls reached. |
| AT-05 | `Commit` returns the current candidate and freezes the session. |
| AT-06 | `Cancel` discards session; tokens NOT refunded. |
| AT-07 | History captures every candidate considered, in order. |
| AT-08 | `LuckyHirePerk(0.10)` shifts the empirical distribution: rare+ rate up at least 5 percentage points over baseline (large-N test). |
| AT-09 | All Phase 0–3 + Phase 4.0.A–L tests stay green. |
| AT-10 | `dotnet build` warning count = 0; all tests green. |

---

## Mod API surface

Introduces **MAC-020: Hire reroll economy substrate**. Append to ledger:

> **MAC-020: Hire reroll economy + perk system**
> - **What:** Token-spending wrapper around `CastNameGenerator` (M) for the loot-box hire mechanic. `HirePerk` is the abstract extension point — modders define new perks (e.g., "Manager's Connection: rare+ guaranteed every 3rd reroll") by subclassing `HirePerk` and implementing `Apply(TierThresholdsDto)`. Catalog of available perks is data-driven (future content packet).
> - **Where:** `APIFramework/Hire/`.
> - **Why a candidate:** The hire mechanic is the player's primary cast-management surface; modders shipping new perks / archetypes / hire-related content all touch this seam.
> - **Stability:** fresh (lands with WP-4.0.N).
> - **Source packet:** WP-4.0.N.

---

## Followups (not in scope)

- Hire-screen UI (Unity-side, future).
- Perk catalog (content packet enumerating actual perks).
- Wallet income source (couples to FF-008 economy).
- "Re-hire" mechanic (bring back a fired NPC from a previous session at reduced cost).
- Decision-history chronicle entry ("you considered 4 candidates and chose Karen Snell").

---

## Completion protocol

Standard. **Cost target $0.55.** Pure additive — no risk of regression.
