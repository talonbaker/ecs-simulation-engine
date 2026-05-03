using System.IO;
using APIFramework.Systems.Animation;
using Xunit;

namespace APIFramework.Tests.Animation;

/// <summary>
/// AT-01: All 8 required states have catalog entries with valid frame timing + accent color.
/// AT-02: All 9 chibi cue families have catalog entries with sprite asset + fade-altitude.
/// AT-03: Catalog round-trips through JSON loader; missing optional fields fall back to defaults.
/// AT-04: Significant state transitions use intermediate frames per catalog.
/// </summary>
public class VisualStateCatalogJsonTests
{
    private static NpcVisualStateCatalog LoadCatalog()
    {
        var path = NpcVisualStateCatalogLoader.FindDefaultPath();
        Assert.True(path is not null && File.Exists(path),
            "visual-state-catalog.json not found via FindDefaultPath() — check file location");
        return NpcVisualStateCatalogLoader.Load(path);
    }

    // ── AT-01: All 8 required states present ────────────────────────────────

    [Fact]
    public void File_LoadsWithoutException()
    {
        var catalog = LoadCatalog();
        Assert.NotNull(catalog);
        Assert.NotEmpty(catalog.States);
    }

    [Theory]
    [InlineData("Idle")]
    [InlineData("Walk")]
    [InlineData("Eating")]
    [InlineData("Drinking")]
    [InlineData("Working")]
    [InlineData("Crying")]
    [InlineData("CoughingFit")]
    [InlineData("Heimlich")]
    public void RequiredState_HasCatalogEntry(string stateId)
    {
        var catalog = LoadCatalog();
        var entry   = catalog.GetState(stateId);
        Assert.NotNull(entry);
    }

    [Theory]
    [InlineData("Idle")]
    [InlineData("Walk")]
    [InlineData("Eating")]
    [InlineData("Drinking")]
    [InlineData("Working")]
    [InlineData("Crying")]
    [InlineData("CoughingFit")]
    [InlineData("Heimlich")]
    public void RequiredState_HasValidFrameDuration(string stateId)
    {
        var catalog = LoadCatalog();
        var entry   = catalog.GetState(stateId);
        Assert.NotNull(entry);
        Assert.InRange(entry.FrameDurationMs, 1, 10000);
    }

    [Theory]
    [InlineData("Idle")]
    [InlineData("Walk")]
    [InlineData("Eating")]
    [InlineData("Drinking")]
    [InlineData("Working")]
    [InlineData("Crying")]
    [InlineData("CoughingFit")]
    [InlineData("Heimlich")]
    public void RequiredState_HasAccentColor(string stateId)
    {
        var catalog = LoadCatalog();
        var entry   = catalog.GetState(stateId);
        Assert.NotNull(entry);
        Assert.False(string.IsNullOrWhiteSpace(entry.AccentColor),
            $"State '{stateId}' must have a non-empty accentColor");
        Assert.True(entry.AccentColor.StartsWith("#"),
            $"AccentColor for '{stateId}' must start with '#'");
    }

    // ── AT-02: All 9 cue families present ────────────────────────────────────

    [Theory]
    [InlineData("anger-lines")]
    [InlineData("sweat")]
    [InlineData("sleep-z")]
    [InlineData("red-face-flush")]
    [InlineData("green-face-nausea")]
    [InlineData("heart")]
    [InlineData("sparkles")]
    [InlineData("exclamation")]
    [InlineData("question-mark")]
    public void RequiredCue_HasCatalogEntry(string cueId)
    {
        var catalog = LoadCatalog();
        var entry   = catalog.GetCue(cueId);
        Assert.NotNull(entry);
    }

