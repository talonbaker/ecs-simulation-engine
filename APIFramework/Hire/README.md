# APIFramework.Hire

Hire reroll economy substrate for the future loot-box-style hire mechanic (WP-4.0.N). Wraps `CastNameGenerator` (`APIFramework.Cast`) with token-spending, perk-driven tier biases, and per-session reroll history.

## What's in here

| Type | Purpose |
|:---|:---|
| `HireRerollService` | Top-level service. `Begin(gender?, perks?, rng?)` returns a `HireSession` the caller drives. |
| `HireSession` | Per-hire state machine. Holds the current candidate, reroll count, history. Methods: `TryReroll()`, `Commit()`, `Cancel()`. Frozen after Commit/Cancel. |
| `HireRerollWallet` | Token bank. `Balance`, `Deposit`, `TrySpend`, `OnBalanceChanged` event. |
| `HireRerollConfig` | Tunable knobs: `RerollCost` (default 1), `MaxRerolls` (default 5), `CapEnabled`. |
| `HireRerollEntry` | One row of the per-session history (Index, Result, Tick). |
| `HirePerk` | Abstract extension point: `Apply(TierThresholdsDto) → TierThresholdsDto`. Modders subclass to implement perks. |
| `LuckyHirePerk(shift)` | Reference implementation. Shifts every cumulative threshold down to expand rare+ buckets. |

## Typical use (future hire-screen UI pattern)

```csharp
// Setup (once at boot):
var data    = CastNameDataLoader.LoadDefault();
var nameGen = new CastNameGenerator(data);
var wallet  = new HireRerollWallet(startingBalance: 5);   // 5 reroll tokens
var config  = new HireRerollConfig();                      // defaults
var service = new HireRerollService(nameGen, wallet, config);

// Open hire screen:
var perks   = playerPerks;   // List<HirePerk> from player's accumulated perk set
var session = service.Begin(CastGender.Female, perks);
ShowCandidate(session.CurrentCandidate);    // UI displays initial roll

// Player clicks "reroll":
if (session.TryReroll())
{
    ShowCandidate(session.CurrentCandidate);   // wallet charged; new candidate displayed
    UpdateWalletDisplay(wallet.Balance);
}
else
{
    Toast("Out of tokens or reroll cap reached!");
}

// Player clicks "hire":
var chosen = session.Commit();
var npcId  = mutationApi.CreateNpc(roomId, x, y, archetypeId, chosen.DisplayName);

// Or "cancel":
session.Cancel();   // tokens NOT refunded — the rerolls happened
```

## Companion: badge generation

For each committed candidate, the future hire screen also wants the badge metadata (sticker, clearance, signature, department stamp, etc.) to render on the hire card. Use `CastBadgeGenerator` (`APIFramework.Cast`) with the same RNG:

```csharp
var badgeGen = new CastBadgeGenerator(data);
var badge    = badgeGen.Generate(rng, chosen);   // tier-appropriate badge composition
```

Or use `CastNameGenerator.GenerateWithBadge(...)` for the simple non-reroll case.

## Determinism

`HireRerollService.Begin(rng: ...)` accepts an explicit `Random` instance. For reproducible hire decisions (tests, save/replay), pass a seeded RNG. Same seed + same perks + same actions = same final candidate. The reroll experience itself is also reproducible — the history list captures every candidate considered.

## Perks compose

```csharp
var perks = new List<HirePerk> {
    new LuckyHirePerk(0.05),    // first perk: shift thresholds down 5%
    new LuckyHirePerk(0.05),    // second perk: ANOTHER 5% shift on top
};
// Net effect: thresholds shifted 10% down. Aggregation is by Apply() chaining.
```

Multiple perks are applied in order via aggregate. Custom perks can do anything that returns a `TierThresholdsDto` — e.g., a "Manager's Connection" perk could pin the Mythic threshold to 0.95 (5% Mythic chance instead of 0.5%).

## What this packet does NOT ship

- Hire-screen UI (Unity-side; future packet)
- Wallet income source (couples to FF-008 economy)
- Perk catalog (content packet enumerating actual perks)
- Decision-history chronicle entry ("Talon considered 4 candidates and chose Karen Snell")

## See also

- `docs/c2-infrastructure/MOD-API-CANDIDATES.md#MAC-020` — modder-extension surface.
- `APIFramework.Cast/README.md` — the underlying name generator + badge generator + tier model.
- `APIFramework.Tests/Hire/` — 23 tests covering the lifecycle + perk distribution shift.
