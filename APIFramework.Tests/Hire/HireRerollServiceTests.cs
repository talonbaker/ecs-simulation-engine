using System;
using System.Linq;
using APIFramework.Cast;
using APIFramework.Hire;
using Xunit;

namespace APIFramework.Tests.Hire;

/// <summary>
/// WP-4.0.N — hire reroll economy substrate. Session lifecycle + reroll + commit + cancel.
/// </summary>
public class HireRerollServiceTests
{
    private static readonly CastNameData     Data    = CastNameDataLoader.LoadDefault()!;
    private static readonly CastNameGenerator NameGen = new(Data);

    private static (HireRerollService Svc, HireRerollWallet Wallet) Setup(int wallet = 100, int maxRerolls = 5, int cost = 1)
    {
        var w  = new HireRerollWallet(startingBalance: wallet);
        var sc = new HireRerollService(NameGen, w, new HireRerollConfig { RerollCost = cost, MaxRerolls = maxRerolls });
        return (sc, w);
    }

    // ── Begin returns a session with an initial candidate ────────────────────────

    [Fact]
    public void Begin_ReturnsSession_WithInitialCandidate_AndZeroRerolls()
    {
        var (svc, _) = Setup();
        var session = svc.Begin(gender: CastGender.Female, rng: new Random(42));

        Assert.NotNull(session.CurrentCandidate);
        Assert.Equal(0, session.RerollsUsed);
        Assert.Single(session.History);
        Assert.False(session.IsCommitted);
        Assert.False(session.IsCancelled);
    }

    // ── TryReroll ────────────────────────────────────────────────────────────────

    [Fact]
    public void TryReroll_Success_ChargesWalletAndAdvancesCandidate()
    {
        var (svc, wallet) = Setup(wallet: 5);
        var session = svc.Begin(rng: new Random(1));

        var initial = session.CurrentCandidate;
        var ok = session.TryReroll();

        Assert.True(ok);
        Assert.Equal(1, session.RerollsUsed);
        Assert.Equal(2, session.History.Count);
        Assert.NotEqual(initial, session.CurrentCandidate);  // overwhelming probability
        Assert.Equal(4, wallet.Balance);                      // 5 - 1 spent
    }

    [Fact]
    public void TryReroll_FailsWhenWalletEmpty_NoTokenChargedNoCandidateChange()
    {
        var (svc, wallet) = Setup(wallet: 0);
        var session = svc.Begin(rng: new Random(2));

        var initial = session.CurrentCandidate;
        var ok = session.TryReroll();

        Assert.False(ok);
        Assert.Equal(0, session.RerollsUsed);
        Assert.Single(session.History);
        Assert.Equal(initial, session.CurrentCandidate);
        Assert.Equal(0, wallet.Balance);
    }

    [Fact]
    public void TryReroll_FailsAtCap_EvenWhenWalletHasFunds()
    {
        var (svc, wallet) = Setup(wallet: 100, maxRerolls: 3);
        var session = svc.Begin(rng: new Random(3));

        Assert.True(session.TryReroll());
        Assert.True(session.TryReroll());
        Assert.True(session.TryReroll());
        Assert.False(session.TryReroll());     // cap reached
        Assert.Equal(3, session.RerollsUsed);
        Assert.Equal(97, wallet.Balance);      // only 3 charges deducted
    }

    [Fact]
    public void TryReroll_HonorsRerollCost_NotJustOne()
    {
        var (svc, wallet) = Setup(wallet: 10, cost: 3);
        var session = svc.Begin(rng: new Random(4));

        Assert.True(session.TryReroll());
        Assert.Equal(7, wallet.Balance);
        Assert.True(session.TryReroll());
        Assert.Equal(4, wallet.Balance);
        Assert.True(session.TryReroll());
        Assert.Equal(1, wallet.Balance);
        Assert.False(session.TryReroll());     // 1 < 3, can't afford
    }

    // ── Commit / Cancel ─────────────────────────────────────────────────────────

    [Fact]
    public void Commit_ReturnsCurrentCandidate_AndFreezesSession()
    {
        var (svc, _) = Setup();
        var session = svc.Begin(rng: new Random(5));
        session.TryReroll();
        var committed = session.Commit();

        Assert.Equal(session.CurrentCandidate, committed);
        Assert.True(session.IsCommitted);
        Assert.Throws<InvalidOperationException>(() => session.TryReroll());
        Assert.Throws<InvalidOperationException>(() => session.Commit());
    }

    [Fact]
    public void Cancel_FreezesSession_DoesNotRefundTokens()
    {
        var (svc, wallet) = Setup(wallet: 10);
        var session = svc.Begin(rng: new Random(6));
        session.TryReroll();
        session.TryReroll();
        var balanceBeforeCancel = wallet.Balance;

        session.Cancel();

        Assert.True(session.IsCancelled);
        Assert.Equal(balanceBeforeCancel, wallet.Balance);   // no refund
        Assert.Throws<InvalidOperationException>(() => session.TryReroll());
        Assert.Throws<InvalidOperationException>(() => session.Cancel());
    }

    // ── History ──────────────────────────────────────────────────────────────────

    [Fact]
    public void History_CapturesEveryCandidateInOrder()
    {
        var (svc, _) = Setup(wallet: 10);
        var session = svc.Begin(rng: new Random(7));
        session.TryReroll();
        session.TryReroll();
        session.TryReroll();

        Assert.Equal(4, session.History.Count);
        Assert.Equal(0, session.History[0].Index);
        Assert.Equal(1, session.History[1].Index);
        Assert.Equal(2, session.History[2].Index);
        Assert.Equal(3, session.History[3].Index);
    }

    // ── Argument validation ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullArgs_Throw()
    {
        var w = new HireRerollWallet(0);
        var c = new HireRerollConfig();
        Assert.Throws<ArgumentNullException>(() => new HireRerollService(null!, w, c));
        Assert.Throws<ArgumentNullException>(() => new HireRerollService(NameGen, null!, c));
        Assert.Throws<ArgumentNullException>(() => new HireRerollService(NameGen, w, null!));
    }
}