    [Theory]
    [InlineData("anger-lines")]
    [InlineData("sweat")]
    [InlineData("sleep-z")]
    [InlineData("red-face-flush")]
    [InlineData("green-face-nausea")]
    [InlineData("heart")]
    [InlineData("sparkles")]
    [InlineData("exclamation")]
    [InlineData("question-mark")]
    public void RequiredCue_HasSpriteAsset(string cueId)
    {
        var catalog = LoadCatalog();
        var entry   = catalog.GetCue(cueId);
        Assert.NotNull(entry);
        Assert.False(string.IsNullOrWhiteSpace(entry.SpriteAsset),
            $"Cue '{cueId}' must declare a spriteAsset filename");
    }

    [Theory]
    [InlineData("anger-lines")]
    [InlineData("sweat")]
    [InlineData("sleep-z")]
    [InlineData("red-face-flush")]
    [InlineData("green-face-nausea")]
    [InlineData("heart")]
    [InlineData("sparkles")]
    [InlineData("exclamation")]
    [InlineData("question-mark")]
    public void RequiredCue_HasValidFadeAltitude(string cueId)
    {
        var catalog = LoadCatalog();
        var entry   = catalog.GetCue(cueId);
        Assert.NotNull(entry);
        Assert.True(entry.FadeAltitudeEnd > entry.FadeAltitudeStart,
            $"Cue '{cueId}': fadeAltitudeEnd ({entry.FadeAltitudeEnd}) must exceed fadeAltitudeStart ({entry.FadeAltitudeStart})");
    }

    // ── AT-02: Must-stay-visible cues have high fade altitude ────────────────

    [Theory]
    [InlineData("sleep-z",           25.0f)]
    [InlineData("red-face-flush",    25.0f)]
    [InlineData("green-face-nausea", 25.0f)]
    [InlineData("exclamation",       25.0f)]
    public void HighPriorityCue_RemainsVisibleAt25m(string cueId, float testAltitude)
    {
        var catalog = LoadCatalog();
        var entry   = catalog.GetCue(cueId);
        Assert.NotNull(entry);

        float alpha = NpcVisualStateCatalog.ComputeCueAlpha(
            testAltitude, entry.FadeAltitudeStart, entry.FadeAltitudeEnd);

        Assert.True(alpha > 0f,
            $"Cue '{cueId}' must be visible (alpha > 0) at altitude {testAltitude}m " +
            $"(fadeStart={entry.FadeAltitudeStart}, fadeEnd={entry.FadeAltitudeEnd})");
    }

    // ── AT-03: Catalog round-trips; missing fields fall back to defaults ──────

