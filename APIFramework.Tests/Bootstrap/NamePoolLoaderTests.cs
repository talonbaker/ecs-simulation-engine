using System.Linq;
using APIFramework.Bootstrap;
using Xunit;

namespace APIFramework.Tests.Bootstrap;

/// <summary>
/// AT-01 — name-pool.json loads cleanly; ≥ 30 entries; all unique non-empty strings.
/// </summary>
public class NamePoolLoaderTests
{
    private static NamePoolDto LoadPool()
    {
        var path = NamePoolLoader.FindDefault();
        Assert.NotNull(path);
        return NamePoolLoader.Load(path!);
    }

    [Fact]
    public void AT01_Pool_LoadsWithoutException()
    {
        var ex = Record.Exception(LoadPool);
        Assert.Null(ex);
    }

    [Fact]
    public void AT01_Pool_HasAtLeastThirtyEntries()
    {
        var pool = LoadPool();
        Assert.True(pool.FirstNames.Length >= 30,
            $"Expected ≥ 30 names; got {pool.FirstNames.Length}");
    }

    [Fact]
    public void AT01_Pool_AllNamesAreNonEmpty()
    {
        var pool = LoadPool();
        Assert.All(pool.FirstNames, name =>
            Assert.False(string.IsNullOrWhiteSpace(name),
                "Name pool contains a null or whitespace entry."));
    }

    [Fact]
    public void AT01_Pool_AllNamesAreUnique()
    {
        var pool = LoadPool();
        var distinct = pool.FirstNames.Distinct(System.StringComparer.Ordinal).Count();
        Assert.Equal(pool.FirstNames.Length, distinct);
    }
}
