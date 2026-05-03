using System;
using System.Collections.Generic;
using System.IO;
using APIFramework.Components;
using Newtonsoft.Json;

namespace APIFramework.Systems.Animation;

// ── Data types ────────────────────────────────────────────────────────────────

/// <summary>
/// Per-state visual treatment: frame timing, accent color, preferred cue families.
/// </summary>
public sealed class StateVisualEntry
{
    public string   StateId        { get; }
    public int      FrameDurationMs { get; }
    public string   AccentColor    { get; }
    public string[] CueAffinity    { get; }

    public StateVisualEntry(string stateId, int frameDurationMs, string accentColor, string[] cueAffinity)
    {
        StateId        = stateId;
        FrameDurationMs = frameDurationMs;
        AccentColor    = accentColor;
        CueAffinity    = cueAffinity ?? Array.Empty<string>();
    }
}

/// <summary>
/// Per-cue rendering parameters: sprite asset name, anchor offset, altitude fade bounds, scale multiplier.
/// </summary>
public sealed class CueVisualEntry
{
    public string    CueId              { get; }
    public string    IconKindStr        { get; }
    public string    SpriteAsset        { get; }
    public float[]   AnchorOffset       { get; }
    public float     FadeAltitudeStart  { get; }
    public float     FadeAltitudeEnd    { get; }
    public float     MinScaleMult       { get; }

    public CueVisualEntry(string cueId, string iconKindStr, string spriteAsset,
        float[] anchorOffset, float fadeAltStart, float fadeAltEnd, float minScaleMult)
    {
        CueId             = cueId;
        IconKindStr       = iconKindStr;
        SpriteAsset       = spriteAsset;
        AnchorOffset      = anchorOffset ?? new float[] { 0f, 1.6f, 0f };
        FadeAltitudeStart = fadeAltStart;
        FadeAltitudeEnd   = fadeAltEnd;
        MinScaleMult      = minScaleMult;
    }
}

/// <summary>
/// Per-pair state transition: intermediate frame indices and total crossfade duration.
/// </summary>
public sealed class TransitionVisualEntry
{
    public string  From              { get; }
    public string  To                { get; }
    public int[]   IntermediateFrames { get; }
    public int     TotalDurationMs   { get; }

    public TransitionVisualEntry(string from, string to, int[] frames, int totalDurationMs)
    {
        From               = from;
        To                 = to;
        IntermediateFrames = frames ?? Array.Empty<int>();
        TotalDurationMs    = totalDurationMs;
    }
}

// ── Catalog ───────────────────────────────────────────────────────────────────

/// <summary>
/// Data-driven visual state catalog for NPC animation states, chibi cues, and transitions.
/// Loaded from <c>docs/c2-content/animation/visual-state-catalog.json</c>.
///
/// This is the MAC-013 extension surface: modders add new states/cues/transitions
/// by extending the JSON without code changes.
/// </summary>
public sealed class NpcVisualStateCatalog
{
    private readonly Dictionary<string, StateVisualEntry>      _byStateId;
    private readonly Dictionary<string, CueVisualEntry>        _byCueId;
    private readonly Dictionary<(string from, string to), TransitionVisualEntry> _byTransition;

    public IReadOnlyCollection<StateVisualEntry>      States      { get; }
    public IReadOnlyCollection<CueVisualEntry>        Cues        { get; }
    public IReadOnlyCollection<TransitionVisualEntry> Transitions { get; }
    public string SchemaVersion { get; }

    public NpcVisualStateCatalog(
        string schemaVersion,
        IEnumerable<StateVisualEntry>      states,
        IEnumerable<CueVisualEntry>        cues,
        IEnumerable<TransitionVisualEntry> transitions)
    {
        SchemaVersion = schemaVersion ?? "0.0.0";

        var stateList = new List<StateVisualEntry>(states ?? Array.Empty<StateVisualEntry>());
        var cueList   = new List<CueVisualEntry>(cues   ?? Array.Empty<CueVisualEntry>());
        var transList = new List<TransitionVisualEntry>(transitions ?? Array.Empty<TransitionVisualEntry>());

        States      = stateList.AsReadOnly();
        Cues        = cueList.AsReadOnly();
        Transitions = transList.AsReadOnly();

        _byStateId = new Dictionary<string, StateVisualEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in stateList)
            _byStateId[s.StateId] = s;

