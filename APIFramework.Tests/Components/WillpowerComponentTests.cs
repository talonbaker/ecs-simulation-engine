using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Components;

public class WillpowerComponentTests
{
    [Fact]
    public void Constructor_ClampsCurrentBelow0()
    {
        var wp = new WillpowerComponent(-10, 50);
        Assert.Equal(0, wp.Current);
    }

    [Fact]
    public void Constructor_ClampsCurrentAbove100()
    {
        var wp = new WillpowerComponent(200, 50);
        Assert.Equal(100, wp.Current);
    }

    [Fact]
    public void Constructor_ClampsBaselineBelow0()
    {
        var wp = new WillpowerComponent(50, -5);
        Assert.Equal(0, wp.Baseline);
    }

    [Fact]
    public void Constructor_ClampsBaselineAbove100()
    {
        var wp = new WillpowerComponent(50, 999);
        Assert.Equal(100, wp.Baseline);
    }

    [Fact]
    public void Constructor_ValidRange_PreservesValues()
    {
        var wp = new WillpowerComponent(75, 60);
        Assert.Equal(75, wp.Current);
        Assert.Equal(60, wp.Baseline);
    }

    [Fact]
    public void DefaultStruct_IsZero()
    {
        var wp = new WillpowerComponent();
        Assert.Equal(0, wp.Current);
        Assert.Equal(0, wp.Baseline);
    }

    [Fact]
    public void BoundaryValues_Accepted()
    {
        var wp = new WillpowerComponent(0, 100);
        Assert.Equal(0,   wp.Current);
        Assert.Equal(100, wp.Baseline);
    }
}
