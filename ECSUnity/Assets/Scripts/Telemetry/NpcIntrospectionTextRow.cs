#if WARDEN
using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using UnityEngine;

/// <summary>
/// Holds the formatted display strings and screen-space anchor for a single NPC's
/// introspection overlay. Populated by <see cref="NpcIntrospectionOverlay"/> at up to
/// 4 Hz; drawn every frame via IMGUI in <see cref="Draw"/>.
/// </summary>
public sealed class NpcIntrospectionTextRow
{
    // ── Tracking ──────────────────────────────────────────────────────────────

    /// <summary>ECS entity GUID string that owns this row.</summary>
    public string EntityId;

    /// <summary>GUI-space screen position (y = 0 at top, matching OnGUI convention).</summary>
    public Vector2 ScreenPos;

    /// <summary>True when the NPC's world position projects behind the camera; row is not drawn.</summary>
    public bool BehindCamera;

    // ── Formatted lines (rebuilt by Refresh) ─────────────────────────────────

    private string _glanceLine = string.Empty;  // "Donna  [Alive]  Working"
    private string _actionLine = string.Empty;  // "Next: Continue working"
    private string _drivesLine = string.Empty;  // "hunger 0.74 | irritation 0.55 | pee 0.42"
    private string _statsLine  = string.Empty;  // "stress 62  willpower 8/10"
    private string _whyLine    = string.Empty;  // "working (scheduled)"
    private Color  _nameColor  = Color.green;

    // ── IMGUI layout constants ─────────────────────────────────────────────────

    private const float RowW  = 230f;
    private const float LineH = 15f;
    private const float PadX  = 5f;
    private const float PadY  = 4f;
    private const float BoxH  = LineH * 5f + PadY * 2f;

    private static GUIStyle _bgStyle;
    private static GUIStyle _boldStyle;
    private static GUIStyle _bodyStyle;

    // ── Content refresh ────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds all display strings from current ECS component state.
    /// Called at the overlay's throttled rate, not every frame.
    /// </summary>
    public void Refresh(Entity entity, EngineHost host)
    {
        // ── Identity ──────────────────────────────────────────────────────────
        string name = entity.Has<IdentityComponent>()
            ? (entity.Get<IdentityComponent>().Name ?? EntityId)
            : EntityId;

        // ── Life state ────────────────────────────────────────────────────────
        LifeState lifeState = LifeState.Alive;
        if (entity.Has<LifeStateComponent>())
            lifeState = entity.Get<LifeStateComponent>().State;

        string lifeBadge;
        switch (lifeState)
        {
            case LifeState.Alive:
                lifeBadge  = "[Alive]";
                _nameColor = new Color(0.2f, 0.9f, 0.3f);
                break;
            case LifeState.Incapacitated:
                lifeBadge  = "[Incapacitated]";
                _nameColor = new Color(1f, 0.85f, 0.1f);
                break;
            default:
                lifeBadge  = "[?]";
                _nameColor = Color.white;
                break;
        }

        // ── Intended action ───────────────────────────────────────────────────
        IntendedActionKind actionKind = IntendedActionKind.Idle;
        int targetId = 0;
        if (entity.Has<IntendedActionComponent>())
        {
            var ia = entity.Get<IntendedActionComponent>();
            actionKind = ia.Kind;
            targetId   = ia.TargetEntityId;
        }

        _glanceLine = $"{name}  {lifeBadge}  {DescribeActivity(actionKind)}";
        _actionLine = $"Next: {DescribeNextAction(actionKind, targetId, host)}";

        // ── Top-3 drives ──────────────────────────────────────────────────────
        _drivesLine = BuildDrivesLine(entity);

        // ── Stress / Willpower ────────────────────────────────────────────────
        int acuteStress = 0;
        if (entity.Has<StressComponent>())
            acuteStress = entity.Get<StressComponent>().AcuteLevel;

        int wpCurrent = 0, wpBaseline = 100;
        if (entity.Has<WillpowerComponent>())
        {
            var wp = entity.Get<WillpowerComponent>();
            wpCurrent  = wp.Current;
            wpBaseline = wp.Baseline;
        }

        _statsLine = $"stress {acuteStress}  willpower {wpCurrent}/{wpBaseline}";

        // ── "Why" heuristic ───────────────────────────────────────────────────
        _whyLine = BuildWhyLine(actionKind, targetId, host);
    }

