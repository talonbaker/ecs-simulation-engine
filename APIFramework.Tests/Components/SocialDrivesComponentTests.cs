using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Components;

public class SocialDrivesComponentTests
{
    [Fact]
    public void DefaultConstruct_AllDrivesZero()
    {
        var c = new SocialDrivesComponent();
        Assert.Equal(0, c.Belonging.Current);
        Assert.Equal(0, c.Loneliness.Baseline);
    }

    [Fact]
    public void Clamp0100_ClampsBelow0()
    {
        Assert.Equal(0,   SocialDrivesComponent.Clamp0100(-5));
        Assert.Equal(0,   SocialDrivesComponent.Clamp0100(int.MinValue));
    }

    [Fact]
    public void Clamp0100_ClampsAbove100()
    {
        Assert.Equal(100, SocialDrivesComponent.Clamp0100(101));
        Assert.Equal(100, SocialDrivesComponent.Clamp0100(int.MaxValue));
    }

    [Fact]
    public void Clamp0100_MidRange_Unchanged()
    {
        Assert.Equal(50, SocialDrivesComponent.Clamp0100(50));
        Assert.Equal(0,  SocialDrivesComponent.Clamp0100(0));
        Assert.Equal(100,SocialDrivesComponent.Clamp0100(100));
    }

    [Fact]
    public void DriveValue_CurrentAndBaselineIndependent()
    {
        var c = new SocialDrivesComponent
        {
            Belonging  = new DriveValue { Current = 70, Baseline = 50 },
            Loneliness = new DriveValue { Current = 30, Baseline = 40 }
        };
        Assert.Equal(70, c.Belonging.Current);
        Assert.Equal(50, c.Belonging.Baseline);
        Assert.Equal(30, c.Loneliness.Current);
        Assert.Equal(40, c.Loneliness.Baseline);
    }

    [Fact]
    public void AllEightDriveFields_Accessible()
    {
        var c = new SocialDrivesComponent
        {
            Belonging  = new DriveValue { Current = 1,  Baseline = 10 },
            Status     = new DriveValue { Current = 2,  Baseline = 20 },
            Affection  = new DriveValue { Current = 3,  Baseline = 30 },
            Irritation = new DriveValue { Current = 4,  Baseline = 40 },
            Attraction = new DriveValue { Current = 5,  Baseline = 50 },
            Trust      = new DriveValue { Current = 6,  Baseline = 60 },
            Suspicion  = new DriveValue { Current = 7,  Baseline = 70 },
            Loneliness = new DriveValue { Current = 8,  Baseline = 80 }
        };

        Assert.Equal(1, c.Belonging.Current);
        Assert.Equal(2, c.Status.Current);
        Assert.Equal(3, c.Affection.Current);
        Assert.Equal(4, c.Irritation.Current);
        Assert.Equal(5, c.Attraction.Current);
        Assert.Equal(6, c.Trust.Current);
        Assert.Equal(7, c.Suspicion.Current);
        Assert.Equal(8, c.Loneliness.Current);
    }
}
