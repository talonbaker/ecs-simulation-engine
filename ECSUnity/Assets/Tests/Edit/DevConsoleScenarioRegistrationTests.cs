using NUnit.Framework;

/// <summary>Edit-mode registration tests for ScenarioCommand — WP-PT.1.</summary>
[TestFixture]
public class DevConsoleScenarioRegistrationTests
{
#if WARDEN
    private ScenarioCommand _cmd;

    [SetUp]
    public void SetUp() => _cmd = new ScenarioCommand();

    [Test]
    public void AllTwelveSubverbsRegistered()
    {
        var names = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var sv in _cmd.Registry.All) names.Add(sv.Name);

        string[] expected =
        {
            "choke", "slip", "faint", "lockout", "kill",
            "rescue", "chore-microwave-to", "throw", "sound",
            "set-time", "seed-stains", "seed-bereavement",
        };

        foreach (var e in expected)
            Assert.IsTrue(names.Contains(e), $"Missing sub-verb: {e}");

        Assert.AreEqual(expected.Length, _cmd.Registry.All.Count,
            "Registry count mismatch — extra or duplicate sub-verb registered.");
    }

    [Test]
    public void NoArgsReturnsHelpText()
    {
        string result = _cmd.Execute(new string[0], new DevCommandContext());
        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result);
        Assert.IsFalse(result.StartsWith("ERROR:"), "scenario (no args) must not return error.");
    }

    [Test]
    public void UnknownSubverbReturnsError()
    {
        string result = _cmd.Execute(new[] { "nonexistent-subverb" }, new DevCommandContext());
        Assert.IsNotNull(result);
        Assert.IsTrue(result.StartsWith("ERROR:"), "Unknown sub-verb must return ERROR:.");
    }

    [Test]
    public void HelpSubverbPrintsDetail()
    {
        string result = _cmd.Execute(new[] { "help", "choke" }, new DevCommandContext());
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("choke"), "help choke must mention 'choke'.");
        Assert.IsFalse(result.StartsWith("ERROR:"));
    }
#else
    [Test]
    public void RetailSkip() => Assert.Pass("RETAIL — skipped.");
#endif
}