    // ── IMGUI draw ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Draws this row's overlay panel. Must be called from OnGUI.
    /// </summary>
    public void Draw()
    {
        EnsureStyles();

        float x = ScreenPos.x - RowW * 0.5f;
        float y = ScreenPos.y;

        x = Mathf.Clamp(x, 2f, Screen.width  - RowW  - 2f);
        y = Mathf.Clamp(y, 2f, Screen.height - BoxH  - 2f);

        GUI.Box(new Rect(x - PadX, y - PadY, RowW + PadX * 2f, BoxH + PadY * 2f),
                GUIContent.none, _bgStyle);

        // Glance row: bold + life-state colour.
        Color prev = GUI.color;
        GUI.color = _nameColor;
        GUI.Label(new Rect(x, y, RowW, LineH), _glanceLine, _boldStyle);
        GUI.color = prev;
        y += LineH;

        // Detail rows.
        GUI.Label(new Rect(x, y, RowW, LineH), _actionLine, _bodyStyle); y += LineH;
        GUI.Label(new Rect(x, y, RowW, LineH), _drivesLine, _bodyStyle); y += LineH;
        GUI.Label(new Rect(x, y, RowW, LineH), _statsLine,  _bodyStyle); y += LineH;
        GUI.Label(new Rect(x, y, RowW, LineH), _whyLine,    _bodyStyle);
    }

    // ── Static content helpers ─────────────────────────────────────────────────

    private static string DescribeActivity(IntendedActionKind kind)
    {
        return kind switch
        {
            IntendedActionKind.Idle      => "Idle",
            IntendedActionKind.Dialog    => "Talking",
            IntendedActionKind.Approach  => "Approaching",
            IntendedActionKind.Avoid     => "Avoiding",
            IntendedActionKind.Linger    => "Lingering",
            IntendedActionKind.Work      => "Working",
            IntendedActionKind.Rescue    => "Rescuing",
            IntendedActionKind.ChoreWork => "Doing chore",
            IntendedActionKind.Eat       => "Eating",
            IntendedActionKind.Drink     => "Drinking",
            IntendedActionKind.Defecate  => "Using bathroom",
            _                            => "—",
        };
    }

    private static string DescribeNextAction(IntendedActionKind kind, int targetId, EngineHost host)
    {
        return kind switch
        {
            IntendedActionKind.Idle      => "(idle)",
            IntendedActionKind.Dialog    => "Engage in dialog",
            IntendedActionKind.Approach  => TryResolveName(targetId, host, "Move toward target"),
            IntendedActionKind.Avoid     => TryResolveName(targetId, host, "Move away from target"),
            IntendedActionKind.Linger    => "Stay near target",
            IntendedActionKind.Work      => "Continue working",
            IntendedActionKind.Rescue    => TryResolveName(targetId, host, "Rescue target"),
            IntendedActionKind.ChoreWork => "Continue chore",
            IntendedActionKind.Eat       => "Continue eating",
            IntendedActionKind.Drink     => "Continue drinking",
            IntendedActionKind.Defecate  => "Relieve themselves",
            _                            => "—",
        };
    }

