using System.Collections.Generic;
using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Components;

/// <summary>
/// AT-01: BlockedActionsComponent and BlockedActionClass compile, instantiate,
/// and correctly report membership.
/// </summary>
public class BlockedActionsComponentTests
{
    [Fact]
    public void DefaultComponent_HasEmptySet()
    {
        var c = new BlockedActionsComponent();
        Assert.Empty(c.Blocked);
    }

    [Fact]
    public void EmptySet_NoContains()
    {
        var c = new BlockedActionsComponent(new HashSet<BlockedActionClass>());
        Assert.False(c.Contains(BlockedActionClass.Eat));
        Assert.False(c.Contains(BlockedActionClass.Sleep));
        Assert.False(c.Contains(BlockedActionClass.Urinate));
        Assert.False(c.Contains(BlockedActionClass.Defecate));
    }

    [Fact]
    public void SingleClass_ContainsOnlyThatClass()
    {
        var blocked = new HashSet<BlockedActionClass> { BlockedActionClass.Eat };
        var c = new BlockedActionsComponent(blocked);

        Assert.True(c.Contains(BlockedActionClass.Eat));
        Assert.False(c.Contains(BlockedActionClass.Sleep));
        Assert.False(c.Contains(BlockedActionClass.Urinate));
        Assert.False(c.Contains(BlockedActionClass.Defecate));
    }

    [Fact]
    public void AllFourClasses_AllContained()
    {
        var blocked = new HashSet<BlockedActionClass>
        {
            BlockedActionClass.Eat,
            BlockedActionClass.Sleep,
            BlockedActionClass.Urinate,
            BlockedActionClass.Defecate
        };
        var c = new BlockedActionsComponent(blocked);

        Assert.True(c.Contains(BlockedActionClass.Eat));
        Assert.True(c.Contains(BlockedActionClass.Sleep));
        Assert.True(c.Contains(BlockedActionClass.Urinate));
        Assert.True(c.Contains(BlockedActionClass.Defecate));
    }

    [Fact]
    public void BlockedSetHasFourDistinctValues()
    {
        var values = System.Enum.GetValues(typeof(BlockedActionClass));
        Assert.Equal(4, values.Length);
    }

    [Fact]
    public void Component_BlockedProperty_IsIReadOnlyCollection()
    {
        // IReadOnlyCollection<T> is returned (IReadOnlySet<T> not available in netstandard2.1)
        var blocked = new HashSet<BlockedActionClass> { BlockedActionClass.Eat };
        var c = new BlockedActionsComponent(blocked);

        Assert.IsAssignableFrom<System.Collections.Generic.IReadOnlyCollection<BlockedActionClass>>(c.Blocked);
    }

    [Fact]
    public void TwoInstances_WithSameSet_BothContainSameValues()
    {
        var set1 = new HashSet<BlockedActionClass> { BlockedActionClass.Sleep };
        var set2 = new HashSet<BlockedActionClass> { BlockedActionClass.Sleep };
        var c1 = new BlockedActionsComponent(set1);
        var c2 = new BlockedActionsComponent(set2);

        Assert.Equal(c1.Contains(BlockedActionClass.Sleep), c2.Contains(BlockedActionClass.Sleep));
        Assert.Equal(c1.Contains(BlockedActionClass.Eat),   c2.Contains(BlockedActionClass.Eat));
    }
}
