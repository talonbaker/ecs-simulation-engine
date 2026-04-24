namespace APIFramework.Core;

/// <summary>
/// Deterministic RNG source for simulation systems.
///
/// DESIGN CONTRACT
/// ───────────────
/// Given the same <paramref name="seed"/> value, any sequence of calls to
/// <see cref="NextFloat"/>, <see cref="NextDouble"/>, and <see cref="NextInt"/>
/// produces the same sequence of results on every run. This is the foundation
/// of the determinism guarantee required by WP-04 / Pillar A.
///
/// USAGE
/// ─────
/// <code>
///   var rng = new SeededRandom(42);
///   float x = rng.NextFloat();          // [0.0, 1.0)
///   int   n = rng.NextInt(100);          // [0, 100)
/// </code>
///
/// THREAD SAFETY
/// ─────────────
/// Not thread-safe. All ECS systems run on the same thread (sequential phase
/// execution), so no locking is required. Do not share an instance across threads.
/// </summary>
public sealed class SeededRandom
{
    private readonly Random _rng;

    /// <summary>The seed this instance was created with.</summary>
    public int Seed { get; }

    /// <summary>
    /// Initialises the RNG with the supplied <paramref name="seed"/>.
    /// Two instances with the same seed produce identical output streams.
    /// </summary>
    public SeededRandom(int seed)
    {
        Seed = seed;
        _rng = new Random(seed);
    }

    /// <summary>
    /// Returns a random <see cref="float"/> in the half-open range [0.0, 1.0).
    /// </summary>
    public float NextFloat() => (float)_rng.NextDouble();

    /// <summary>
    /// Returns a random <see cref="double"/> in the half-open range [0.0, 1.0).
    /// </summary>
    public double NextDouble() => _rng.NextDouble();

    /// <summary>
    /// Returns a non-negative random <see cref="int"/> in [0, <paramref name="maxExclusive"/>).
    /// </summary>
    /// <param name="maxExclusive">Exclusive upper bound. Must be greater than zero.</param>
    public int NextInt(int maxExclusive) => _rng.Next(maxExclusive);

    /// <summary>
    /// Returns a random <see cref="float"/> in [<paramref name="min"/>, <paramref name="max"/>).
    /// </summary>
    public float NextFloatRange(float min, float max) =>
        min + NextFloat() * (max - min);
}
