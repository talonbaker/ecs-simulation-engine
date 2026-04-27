using APIFramework.Bootstrap;
using Xunit;

namespace APIFramework.Tests.Bootstrap;

/// <summary>
/// AT-02 — Pool contains the cast-bible's named exemplars: "Donna", "Greg", "Frank".
/// </summary>
public class NamePoolBibleExemplarsTests
{
    private static NamePoolDto LoadPool()
    {
        var path = NamePoolLoader.FindDefault();
        Assert.NotNull(path);
        return NamePoolLoader.Load(path!);
    }

    [Theory]
    [InlineData("Donna")]
    [InlineData("Greg")]
    [InlineData("Frank")]
    public void AT02_BibleExemplar_IsPresentInPool(string name)
    {
        var pool = LoadPool();
        Assert.Contains(name, pool.FirstNames, System.StringComparer.Ordinal);
    }
}