        _byCueId = new Dictionary<string, CueVisualEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in cueList)
            _byCueId[c.CueId] = c;

        _byTransition = new Dictionary<(string, string), TransitionVisualEntry>();
        foreach (var t in transList)
            _byTransition[(t.From.ToLowerInvariant(), t.To.ToLowerInvariant())] = t;
    }

    /// <summary>Returns the state entry for <paramref name="stateId"/>, or null if not catalogued.</summary>
    public StateVisualEntry GetState(string stateId) =>
        _byStateId.TryGetValue(stateId, out var e) ? e : null;

    /// <summary>
    /// Returns the state entry for the given <see cref="NpcAnimationState"/> enum value,
    /// or null if not catalogued.
    /// </summary>
    public StateVisualEntry GetState(NpcAnimationState state) =>
        GetState(state.ToString());

    /// <summary>Returns the cue entry for <paramref name="cueId"/>, or null if not catalogued.</summary>
    public CueVisualEntry GetCue(string cueId) =>
        _byCueId.TryGetValue(cueId, out var e) ? e : null;

    /// <summary>Returns the transition entry for a (from, to) pair, or null if not catalogued.</summary>
    public TransitionVisualEntry GetTransition(NpcAnimationState from, NpcAnimationState to) =>
        _byTransition.TryGetValue(
            (from.ToString().ToLowerInvariant(), to.ToString().ToLowerInvariant()),
            out var t) ? t : null;

    /// <summary>
    /// Returns the frame duration for <paramref name="state"/> in milliseconds.
    /// Falls back to 200ms if the state is not in the catalog.
    /// </summary>
    public int GetFrameDurationMs(NpcAnimationState state)
    {
        var entry = GetState(state);
        return entry?.FrameDurationMs ?? 200;
    }

    /// <summary>
    /// Returns the Animator speed multiplier for <paramref name="state"/> relative to
    /// a reference frame duration of 200ms (the Idle default).
    /// </summary>
    public float GetAnimatorSpeed(NpcAnimationState state)
    {
        const float ReferenceFrameMs = 200f;
        int durationMs = GetFrameDurationMs(state);
        if (durationMs <= 0) return 1f;
        return ReferenceFrameMs / durationMs;
    }

    /// <summary>
    /// Computes the chibi cue alpha for a given altitude and cue.
    /// Returns 1.0 below fadeAltitudeStart, 0.0 above fadeAltitudeEnd, linear between.
    /// </summary>
    public static float ComputeCueAlpha(float altitude, float fadeAltitudeStart, float fadeAltitudeEnd)
    {
        if (altitude <= fadeAltitudeStart) return 1f;
        if (altitude >= fadeAltitudeEnd)   return 0f;
        float t = (altitude - fadeAltitudeStart) / (fadeAltitudeEnd - fadeAltitudeStart);
        return 1f - t;
    }
}

// ── JSON loader ───────────────────────────────────────────────────────────────

/// <summary>
/// Pure-C# loader for <see cref="NpcVisualStateCatalog"/>. Testable without Unity.
/// </summary>
public static class NpcVisualStateCatalogLoader
{
    private const string CatalogRelativePath = "docs/c2-content/animation/visual-state-catalog.json";

