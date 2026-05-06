using System;
using APIFramework.Hire;
using Xunit;

namespace APIFramework.Tests.Hire;

public class HireRerollWalletTests
{
    [Fact]
    public void StartingBalance_DefaultsToZero()
    {
        Assert.Equal(0, new HireRerollWallet().Balance);
    }

    [Fact]
    public void StartingBalance_AcceptsPositive()
    {
        Assert.Equal(50, new HireRerollWallet(50).Balance);
    }

    [Fact]
    public void StartingBalance_RejectsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HireRerollWallet(-1));
    }

    [Fact]
    public void Deposit_AddsAndFiresEvent()
    {
        var w = new HireRerollWallet(10);
        int? lastBalance = null;
        w.OnBalanceChanged += b => lastBalance = b;

        w.Deposit(5);

        Assert.Equal(15, w.Balance);
        Assert.Equal(15, lastBalance);
    }

    [Fact]
    public void Deposit_Zero_DoesNotFireEvent()
    {
        var w = new HireRerollWallet(10);
        bool fired = false;
        w.OnBalanceChanged += _ => fired = true;
        w.Deposit(0);
        Assert.False(fired);
    }

    [Fact]
    public void Deposit_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HireRerollWallet().Deposit(-1));
    }

    [Fact]
    public void TrySpend_Success_DeductsAndFiresEvent()
    {
        var w = new HireRerollWallet(10);
        int? lastBalance = null;
        w.OnBalanceChanged += b => lastBalance = b;

        var ok = w.TrySpend(3);

        Assert.True(ok);
        Assert.Equal(7, w.Balance);
        Assert.Equal(7, lastBalance);
    }

    [Fact]
    public void TrySpend_InsufficientFunds_NoChange()
    {
        var w = new HireRerollWallet(2);
        bool fired = false;
        w.OnBalanceChanged += _ => fired = true;

        var ok = w.TrySpend(3);

        Assert.False(ok);
        Assert.Equal(2, w.Balance);
        Assert.False(fired);
    }

    [Fact]
    public void TrySpend_Zero_AlwaysSucceeds_NoEvent()
    {
        var w = new HireRerollWallet(0);
        bool fired = false;
        w.OnBalanceChanged += _ => fired = true;
        Assert.True(w.TrySpend(0));
        Assert.False(fired);
    }

    [Fact]
    public void TrySpend_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HireRerollWallet(10).TrySpend(-1));
    }
}
