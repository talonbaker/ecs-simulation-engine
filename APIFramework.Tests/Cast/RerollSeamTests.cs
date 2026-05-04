using System;
using APIFramework.Cast;
using Xunit;

namespace APIFramework.Tests.Cast;

/// <summary>
/// Verifies the reroll seam: same seed → same result; different seed → almost-always different result.
/// The future loot-box hire mechanic builds on these guarantees — a reroll is just calling Generate
/// with a fresh Random instance.
/// </summary>
public class RerollSeamTests
{
    private static readonly CastNameData Data = CastNameDataLoader.LoadDefault()!;
    private static readonly CastNameGenerator Gen = new(Data);

    [Fact]
    public void SameSeed_DifferentInstances_ProduceIdenticalResults()
    {
        for (int seed = 0; seed < 25; seed++)
        {
            var a = Gen.Generate(new Random(seed));
            var b = Gen.Generate(new Random(seed));
            Assert.Equal(a, b);
        }
    }

    [Fact]
    public void Reroll_OnFreshSeed_ChangesOutcome()
    {
        // Simulate the loot-box reroll loop: spend a token, reroll, get a new candidate.
        var initial = Gen.Generate(seed: 1);
        var changed = 0;
        for (int rerollSeed = 2; rerollSeed < 22; rerollSeed++)
        {
            var rerolled = Gen.Generate(seed: rerollSeed);
            if (rerolled.DisplayName != initial.DisplayName) changed++;
        }
        Assert.True(changed >= 18, $"Expected nearly all rerolls to change the name; only {changed}/20 did.");
    }

    [Fact]
    public void ConsecutiveCallsOnSameRng_ProduceDifferentResults()
    {
        // The other reroll mode: same RNG instance, advance state per call.
        var rng    = new Random(99);
        var first  = Gen.Generate(rng);
        var second = Gen.Generate(rng);
        // With overwhelming probability these differ.
        Assert.NotEqual(first.DisplayName, second.DisplayName);
    }
}
