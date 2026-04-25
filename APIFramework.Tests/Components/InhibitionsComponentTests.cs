using System;
using System.Collections.Generic;
using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Components;

public class InhibitionsComponentTests
{
    [Fact]
    public void EmptyList_Accepted()
    {
        var c = new InhibitionsComponent(new List<Inhibition>());
        Assert.Empty(c.Inhibitions);
    }

    [Fact]
    public void MaxEight_Accepted()
    {
        var list = new List<Inhibition>();
        for (int i = 0; i < 8; i++)
            list.Add(new Inhibition(InhibitionClass.Vulnerability, 50, InhibitionAwareness.Known));

        var c = new InhibitionsComponent(list);
        Assert.Equal(8, c.Inhibitions.Count);
    }

    [Fact]
    public void NineInhibitions_Throws()
    {
        var list = new List<Inhibition>();
        for (int i = 0; i < 9; i++)
            list.Add(new Inhibition(InhibitionClass.Confrontation, 50, InhibitionAwareness.Known));

        Assert.Throws<ArgumentException>(() => new InhibitionsComponent(list));
    }

    [Fact]
    public void Inhibition_StrengthClamped()
    {
        var inh = new Inhibition(InhibitionClass.RiskTaking, 150, InhibitionAwareness.Hidden);
        Assert.Equal(100, inh.Strength);

        var inh2 = new Inhibition(InhibitionClass.RiskTaking, -5, InhibitionAwareness.Hidden);
        Assert.Equal(0, inh2.Strength);
    }

    [Fact]
    public void Inhibition_AwarenessPreserved()
    {
        var known  = new Inhibition(InhibitionClass.Infidelity, 80, InhibitionAwareness.Known);
        var hidden = new Inhibition(InhibitionClass.Infidelity, 80, InhibitionAwareness.Hidden);
        Assert.Equal(InhibitionAwareness.Known,  known.Awareness);
        Assert.Equal(InhibitionAwareness.Hidden, hidden.Awareness);
    }

    [Fact]
    public void AllEightInhibitionClasses_Accepted()
    {
        var classes = (InhibitionClass[])Enum.GetValues(typeof(InhibitionClass));
        Assert.Equal(8, classes.Length);

        var list = new List<Inhibition>();
        foreach (var cls in classes)
            list.Add(new Inhibition(cls, 50, InhibitionAwareness.Known));

        var c = new InhibitionsComponent(list);
        Assert.Equal(8, c.Inhibitions.Count);
    }

    [Fact]
    public void DefaultComponent_ReturnsEmptyList()
    {
        var c = new InhibitionsComponent();
        Assert.Empty(c.Inhibitions);
    }
}
