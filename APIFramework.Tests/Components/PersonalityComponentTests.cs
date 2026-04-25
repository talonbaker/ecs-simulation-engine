using APIFramework.Components;
using Xunit;

namespace APIFramework.Tests.Components;

public class PersonalityComponentTests
{
    [Fact]
    public void Constructor_ClampsBigFiveToMinus2Plus2()
    {
        var p = new PersonalityComponent(-5, 3, -3, 5, -10);
        Assert.Equal(-2, p.Openness);
        Assert.Equal( 2, p.Conscientiousness);
        Assert.Equal(-2, p.Extraversion);
        Assert.Equal( 2, p.Agreeableness);
        Assert.Equal(-2, p.Neuroticism);
    }

    [Fact]
    public void Constructor_ValidBigFive_PreservesValues()
    {
        var p = new PersonalityComponent(-2, 1, 0, -1, 2);
        Assert.Equal(-2, p.Openness);
        Assert.Equal( 1, p.Conscientiousness);
        Assert.Equal( 0, p.Extraversion);
        Assert.Equal(-1, p.Agreeableness);
        Assert.Equal( 2, p.Neuroticism);
    }

    [Fact]
    public void Constructor_TruncatesCurrentMoodAt32Chars()
    {
        var longMood = new string('x', 50);
        var p = new PersonalityComponent(0, 0, 0, 0, 0, currentMood: longMood);
        Assert.Equal(32, p.CurrentMood.Length);
    }

    [Fact]
    public void Constructor_AcceptsMoodUnder32Chars()
    {
        var p = new PersonalityComponent(0, 0, 0, 0, 0, currentMood: "fine");
        Assert.Equal("fine", p.CurrentMood);
    }

    [Fact]
    public void Setter_TruncatesMoodOver32Chars()
    {
        var p = new PersonalityComponent(0, 0, 0, 0, 0);
        p.CurrentMood = new string('z', 40);
        Assert.Equal(32, p.CurrentMood.Length);
    }

    [Fact]
    public void DefaultCurrentMood_IsEmpty()
    {
        var p = new PersonalityComponent(0, 0, 0, 0, 0);
        Assert.Equal(string.Empty, p.CurrentMood);
    }

    [Fact]
    public void VocabularyRegister_DefaultIsCasual()
    {
        var p = new PersonalityComponent(0, 0, 0, 0, 0);
        Assert.Equal(VocabularyRegister.Casual, p.VocabularyRegister);
    }

    [Theory]
    [InlineData(VocabularyRegister.Formal)]
    [InlineData(VocabularyRegister.Crass)]
    [InlineData(VocabularyRegister.Clipped)]
    [InlineData(VocabularyRegister.Academic)]
    [InlineData(VocabularyRegister.Folksy)]
    public void VocabularyRegister_AllValuesRoundTrip(VocabularyRegister reg)
    {
        var p = new PersonalityComponent(0, 0, 0, 0, 0, register: reg);
        Assert.Equal(reg, p.VocabularyRegister);
    }
}
