using Warden.Orchestrator.Persistence;
using Xunit;

namespace Warden.Orchestrator.Tests.Persistence;

public class BudgetGuardTests
{
    // ── AT-04: Fails closed when running sum + projected > budget ─────────────

    [Fact]
    public void AT04_BudgetGuard_HaltsWhenProjectedExceedsBudget()
    {
        var guard = new BudgetGuard(1.00m);

        // Record $0.80 of actual spend.
        guard.Record(0.80m);

        // Projecting $0.21 would push total to $1.01 — must block.
        var verdict = guard.Check(0.21m);

        Assert.False(verdict.CanProceed);
        Assert.Equal("budget-exceeded", verdict.HaltReason);
        Assert.Equal(0.80m, verdict.SpentUsd);
        Assert.Equal(0.20m, verdict.RemainingUsd);
    }

    [Fact]
    public void AT04_BudgetGuard_AllowsWhenProjectedFitsInBudget()
    {
        var guard = new BudgetGuard(1.00m);
        guard.Record(0.80m);

        // $0.80 spent; $0.20 remaining; project exactly $0.20 → still within budget.
        var verdict = guard.Check(0.20m);

        Assert.True(verdict.CanProceed);
        Assert.Null(verdict.HaltReason);
    }

    // ── AT-05: Running sum accumulates; blocks once ceiling is reached ─────────

    /// <summary>
    /// AT-05: With a $1.00 budget and $0.06 projected per call, the guard
    /// allows 16 consecutive Check + Record cycles ($0.96 cumulative) and
    /// blocks on the 17th ($0.96 + $0.06 = $1.02 > $1.00).
    ///
    /// Note: the packet illustrates this as "blocks the 25th call" but uses
    /// $0.06/call figures; the first blocking call is mathematically the 17th.
    /// This test asserts the correct boundary, not the illustrative number.
    /// </summary>
    [Fact]
    public void AT05_BudgetGuard_BlocksOnceRunningTotalExceedsBudget()
    {
        const decimal budget  = 1.00m;
        const decimal perCall = 0.06m;
        var guard = new BudgetGuard(budget);

        // 16 calls × $0.06 = $0.96 ≤ $1.00 — all must proceed.
        for (int i = 1; i <= 16; i++)
        {
            var v = guard.Check(perCall);
            Assert.True(v.CanProceed, $"Call {i} should have proceeded (spent=${v.SpentUsd})");
            guard.Record(perCall);
        }

        // 17th call: $0.96 + $0.06 = $1.02 > $1.00 — must block.
        var blocked = guard.Check(perCall);
        Assert.False(blocked.CanProceed);
        Assert.Equal("budget-exceeded", blocked.HaltReason);
        Assert.Equal(16 * perCall, blocked.SpentUsd);
    }

    // ── Thread safety smoke test ───────────────────────────────────────────────

    [Fact]
    public async Task BudgetGuard_ConcurrentRecords_DoNotCorruptRunningSum()
    {
        const int threads  = 20;
        const decimal each = 0.01m;
        var guard = new BudgetGuard(decimal.MaxValue);

        await Task.WhenAll(Enumerable.Range(0, threads)
            .Select(_ => Task.Run(() => guard.Record(each))));

        Assert.Equal(threads * each, guard.SpentUsd);
    }
}
