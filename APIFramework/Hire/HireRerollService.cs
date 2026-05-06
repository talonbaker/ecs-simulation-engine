using System;
using System.Collections.Generic;
using System.Linq;
using APIFramework.Cast;

namespace APIFramework.Hire;

/// <summary>
/// Top-level service for the hire reroll economy (WP-4.0.N — implementation of the
/// loot-box hire mechanic Talon designed in conversation with the cast name generator).
/// Wraps <see cref="CastNameGenerator"/> with token-spending + perk-driven tier biases
/// + per-session reroll history.
///
/// Future hire-screen UI consumes this: <c>service.Begin(...)</c> opens the session;
/// each "reroll" button click calls <c>session.TryReroll()</c>; "hire" calls
/// <c>session.Commit()</c> and the returned <see cref="CastNameResult"/> is fed to
/// <c>IWorldMutationApi.CreateNpc</c> for actual spawn.
///
/// This packet ships ONLY the substrate. The slot-machine reveal animation, the
/// per-perk visual bias indicators, the wallet-balance HUD — all UI work for a
/// future Unity-side packet.
/// </summary>
public sealed class HireRerollService
{
    private readonly CastNameGenerator _gen;
    private readonly HireRerollWallet  _wallet;
    private readonly HireRerollConfig  _config;

    public HireRerollService(CastNameGenerator gen, HireRerollWallet wallet, HireRerollConfig config)
    {
        _gen    = gen    ?? throw new ArgumentNullException(nameof(gen));
        _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Begin a hire session. Generates the initial candidate and returns a session
    /// the caller drives via TryReroll / Commit / Cancel.
    ///
    /// <paramref name="perks"/> compose in order: each perk's Apply() runs against the
    /// thresholds returned by the previous perk (or the catalog default for the first
    /// perk). When perks is null or empty, the catalog default thresholds are used.
    /// </summary>
    public HireSession Begin(
        CastGender?              gender = null,
        IReadOnlyList<HirePerk>? perks  = null,
        Random?                  rng    = null)
    {
        var thresholds = (perks?.Count ?? 0) == 0
            ? _gen.Data.TierThresholds
            : perks!.Aggregate(_gen.Data.TierThresholds, (t, p) => p.Apply(t));

        return new HireSession(_gen, _wallet, _config, thresholds, gender, rng ?? new Random());
    }
}
