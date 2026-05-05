using System;
using System.Collections.Generic;
using APIFramework.Cast;

namespace APIFramework.Hire;

/// <summary>
/// One hire decision in flight. Created by <see cref="HireRerollService.Begin"/>;
/// advanced by <see cref="TryReroll"/>; finalised by <see cref="Commit"/> or
/// <see cref="Cancel"/>. After Commit/Cancel the session is frozen and further
/// reroll attempts throw.
/// </summary>
public sealed class HireSession
{
    private readonly CastNameGenerator   _gen;
    private readonly HireRerollWallet    _wallet;
    private readonly HireRerollConfig    _config;
    private readonly TierThresholdsDto   _thresholds;
    private readonly CastGender?         _gender;
    private readonly Random              _rng;
    private readonly List<HireRerollEntry> _history = new();
    private bool                          _isCommitted;
    private bool                          _isCancelled;

    /// <summary>Read-only view of every candidate considered so far, in order.</summary>
    public IReadOnlyList<HireRerollEntry> History => _history;

    /// <summary>The candidate currently on offer. Updated by each successful <see cref="TryReroll"/>.</summary>
    public CastNameResult CurrentCandidate { get; private set; }

    /// <summary>Number of reroll attempts that have CHARGED the wallet. The initial candidate is index 0 and does NOT count.</summary>
    public int RerollsUsed { get; private set; }

    /// <summary>True after <see cref="Commit"/>.</summary>
    public bool IsCommitted => _isCommitted;

    /// <summary>True after <see cref="Cancel"/>.</summary>
    public bool IsCancelled => _isCancelled;

    /// <summary>Constructor — internal; only <see cref="HireRerollService.Begin"/> calls this.</summary>
    internal HireSession(
        CastNameGenerator   gen,
        HireRerollWallet    wallet,
        HireRerollConfig    config,
        TierThresholdsDto   thresholds,
        CastGender?         gender,
        Random              rng)
    {
        _gen        = gen;
        _wallet     = wallet;
        _config     = config;
        _thresholds = thresholds;
        _gender     = gender;
        _rng        = rng;

        var initial = _gen.GenerateWithThresholds(_rng, _thresholds, _gender);
        CurrentCandidate = initial;
        _history.Add(new HireRerollEntry(Index: 0, Result: initial, Tick: 0));
    }

    /// <summary>
    /// Attempt a reroll. Returns true on success (token charged, candidate advanced,
    /// history appended); false on failure (insufficient tokens OR cap reached).
    /// Throws if the session is committed or cancelled.
    /// </summary>
    public bool TryReroll()
    {
        EnsureActive();
        if (_config.CapEnabled && RerollsUsed >= _config.MaxRerolls) return false;
        if (!_wallet.TrySpend(_config.RerollCost)) return false;

        RerollsUsed++;
        var next = _gen.GenerateWithThresholds(_rng, _thresholds, _gender);
        CurrentCandidate = next;
        _history.Add(new HireRerollEntry(Index: RerollsUsed, Result: next, Tick: 0));
        return true;
    }

    /// <summary>Commit to the current candidate; freezes the session. Returns the candidate.</summary>
    public CastNameResult Commit()
    {
        EnsureActive();
        _isCommitted = true;
        return CurrentCandidate;
    }

    /// <summary>
    /// Cancel the session — discards the decision but does NOT refund spent tokens
    /// (the rerolls happened; the player paid for the experience).
    /// </summary>
    public void Cancel()
    {
        EnsureActive();
        _isCancelled = true;
    }

    private void EnsureActive()
    {
        if (_isCommitted) throw new InvalidOperationException("HireSession already committed.");
        if (_isCancelled) throw new InvalidOperationException("HireSession already cancelled.");
    }
}
