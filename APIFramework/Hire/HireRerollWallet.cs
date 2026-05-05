using System;

namespace APIFramework.Hire;

/// <summary>
/// Token bank for the hire reroll economy (WP-4.0.N). Future economy packets
/// (FF-008 territory) populate the balance from in-game income sources;
/// this packet just ships the counter + spend protocol.
/// </summary>
public sealed class HireRerollWallet
{
    /// <summary>Current spendable token balance. Always &gt;= 0.</summary>
    public int Balance { get; private set; }

    /// <summary>Fires whenever the balance changes (deposit or successful spend).</summary>
    public event Action<int>? OnBalanceChanged;

    /// <summary>Construct with an optional starting balance.</summary>
    public HireRerollWallet(int startingBalance = 0)
    {
        if (startingBalance < 0)
            throw new ArgumentOutOfRangeException(nameof(startingBalance), "starting balance must be >= 0");
        Balance = startingBalance;
    }

    /// <summary>Add tokens to the wallet. Throws on negative input.</summary>
    public void Deposit(int tokens)
    {
        if (tokens < 0) throw new ArgumentOutOfRangeException(nameof(tokens), "deposit must be >= 0");
        if (tokens == 0) return;
        Balance += tokens;
        OnBalanceChanged?.Invoke(Balance);
    }

    /// <summary>
    /// Atomically charge <paramref name="tokens"/>. Returns true on success;
    /// false (and balance unchanged) when there are insufficient funds. Negative
    /// inputs throw.
    /// </summary>
    public bool TrySpend(int tokens)
    {
        if (tokens < 0) throw new ArgumentOutOfRangeException(nameof(tokens), "spend must be >= 0");
        if (tokens == 0) return true;
        if (Balance < tokens) return false;
        Balance -= tokens;
        OnBalanceChanged?.Invoke(Balance);
        return true;
    }
}
