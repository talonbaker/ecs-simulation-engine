using System;
using Xunit;
using APIFramework.Components;

namespace APIFramework.Tests.Components;

public class FallRiskComponentTests
{
    [Fact]
    public void Construction_Succeeds()
    {
        var component = new FallRiskComponent { RiskLevel = 0.5f };
        Assert.Equal(0.5f, component.RiskLevel);
    }

    [Fact]
    public void RiskLevel_CanBeZero()
    {
        var component = new FallRiskComponent { RiskLevel = 0.0f };
        Assert.Equal(0.0f, component.RiskLevel);
    }

    [Fact]
    public void RiskLevel_CanBeOne()
    {
        var component = new FallRiskComponent { RiskLevel = 1.0f };
        Assert.Equal(1.0f, component.RiskLevel);
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.5f)]
    [InlineData(0.9f)]
    public void RiskLevel_StoresArbitraryValue(float riskLevel)
    {
        var component = new FallRiskComponent { RiskLevel = riskLevel };
        Assert.Equal(riskLevel, component.RiskLevel);
    }
}
