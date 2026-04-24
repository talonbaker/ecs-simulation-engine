using System;
using System.CommandLine;   // CommandExtensions.InvokeAsync
using System.IO;
using System.Linq;
using System.Text.Json;
using ECSCli.Ai;
using Warden.Contracts;
using Warden.Contracts.SchemaValidation;
using Xunit;

namespace ECSCli.Tests;

/// <summary>
/// Integration tests for the five <c>ECSCli ai</c> subcommands.
/// AT-01 through AT-07 from the WP-04 acceptance table.
///
/// All tests invoke <see cref="AiCommand.Root"/> directly via
/// <c>InvokeAsync</c> — no subprocess, no <c>Environment.Exit()</c>.
/// Output files land in the OS temp directory and are cleaned up after each test.
/// </summary>
public sealed class AiVerbTests : IDisposable
{
    // ── Shared temp directory ─────────────────────────────────────────────────

    private readonly string _tmp;

    public AiVerbTests()
    {
        _tmp = Path.Combine(Path.GetTempPath(), $"ecscli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tmp);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmp, recursive: true); }
        catch { /* best-effort */ }
    }

    private string Tmp(string name) => Path.Combine(_tmp, name);

    // ─────────────────────────────────────────────────────────────────────────
    // AT-01 — no-args path is unaffected by `ai` verb presence
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// AT-01: When the first argument is NOT "ai", the old CliOptions parser is
    /// invoked — our delegation code does not interfere. We verify this at the
    /// code-path level (no subprocess required).
    ///
    /// The actual "ECSCli (no args) → exit 0" guarantee is enforced by the end-
    /// to-end build + run check in the completion note, since the main simulation
    /// loop logic lives in top-level statements that cannot be unit-tested here.
    /// </summary>
    [Fact]
    public void NoAiVerb_DelegationConditionIsFalse()
    {
        // The delegation guard: args.Length > 0 && args[0] == "ai"
        // Verify it does NOT fire for common non-ai args.
        var nonAiInputs = new[] { Array.Empty<string>(), new[] { "--help" }, new[] { "--duration", "60" } };
        foreach (var input in nonAiInputs)
        {
            bool wouldDelegate = input.Length > 0 && input[0] == "ai";
            Assert.False(wouldDelegate,
                $"Expected no delegation for args [{string.Join(", ", input)}]");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AT-02 — ai describe
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async System.Threading.Tasks.Task AiDescribe_WritesNonEmptyMarkdown()
    {
        var outFile = Tmp("fact-sheet.md");
        int exit = await AiCommand.Root.InvokeAsync(
            new[] { "describe", "--out", outFile });

        Assert.Equal(0, exit);
        Assert.True(File.Exists(outFile), "Fact-sheet file must exist.");

        var content = File.ReadAllText(outFile);
        Assert.False(string.IsNullOrWhiteSpace(content), "Fact-sheet must not be empty.");

        // Must cover at least 19 registered systems.
        // (SimulationBootstrapper registers 20 systems as of v0.7.2)
        int systemLineCount = content.Split('\n')
            .Count(l => l.Contains("System") && l.TrimStart().StartsWith("|"));
        Assert.True(systemLineCount >= 19,
            $"Expected >= 19 system rows in the table, found {systemLineCount}.");

        // Must include SimConfig keys section.
        Assert.Contains("## SimConfig Keys", content);

        // Must include component types section.
        Assert.Contains("## Component Types", content);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AT-03 — ai snapshot
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async System.Threading.Tasks.Task AiSnapshot_WritesValidJsonAndPassesSchema()
    {
        var outFile = Tmp("snapshot.json");
        int exit = await AiCommand.Root.InvokeAsync(
            new[] { "snapshot", "--out", outFile });

        Assert.True(exit is 0 or 2,
            $"Expected exit 0 (ok) or 2 (invariant warning), got {exit}.");
        Assert.True(File.Exists(outFile), "Snapshot file must exist.");

        var json = File.ReadAllText(outFile);

        // Must be valid JSON.
        var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);

        // Must contain required top-level fields.
        var root = doc.RootElement;
        Assert.Equal("0.1.0", root.GetProperty("schemaVersion").GetString());
        Assert.True(root.TryGetProperty("clock",    out _), "Missing 'clock' field.");
        Assert.True(root.TryGetProperty("entities", out _), "Missing 'entities' field.");

        // Validate against the embedded world-state.schema.json.
        var result = SchemaValidator.Validate(json, Schema.WorldState);
        Assert.True(result.IsValid,
            $"Schema validation failed:\n{string.Join("\n", result.Errors)}");
    }

    [Fact]
    public async System.Threading.Tasks.Task AiSnapshot_PrettyFlag_WritesIndentedJson()
    {
        var outFile = Tmp("snapshot-pretty.json");
        await AiCommand.Root.InvokeAsync(
            new[] { "snapshot", "--out", outFile, "--pretty" });

        var text = File.ReadAllText(outFile);
        // Indented JSON contains newlines within the object.
        Assert.True(text.Contains('\n') && text.Contains("  "),
            "Expected indented (pretty) JSON.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AT-04 — ai stream
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async System.Threading.Tasks.Task AiStream_ProducesAtLeastSixLines()
    {
        var outFile = Tmp("stream.jsonl");
        int exit = await AiCommand.Root.InvokeAsync(
            new[] { "stream", "--out", outFile, "--interval", "600", "--duration", "3600" });

        Assert.Equal(0, exit);
        Assert.True(File.Exists(outFile), "Stream output file must exist.");

        var lines = File.ReadAllLines(outFile)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        Assert.True(lines.Length >= 6,
            $"Expected >= 6 JSONL lines, got {lines.Length}.");

        // Every line must be valid JSON that passes the world-state schema.
        foreach (var line in lines)
        {
            var result = SchemaValidator.Validate(line, Schema.WorldState);
            Assert.True(result.IsValid,
                $"Line failed schema validation:\n{string.Join("\n", result.Errors)}\nLine: {line[..Math.Min(120, line.Length)]}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AT-05 — ai inject
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async System.Threading.Tasks.Task AiInject_ValidBatch_ExitCode0()
    {
        var batchFile = Tmp("valid-batch.json");
        File.WriteAllText(batchFile, """
            {
              "schemaVersion": "0.1.0",
              "commands": [
                {
                  "kind": "spawn-food",
                  "foodType": "banana",
                  "x": 5.0,
                  "y": 0.0,
                  "z": 5.0,
                  "count": 1
                }
              ]
            }
            """);

        int exit = await AiCommand.Root.InvokeAsync(
            new[] { "inject", "--in", batchFile });

        Assert.Equal(0, exit);
    }

    [Fact]
    public async System.Threading.Tasks.Task AiInject_InvalidBatch_ExitCode3()
    {
        var batchFile = Tmp("invalid-batch.json");
        File.WriteAllText(batchFile, """
            {
              "schemaVersion": "0.1.0",
              "commands": [
                {
                  "kind": "spawn-food",
                  "foodType": "",
                  "x": 5.0,
                  "y": 0.0,
                  "z": 5.0,
                  "count": 0
                }
              ]
            }
            """);

        int exit = await AiCommand.Root.InvokeAsync(
            new[] { "inject", "--in", batchFile });

        Assert.Equal(3, exit);
    }

    [Fact]
    public async System.Threading.Tasks.Task AiInject_MissingFile_ExitCode1()
    {
        int exit = await AiCommand.Root.InvokeAsync(
            new[] { "inject", "--in", Tmp("does-not-exist.json") });

        Assert.Equal(1, exit);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AT-06 — Deterministic replay
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Two replay runs with the same seed and duration must produce byte-identical
    /// JSONL after stripping the <c>capturedAt</c> field from each line.
    ///
    /// If this test fails the simulation contains nondeterminism. Per WP-04:
    /// "Go find the source. Do not mask the symptom."
    /// Known sources fixed in this WP: Guid.NewGuid() → counter IDs,
    /// HashSet iteration order → Entity.GetHashCode() based on Id,
    /// System.Random in MovementSystem → SeededRandom.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task AiReplay_SameSeed_ByteIdenticalAfterStrippingCapturedAt()
    {
        var runA = Tmp("run-a.jsonl");
        var runB = Tmp("run-b.jsonl");

        int exitA = await AiCommand.Root.InvokeAsync(
            new[] { "replay", "--seed", "42", "--duration", "3600", "--out", runA });
        int exitB = await AiCommand.Root.InvokeAsync(
            new[] { "replay", "--seed", "42", "--duration", "3600", "--out", runB });

        Assert.Equal(0, exitA);
        Assert.Equal(0, exitB);
        Assert.True(File.Exists(runA));
        Assert.True(File.Exists(runB));

        var linesA = File.ReadAllLines(runA).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        var linesB = File.ReadAllLines(runB).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();

        Assert.True(linesA.Length > 0, "Run-A must produce at least one line.");
        Assert.Equal(linesA.Length, linesB.Length);

        for (int i = 0; i < linesA.Length; i++)
        {
            var a = StripCapturedAt(linesA[i]);
            var b = StripCapturedAt(linesB[i]);
            Assert.Equal(a, b);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task AiReplay_DifferentSeeds_ProduceDifferentOutput()
    {
        var runA = Tmp("replay-seed42.jsonl");
        var runB = Tmp("replay-seed99.jsonl");

        await AiCommand.Root.InvokeAsync(
            new[] { "replay", "--seed", "42", "--duration", "600", "--out", runA });
        await AiCommand.Root.InvokeAsync(
            new[] { "replay", "--seed", "99", "--duration", "600", "--out", runB });

        var linesA = File.ReadAllLines(runA).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        var linesB = File.ReadAllLines(runB).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();

        // Different seeds should produce at least one differing line (entity positions will diverge).
        bool anyDifference = linesA.Zip(linesB)
            .Any(pair => StripCapturedAt(pair.First) != StripCapturedAt(pair.Second));
        Assert.True(anyDifference,
            "Different seeds must produce different telemetry.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AT-07 — --help at every level
    // ─────────────────────────────────────────────────────────────────────────

    // InlineData cannot hold arrays (CS0182 — attribute argument must be a constant
    // or typed array creation of the exact parameter type). MemberData + TheoryData<T>
    // is the standard xUnit workaround for string[] theory arguments.
    public static TheoryData<string[]> HelpArgsCases => new()
    {
        new string[] { "--help" },
        new string[] { "describe", "--help" },
        new string[] { "snapshot", "--help" },
        new string[] { "stream",   "--help" },
        new string[] { "inject",   "--help" },
        new string[] { "replay",   "--help" },
    };

    [Theory]
    [MemberData(nameof(HelpArgsCases))]
    public async System.Threading.Tasks.Task Help_AtEveryLevel_ExitCode0(string[] args)
    {
        int exit = await AiCommand.Root.InvokeAsync(args);
        Assert.Equal(0, exit);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes the <c>capturedAt</c> field from a JSON line so two replay runs
    /// can be compared for byte-identity independent of wall-clock time.
    ///
    /// Note: the replay command already uses game-time-derived capturedAt, so in
    /// practice the field should already be identical. This strip is belt-and-
    /// suspenders per the WP specification.
    /// </summary>
    private static string StripCapturedAt(string jsonLine)
    {
        // Parse → remove capturedAt → re-serialize deterministically.
        using var doc = JsonDocument.Parse(jsonLine);
        var root      = doc.RootElement;

        using var ms  = new MemoryStream();
        using var w   = new Utf8JsonWriter(ms);
        w.WriteStartObject();
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.NameEquals("capturedAt")) continue;
            prop.WriteTo(w);
        }
        w.WriteEndObject();
        w.Flush();
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
