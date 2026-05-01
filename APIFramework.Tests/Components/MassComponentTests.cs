using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Components;

public class MassComponentTests
{
    [Fact]
    public void DefaultConstruction_MassKilograms_IsZero()
    {
        var c = new MassComponent();
        Assert.Equal(0f, c.MassKilograms);
    }

    [Fact]
    public void SetMassKilograms_RoundTrips()
    {
        var c = new MassComponent { MassKilograms = 1.2f };
        Assert.Equal(1.2f, c.MassKilograms);
    }

    [Fact]
    public void MugMass_IsPointFour()
    {
        var c = new MassComponent { MassKilograms = 0.4f };
        Assert.Equal(0.4f, c.MassKilograms);
    }
}
