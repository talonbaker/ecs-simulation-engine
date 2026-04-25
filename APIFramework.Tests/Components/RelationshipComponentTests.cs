using System;
using System.Collections.Generic;
using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Components;

public class RelationshipComponentTests
{
    // AT-09: canonical pair ordering enforced at construction
    [Fact]
    public void Constructor_CanonicalizesOrder_HigherSecond()
    {
        var rel = new RelationshipComponent(7, 3);
        Assert.Equal(3, rel.ParticipantA);
        Assert.Equal(7, rel.ParticipantB);
    }

    [Fact]
    public void Constructor_AlreadyCanonical_Unchanged()
    {
        var rel = new RelationshipComponent(3, 7);
        Assert.Equal(3, rel.ParticipantA);
        Assert.Equal(7, rel.ParticipantB);
    }

    [Fact]
    public void Constructor_EqualIds_BothSameValue()
    {
        var rel = new RelationshipComponent(5, 5);
        Assert.Equal(5, rel.ParticipantA);
        Assert.Equal(5, rel.ParticipantB);
    }

    [Fact]
    public void TwoRelationships_SameCanonicalPair_SameIds()
    {
        var r1 = new RelationshipComponent(7, 3);
        var r2 = new RelationshipComponent(3, 7);
        Assert.Equal(r1.ParticipantA, r2.ParticipantA);
        Assert.Equal(r1.ParticipantB, r2.ParticipantB);
    }

    [Fact]
    public void Patterns_MaxTwo_Accepted()
    {
        var patterns = new List<RelationshipPattern> { RelationshipPattern.Rival, RelationshipPattern.OldFlame };
        var rel = new RelationshipComponent(1, 2, patterns);
        Assert.Equal(2, rel.Patterns.Count);
    }

    [Fact]
    public void Patterns_ThreeOrMore_Throws()
    {
        var patterns = new List<RelationshipPattern>
        {
            RelationshipPattern.Rival,
            RelationshipPattern.Friend,
            RelationshipPattern.Confidant
        };
        Assert.Throws<ArgumentException>(() => new RelationshipComponent(1, 2, patterns));
    }

    [Fact]
    public void Intensity_ClampedTo0_100()
    {
        var over  = new RelationshipComponent(1, 2, null, 150);
        var under = new RelationshipComponent(1, 2, null, -10);
        Assert.Equal(100, over.Intensity);
        Assert.Equal(0,   under.Intensity);
    }

    [Fact]
    public void DefaultIntensity_Is50()
    {
        var rel = new RelationshipComponent(1, 2);
        Assert.Equal(50, rel.Intensity);
    }

    [Fact]
    public void AllThirteenPatterns_Defined()
    {
        var values = (RelationshipPattern[])Enum.GetValues(typeof(RelationshipPattern));
        Assert.Equal(13, values.Length);
    }
}