    private static string BuildDrivesLine(Entity entity)
    {
        var entries = new List<(string label, float value)>(13);

        if (entity.Has<DriveComponent>())
        {
            var d = entity.Get<DriveComponent>();
            if (d.EatUrgency      > 0.01f) entries.Add(("hunger",   d.EatUrgency));
            if (d.DrinkUrgency    > 0.01f) entries.Add(("thirst",   d.DrinkUrgency));
            if (d.SleepUrgency    > 0.01f) entries.Add(("fatigue",  d.SleepUrgency));
            if (d.DefecateUrgency > 0.01f) entries.Add(("bowel",    d.DefecateUrgency));
            if (d.PeeUrgency      > 0.01f) entries.Add(("bladder",  d.PeeUrgency));
        }

        if (entity.Has<SocialDrivesComponent>())
        {
            const float Scale = 1f / 100f;
            var s = entity.Get<SocialDrivesComponent>();
            if (s.Irritation.Current > 1) entries.Add(("irritation", s.Irritation.Current * Scale));
            if (s.Affection.Current  > 1) entries.Add(("affection",  s.Affection.Current  * Scale));
            if (s.Suspicion.Current  > 1) entries.Add(("suspicion",  s.Suspicion.Current  * Scale));
            if (s.Loneliness.Current > 1) entries.Add(("loneliness", s.Loneliness.Current * Scale));
            if (s.Trust.Current      > 1) entries.Add(("trust",      s.Trust.Current      * Scale));
            if (s.Belonging.Current  > 1) entries.Add(("belonging",  s.Belonging.Current  * Scale));
            if (s.Status.Current     > 1) entries.Add(("status",     s.Status.Current     * Scale));
            if (s.Attraction.Current > 1) entries.Add(("attraction", s.Attraction.Current * Scale));
        }

        if (entries.Count == 0) return "drives: (none active)";

        entries.Sort((a, b) => b.value.CompareTo(a.value));

        int take = Math.Min(3, entries.Count);
        var parts = new string[take];
        for (int i = 0; i < take; i++)
            parts[i] = $"{entries[i].label} {entries[i].value:F2}";

        return string.Join(" | ", parts);
    }

    private static string BuildWhyLine(IntendedActionKind kind, int targetId, EngineHost host)
    {
        return kind switch
        {
            IntendedActionKind.Approach  => TryResolveName(targetId, host, "moving toward target"),
            IntendedActionKind.Avoid     => "avoiding someone",
            IntendedActionKind.Work      => "working (scheduled)",
            IntendedActionKind.ChoreWork => "doing chore",
            IntendedActionKind.Eat       => "hungry; eating",
            IntendedActionKind.Drink     => "thirsty; drinking",
            IntendedActionKind.Defecate  => "needed bathroom",
            IntendedActionKind.Dialog    => "social interaction",
            IntendedActionKind.Rescue    => "rescuing incapacitated NPC",
            IntendedActionKind.Linger    => "lingering near target",
            IntendedActionKind.Idle      => "—",
            _                            => "—",
        };
    }

    /// <summary>
    /// Best-effort target-name lookup using the lower-32-bit entity-id convention
    /// from <see cref="IntendedActionComponent.TargetEntityId"/>. Falls back to
    /// <paramref name="fallback"/> on no match.
    /// </summary>
    private static string TryResolveName(int targetId, EngineHost host, string fallback)
    {
        if (targetId == 0 || host?.Engine?.Entities == null) return fallback;

        foreach (var e in host.Engine.Entities)
        {
            var bytes  = e.Id.ToByteArray();
            int lower  = BitConverter.ToInt32(bytes, 0);
            if (lower == targetId && e.Has<IdentityComponent>())
                return $"→ {e.Get<IdentityComponent>().Name}";
        }

        return fallback;
    }

    // ── IMGUI style helpers ────────────────────────────────────────────────────

    private static void EnsureStyles()
    {
        if (_bgStyle != null) return;

        var bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.10f, 0.88f));
        bgTex.Apply();

        _bgStyle = new GUIStyle(GUI.skin.box);
        _bgStyle.normal.background = bgTex;
        _bgStyle.border = new RectOffset(2, 2, 2, 2);

        _boldStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize   = 11,
            fontStyle  = FontStyle.Bold,
            wordWrap   = false,
            clipping   = TextClipping.Clip,
        };
        _boldStyle.normal.textColor = Color.white;

        _bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize   = 10,
            fontStyle  = FontStyle.Normal,
            wordWrap   = false,
            clipping   = TextClipping.Clip,
        };
        _bodyStyle.normal.textColor = new Color(0.82f, 0.82f, 0.82f);
    }
}
#endif