    [Fact]
    public void ParseJson_MinimalValidJson_ReturnsNonEmptyCatalog()
    {
        const string json = @"{
            ""schemaVersion"": ""0.1.0"",
            ""states"": [{ ""stateId"": ""Idle"", ""frameDurationMs"": 200, ""accentColor"": ""#aaa"" }],
            ""cues"": [],
            ""transitions"": []
        }";
        var catalog = NpcVisualStateCatalogLoader.ParseJson(json);
        Assert.NotNull(catalog);
        Assert.Single(catalog.States);
        Assert.Equal("0.1.0", catalog.SchemaVersion);
    }

    [Fact]
    public void ParseJson_MissingOptionalCueAffinity_FallsBackToEmptyArray()
    {
        const string json = @"{
            ""schemaVersion"": ""0.1.0"",
            ""states"": [{ ""stateId"": ""Walk"", ""frameDurationMs"": 120, ""accentColor"": ""#ccc"" }],
            ""cues"": [], ""transitions"": []
        }";
        var catalog = NpcVisualStateCatalogLoader.ParseJson(json);
        var state   = catalog.GetState("Walk");
        Assert.NotNull(state);
        Assert.NotNull(state.CueAffinity);
        Assert.Empty(state.CueAffinity);
    }

    [Fact]
    public void ParseJson_EmptyOrNullJson_ReturnsEmptyCatalog()
    {
        var emptyResult = NpcVisualStateCatalogLoader.ParseJson(string.Empty);
        Assert.NotNull(emptyResult);
        Assert.Empty(emptyResult.States);

        var nullResult = NpcVisualStateCatalogLoader.ParseJson(null!);
        Assert.NotNull(nullResult);
        Assert.Empty(nullResult.States);
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmptyCatalog()
    {
        var catalog = NpcVisualStateCatalogLoader.Load("/nonexistent/path/visual-state-catalog.json");
        Assert.NotNull(catalog);
        Assert.Empty(catalog.States);
    }

    [Fact]
    public void GetState_UnknownState_ReturnsNull()
    {
        var catalog = LoadCatalog();
        Assert.Null(catalog.GetState("NonexistentState"));
    }

    [Fact]
    public void GetCue_UnknownCue_ReturnsNull()
    {
        var catalog = LoadCatalog();
        Assert.Null(catalog.GetCue("nonexistent-cue"));
    }

    [Fact]
    public void GetAnimatorSpeed_Dead_IsVerySmall()
    {
        var catalog = LoadCatalog();
        float speed = catalog.GetAnimatorSpeed(APIFramework.Components.NpcAnimationState.Dead);
        // Dead frameDurationMs is 9999 → speed ≈ 200/9999 ≈ 0.02
        Assert.True(speed < 0.1f,
            $"Dead state animator speed should be very small (got {speed:F4})");
    }

    [Fact]
    public void GetAnimatorSpeed_Walk_IsFasterThanIdle()
    {
        var catalog   = LoadCatalog();
        float walkSpeed = catalog.GetAnimatorSpeed(APIFramework.Components.NpcAnimationState.Walk);
        float idleSpeed = catalog.GetAnimatorSpeed(APIFramework.Components.NpcAnimationState.Idle);
        Assert.True(walkSpeed > idleSpeed,
            $"Walk ({walkSpeed:F2}) should play faster than Idle ({idleSpeed:F2})");
    }

    // ── AT-04: Significant transitions have intermediate frames ──────────────

    [Theory]
    [InlineData("Walk",   "CoughingFit")]
    [InlineData("Eating", "CoughingFit")]
    public void SignificantTransition_HasIntermediateFrames(string from, string to)
    {
        var catalog    = LoadCatalog();
        var fromState  = System.Enum.Parse<APIFramework.Components.NpcAnimationState>(from);
        var toState    = System.Enum.Parse<APIFramework.Components.NpcAnimationState>(to);
        var transition = catalog.GetTransition(fromState, toState);
        Assert.NotNull(transition);
        Assert.NotEmpty(transition.IntermediateFrames);
        Assert.True(transition.TotalDurationMs > 0,
            $"Transition {from}→{to} must have a positive totalDurationMs");
    }

    // ── Alpha computation ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(10f,  25f, 35f, 1.0f)]   // below fadeStart → full alpha
    [InlineData(25f,  25f, 35f, 1.0f)]   // at fadeStart → full alpha
    [InlineData(30f,  25f, 35f, 0.5f)]   // midpoint → half alpha
    [InlineData(35f,  25f, 35f, 0.0f)]   // at fadeEnd → zero alpha
    [InlineData(40f,  25f, 35f, 0.0f)]   // above fadeEnd → zero alpha
    public void ComputeCueAlpha_ReturnsCorrectValue(
        float altitude, float fadeStart, float fadeEnd, float expectedAlpha)
    {
        float alpha = NpcVisualStateCatalog.ComputeCueAlpha(altitude, fadeStart, fadeEnd);
        Assert.Equal(expectedAlpha, alpha, precision: 4);
    }

    // ── Schema version ───────────────────────────────────���────────────────────

    [Fact]
    public void SchemaVersion_IsPresent()
    {
        var catalog = LoadCatalog();
        Assert.False(string.IsNullOrWhiteSpace(catalog.SchemaVersion),
            "visual-state-catalog.json must declare a schemaVersion");
    }
}
