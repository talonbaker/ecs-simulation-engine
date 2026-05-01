using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Warden.Contracts.Telemetry;
using Warden.Orchestrator.Cache;
using Warden.Telemetry.AsciiMap;

namespace Warden.Orchestrator.Prompts;

#if WARDEN

/// <summary>
/// Renders a <see cref="WorldStateDto"/> into a (Stable, Volatile) pair of prompt slabs
/// per WP-3.0.W.1's two-tier cache strategy.
///
/// Stable  — walls, doors, room shading, fixed furniture. Cached (Ephemeral5m for Sonnet,
///           Ephemeral1h for Haiku batches). Does not include NPCs or hazards.
/// Volatile — NPC positions + drive labels, transient hazards. Always Uncached.
///            Default mode: delta list only (no full map re-render, saves tokens).
///            Full mode: full re-render when AsciiMapOptions.IncludeStableInVolatile = true.
/// </summary>
public static class MapSlabFactory
{
    private const int TokenWarningThreshold = 16_000; // ~4 000 tokens at 4 chars/token

    /// <summary>
    /// Renders the world state into a (Stable, Volatile) pair of prompt slabs.
    /// </summary>
    /// <param name="state">Current world snapshot.</param>
    /// <param name="options">
    /// Render options forwarded to <see cref="AsciiMapProjector"/>. The factory
    /// overrides <c>ShowNpcs</c> and <c>ShowHazards</c> for the stable layer.
    /// Set <c>IncludeStableInVolatile = true</c> for a full re-render in volatile.
    /// </param>
    /// <param name="isHaikuBatch">
    /// When <c>true</c>, <c>Stable.Cache</c> is <see cref="CacheDisposition.Ephemeral1h"/>
    /// (Haiku batch window). When <c>false</c>, <see cref="CacheDisposition.Ephemeral5m"/>
    /// (Sonnet authoring). <c>Volatile.Cache</c> is always <see cref="CacheDisposition.Uncached"/>.
    /// </param>
    public static (MapSlab Stable, MapSlab Volatile) Build(
        WorldStateDto   state,
        AsciiMapOptions options      = default,
        bool            isHaikuBatch = false)
    {
        var stableCache = isHaikuBatch ? CacheDisposition.Ephemeral1h : CacheDisposition.Ephemeral5m;

        // Stable: suppress NPCs and hazards so only structure + furniture render.
        var stableOpts = options with { ShowNpcs = false, ShowHazards = false, ShowFurniture = true, IncludeLegend = true };
        var rawStable  = AsciiMapProjector.Render(state, stableOpts);
        var stableText = ReplaceHeader(rawStable, "=== WORLD MAP — STABLE ===");

        if (stableText.Length > TokenWarningThreshold)
            Console.Error.WriteLine(
                $"[MapSlabFactory WARNING] Stable map is ~{stableText.Length / 4} tokens " +
                $"({stableText.Length} chars). Map may exceed the 4 000-token budget.");

        // Volatile: delta list or full re-render.
        string volatileText;
        if (options.IncludeStableInVolatile)
        {
            var fullRender = AsciiMapProjector.Render(state, options with { IncludeLegend = true });
            volatileText   = ReplaceHeader(fullRender, BuildTickHeader(state));
        }
        else
        {
            volatileText = BuildVolatileDelta(state);
        }

        return (new MapSlab(stableText, stableCache),
                new MapSlab(volatileText, CacheDisposition.Uncached));
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>Strips the first line of <paramref name="rendered"/> and replaces it.</summary>
    private static string ReplaceHeader(string rendered, string header)
    {
        var idx  = rendered.IndexOf('\n');
        var body = idx >= 0 ? rendered[(idx + 1)..] : rendered;
        return header + "\n" + body;
    }

    private static string BuildTickHeader(WorldStateDto state)
    {
        var time = state.Clock?.GameTimeDisplay ?? "00:00";
        return $"=== WORLD MAP — TICK {state.Tick} ({time}) ===";
    }

    /// <summary>
    /// Builds the volatile delta: tick header + per-NPC position + drive + hazard list.
    /// Does NOT re-render the ASCII grid (saves tokens; models read NPCs against the
    /// cached stable layer).
    /// </summary>
    private static string BuildVolatileDelta(WorldStateDto state)
    {
        var sb = new StringBuilder();
        sb.AppendLine(BuildTickHeader(state));
        sb.AppendLine();

        var entities = (state.Entities ?? new List<EntityStateDto>())
            .Where(e => e.Position.HasPosition)
            .OrderBy(e => e.Name);

        foreach (var e in entities)
        {
            var glyph  = string.IsNullOrEmpty(e.Name) ? '?' : char.ToLowerInvariant(e.Name[0]);
            var tx     = (int)Math.Floor(e.Position.X);
            var ty     = (int)Math.Floor(e.Position.Y);
            sb.AppendLine($"  {glyph} ({tx}, {ty}) — {DriveLabel(e.Drives.Dominant)}");
        }

        foreach (var obj in (state.WorldObjects ?? new List<WorldObjectDto>()).OrderBy(o => o.Id))
        {
            if (!IsHazard(obj, out char hg)) continue;
            var tx = (int)Math.Floor(obj.X);
            var ty = (int)Math.Floor(obj.Y);
            sb.AppendLine($"  {hg} {obj.Name} ({tx}, {ty})");
        }

        sb.AppendLine();
        sb.Append("(NPCs and hazards overlaid on the stable map above; ASCII layer omitted to save tokens.)");
        return sb.ToString();
    }

    private static bool IsHazard(WorldObjectDto obj, out char glyph)
    {
        if (obj.Kind != WorldObjectKind.Other) { glyph = ' '; return false; }
        var n = obj.Name.ToLowerInvariant();
        if (n.Contains("fire"))                         { glyph = '!'; return true; }
        if (n.Contains("stain"))                        { glyph = '*'; return true; }
        if (n.Contains("water") || n.Contains("spill")) { glyph = '~'; return true; }
        if (n.Contains("corpse") || n.Contains("body")) { glyph = 'x'; return true; }
        if (n.Contains("hazard"))                       { glyph = '?'; return true; }
        glyph = ' '; return false;
    }

    private static string DriveLabel(DominantDrive d) => d switch
    {
        DominantDrive.Eat      => "Eating",
        DominantDrive.Drink    => "Drinking",
        DominantDrive.Sleep    => "Sleeping",
        DominantDrive.Defecate => "Defecating",
        DominantDrive.Pee      => "Pee",
        _                      => "Idle",
    };
}

#endif
