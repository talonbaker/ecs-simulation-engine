using System.Collections.Generic;
using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Components;

/// <summary>
/// AT-01 — per-listener field correctness: isolation between listeners and struct reference semantics.
/// </summary>
public class DialogHistoryComponentPerListenerTests
{
    [Fact]
    public void DefaultConstruct_UsesByListenerAndFragmentId_IsEmptyNotNull()
    {
        var hist = new DialogHistoryComponent();
        Assert.NotNull(hist.UsesByListenerAndFragmentId);
        Assert.Empty(hist.UsesByListenerAndFragmentId);
    }

    [Fact]
    public void AddToOneListener_DoesNotAffectOtherListener()
    {
        var hist = new DialogHistoryComponent();

        hist.UsesByListenerAndFragmentId[1] = new Dictionary<string, int> { ["frag-A"] = 3 };
        hist.UsesByListenerAndFragmentId[2] = new Dictionary<string, int> { ["frag-B"] = 5 };

        Assert.Single(hist.UsesByListenerAndFragmentId[1]);
        Assert.Equal(3, hist.UsesByListenerAndFragmentId[1]["frag-A"]);
        Assert.False(hist.UsesByListenerAndFragmentId[1].ContainsKey("frag-B"));

        Assert.Single(hist.UsesByListenerAndFragmentId[2]);
        Assert.Equal(5, hist.UsesByListenerAndFragmentId[2]["frag-B"]);
        Assert.False(hist.UsesByListenerAndFragmentId[2].ContainsKey("frag-A"));
    }

    [Fact]
    public void StructCopy_SharesUnderlyingDictionary_Reference()
    {
        var hist = new DialogHistoryComponent();
        var copy = hist;

        hist.UsesByListenerAndFragmentId[42] = new Dictionary<string, int> { ["frag-X"] = 7 };

        Assert.Same(hist.UsesByListenerAndFragmentId, copy.UsesByListenerAndFragmentId);
        Assert.True(copy.UsesByListenerAndFragmentId.ContainsKey(42));
    }

    [Fact]
    public void UsesByListenerAndFragmentId_IsIndependentOf_UsesByFragmentId()
    {
        var hist = new DialogHistoryComponent();

        hist.UsesByListenerAndFragmentId[10] = new Dictionary<string, int> { ["frag-Z"] = 2 };

        Assert.Empty(hist.UsesByFragmentId);
        Assert.Single(hist.UsesByListenerAndFragmentId);
    }

    [Fact]
    public void IncrementCount_UpdatesOnlyTargetListenerFragment()
    {
        var hist = new DialogHistoryComponent();

        var perL1 = new Dictionary<string, int> { ["frag-A"] = 4 };
        var perL2 = new Dictionary<string, int> { ["frag-A"] = 1 };
        hist.UsesByListenerAndFragmentId[1] = perL1;
        hist.UsesByListenerAndFragmentId[2] = perL2;

        hist.UsesByListenerAndFragmentId[1]["frag-A"]++;

        Assert.Equal(5, hist.UsesByListenerAndFragmentId[1]["frag-A"]);
        Assert.Equal(1, hist.UsesByListenerAndFragmentId[2]["frag-A"]);
    }
}
