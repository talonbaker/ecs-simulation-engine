using System.Text.Json;
using System.Text.RegularExpressions;
using Warden.Contracts;
using Warden.Contracts.Handshake;

namespace Warden.Orchestrator.Mocks;

/// <summary>
/// Loads canned mock responses from the <c>examples/mocks/</c> directory.
/// File naming convention:
/// <list type="bullet">
///   <item><c>&lt;specId&gt;.sonnet.json</c> — one <see cref="SonnetResult"/> per spec.</item>
///   <item><c>&lt;specId&gt;.haiku-NN.json</c> — one <see cref="HaikuResult"/> per scenario.</item>
/// </list>
/// </summary>
public sealed class MockCannedResponses
{
    private readonly Dictionary<string, SonnetResult> _sonnetResults;
    private readonly Dictionary<string, HaikuResult>  _haikuResults;

    private MockCannedResponses(
        Dictionary<string, SonnetResult> sonnetResults,
        Dictionary<string, HaikuResult>  haikuResults)
    {
        _sonnetResults = sonnetResults;
        _haikuResults  = haikuResults;
    }

    /// <summary>
    /// Loads all canned responses from <paramref name="mocksDirectory"/>.
    /// Throws <see cref="InvalidOperationException"/> if the directory does not exist.
    /// </summary>
    public static MockCannedResponses Load(string mocksDirectory)
    {
        if (!Directory.Exists(mocksDirectory))
            throw new InvalidOperationException(
                $"Mocks directory not found: {mocksDirectory}. " +
                "Create it and populate it with *.sonnet.json and *.haiku-*.json files.");

        var sonnetResults = new Dictionary<string, SonnetResult>(StringComparer.Ordinal);
        var haikuResults  = new Dictionary<string, HaikuResult>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(mocksDirectory, "*.json"))
        {
            var name = Path.GetFileName(file);
            var json = File.ReadAllText(file);

            if (name.EndsWith(".sonnet.json", StringComparison.OrdinalIgnoreCase))
            {
                var result = JsonSerializer.Deserialize<SonnetResult>(json, JsonOptions.Wire)
                    ?? throw new InvalidDataException($"Failed to deserialise SonnetResult from {file}.");
                sonnetResults[result.SpecId] = result;
            }
            else if (Regex.IsMatch(name, @"\.haiku-\d+\.json$", RegexOptions.IgnoreCase))
            {
                var result = JsonSerializer.Deserialize<HaikuResult>(json, JsonOptions.Wire)
                    ?? throw new InvalidDataException($"Failed to deserialise HaikuResult from {file}.");
                haikuResults[result.ScenarioId] = result;
            }
        }

        return new MockCannedResponses(sonnetResults, haikuResults);
    }

    /// <summary>Returns the canned <see cref="SonnetResult"/> for <paramref name="specId"/>.</summary>
    /// <exception cref="KeyNotFoundException">No canned result for the given specId.</exception>
    public SonnetResult GetSonnetResult(string specId)
    {
        if (_sonnetResults.TryGetValue(specId, out var result))
            return result;
        throw new KeyNotFoundException(
            $"No canned SonnetResult for specId '{specId}'. " +
            $"Add a file named '{specId}.sonnet.json' to the mocks directory.");
    }

    /// <summary>
    /// Returns the canned <see cref="HaikuResult"/> for <paramref name="scenarioId"/>,
    /// or a blocked stub if none is registered.
    /// </summary>
    public HaikuResult GetHaikuResult(string scenarioId)
    {
        if (_haikuResults.TryGetValue(scenarioId, out var result))
            return result;

        return new HaikuResult
        {
            SchemaVersion    = "0.1.0",
            ScenarioId       = scenarioId,
            ParentBatchId    = "batch-mock-fallback",
            WorkerId         = "haiku-01",
            Outcome          = OutcomeCode.Blocked,
            BlockReason      = BlockReason.ToolError,
            AssertionResults = new List<AssertionResult>
            {
                new() { Id = "A-01", Passed = false, Note = "No canned result registered." }
            },
            TokensUsed       = new Contracts.Handshake.TokenUsage()
        };
    }
}