    /// <summary>
    /// Walks up from the running assembly's directory to find
    /// <c>docs/c2-content/animation/visual-state-catalog.json</c>.
    /// Returns null if not found.
    /// </summary>
    public static string FindDefaultPath()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int depth = 0; depth < 8; depth++)
        {
            var candidate = Path.Combine(dir, CatalogRelativePath);
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return null;
    }

    /// <summary>
    /// Loads <see cref="NpcVisualStateCatalog"/> from <paramref name="jsonPath"/>.
    /// Returns an empty catalog on missing file or parse error.
    /// </summary>
    public static NpcVisualStateCatalog Load(string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
            return Empty;

        try
        {
            var text = File.ReadAllText(jsonPath);
            return ParseJson(text);
        }
        catch
        {
            return Empty;
        }
    }

    /// <summary>Parses a catalog from a JSON string. Returns an empty catalog on error.</summary>
    public static NpcVisualStateCatalog ParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Empty;
        try
        {
            var dto = JsonConvert.DeserializeObject<CatalogDto>(json);
            if (dto == null) return Empty;
            return FromDto(dto);
        }
        catch
        {
            return Empty;
        }
    }

    /// <summary>An empty catalog with no entries — graceful fallback when the file is absent.</summary>
    public static NpcVisualStateCatalog Empty { get; } =
        new NpcVisualStateCatalog("0.0.0",
            Array.Empty<StateVisualEntry>(),
            Array.Empty<CueVisualEntry>(),
            Array.Empty<TransitionVisualEntry>());

    // ── DTO → catalog conversion ──────────────────────────────────────────────

    private static NpcVisualStateCatalog FromDto(CatalogDto dto)
    {
        var states = new List<StateVisualEntry>();
        if (dto.states != null)
        {
            foreach (var s in dto.states)
            {
                if (string.IsNullOrWhiteSpace(s.stateId)) continue;
                states.Add(new StateVisualEntry(
                    s.stateId,
                    s.frameDurationMs > 0 ? s.frameDurationMs : 200,
                    s.accentColor ?? "#a0a0a0",
                    s.cueAffinity ?? Array.Empty<string>()));
            }
        }

        var cues = new List<CueVisualEntry>();
        if (dto.cues != null)
        {
            foreach (var c in dto.cues)
            {
                if (string.IsNullOrWhiteSpace(c.cueId)) continue;
                cues.Add(new CueVisualEntry(
                    c.cueId,
                    c.iconKind ?? c.cueId,
                    c.spriteAsset ?? string.Empty,
                    c.anchorOffset,
                    c.fadeAltitudeStart,
                    c.fadeAltitudeEnd > c.fadeAltitudeStart ? c.fadeAltitudeEnd : c.fadeAltitudeStart + 10f,
                    c.minScaleMult > 0f ? c.minScaleMult : 1f));
            }
        }

        var transitions = new List<TransitionVisualEntry>();
        if (dto.transitions != null)
        {
            foreach (var t in dto.transitions)
            {
                if (string.IsNullOrWhiteSpace(t.from) || string.IsNullOrWhiteSpace(t.to)) continue;
                transitions.Add(new TransitionVisualEntry(
                    t.from,
                    t.to,
                    t.intermediateFrames ?? Array.Empty<int>(),
                    t.totalDurationMs > 0 ? t.totalDurationMs : 200));
            }
        }

        return new NpcVisualStateCatalog(dto.schemaVersion ?? "0.0.0", states, cues, transitions);
    }

    // ── JSON DTOs ─────────────────────────────────────────────────────────────

    private sealed class CatalogDto
    {
        public string        schemaVersion { get; set; }
        public StateDtoRow[] states        { get; set; }
        public CueDtoRow[]   cues          { get; set; }
        public TransDtoRow[] transitions   { get; set; }
    }

    private sealed class StateDtoRow
    {
        public string   stateId        { get; set; }
        public int      frameDurationMs { get; set; }
        public string   accentColor    { get; set; }
        public string[] cueAffinity    { get; set; }
        public string   description    { get; set; }
    }

    private sealed class CueDtoRow
    {
        public string  cueId             { get; set; }
        public string  iconKind          { get; set; }
        public string  spriteAsset       { get; set; }
        public float[] anchorOffset      { get; set; }
        public float   fadeAltitudeStart { get; set; }
        public float   fadeAltitudeEnd   { get; set; }
        public float   minScaleMult      { get; set; }
        public string  description       { get; set; }
    }

    private sealed class TransDtoRow
    {
        public string  from               { get; set; }
        public string  to                 { get; set; }
        public int[]   intermediateFrames { get; set; }
        public int     totalDurationMs    { get; set; }
        public string  description        { get; set; }
    }
}
