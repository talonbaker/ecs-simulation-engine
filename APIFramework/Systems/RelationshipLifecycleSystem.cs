using System;
using System.Collections.Generic;
using System.IO;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.LifeState;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace APIFramework.Systems;

/// <summary>
/// Per-tick lifecycle management for relationship entities.
///
/// This packet implements two things:
///   1. Intensity decay — open-loop; every relationship loses Intensity at
///      RelationshipIntensityDecayPerTick until proximity events ship.
///   2. Pattern transition skeleton — the transition table is loaded at boot;
///      the system iterates each relationship each tick but does NOT fire any
///      transition (trigger conditions need memory + proximity, deferred).
///      Tests verify the table loads and no transitions fire.
///
/// Phase: Cognition.
/// </summary>
/// <remarks>
/// Reads: <see cref="RelationshipTag"/>, <see cref="RelationshipComponent"/>,
/// <see cref="LifeStateComponent"/>.<br/>
/// Writes: <see cref="RelationshipComponent"/>.Intensity (decay).<br/>
/// Phase: Cognition.
/// </remarks>
public class RelationshipLifecycleSystem : ISystem
{
    private readonly SocialSystemConfig _cfg;

    // Each entry is (from, to) — a legal pattern transition.
    private readonly IReadOnlyList<(RelationshipPattern From, RelationshipPattern To)> _transitions;

    // Accumulated fractional decay; applied to integer Intensity when it crosses 1.
    private readonly Dictionary<Guid, double> _decayAccum = new();

    /// <summary>Constructs the lifecycle system with its config and a pre-loaded transition table.</summary>
    /// <param name="cfg">Social-system tuning (intensity decay rate).</param>
    /// <param name="transitions">Pre-loaded list of legal (from, to) pattern transitions.</param>
    public RelationshipLifecycleSystem(
        SocialSystemConfig cfg,
        IReadOnlyList<(RelationshipPattern, RelationshipPattern)> transitions)
    {
        _cfg         = cfg;
        _transitions = transitions;
    }

    /// <summary>Per-tick relationship pass; decays intensity and walks the transition table.</summary>
    /// <param name="em">Entity manager backing this tick.</param>
    /// <param name="deltaTime">Elapsed game time for this tick (seconds, unused).</param>
    public void Update(EntityManager em, float deltaTime)
    {
        foreach (var entity in em.Query<RelationshipTag>().ToList())
        {
            if (!LifeStateGuard.IsAlive(entity)) continue;  // WP-3.0.0: skip non-Alive NPCs
            if (!entity.Has<RelationshipComponent>()) continue;

            var rel = entity.Get<RelationshipComponent>();

            // 1. Intensity decay (open-loop until proximity ships)
            if (!_decayAccum.TryGetValue(entity.Id, out var accum))
                accum = 0.0;

            accum += _cfg.RelationshipIntensityDecayPerTick;
            int intDecay = (int)accum;
            accum -= intDecay;
            _decayAccum[entity.Id] = accum;

            if (intDecay > 0)
            {
                rel.Intensity = Math.Clamp(rel.Intensity - intDecay, 0, 100);
                entity.Add(rel);
            }

            // 2. Pattern transition skeleton — iterate table, fire nothing
            //    (trigger conditions deferred; this verifies table loads and iterates cleanly)
            foreach (var (from, to) in _transitions)
            {
                // Future: check memory + proximity conditions here.
                // Currently no trigger conditions exist, so no transition fires.
                _ = from;
                _ = to;
            }
        }
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the transition table from <paramref name="tablePath"/>.
    /// Searches up to 6 parent directories if the file is not found at the given path.
    /// Returns a system with an empty table (no transitions) if the file is not found.
    /// </summary>
    public static RelationshipLifecycleSystem LoadFromFile(
        SocialSystemConfig cfg,
        string tablePath = "APIFramework/Data/RelationshipTransitionTable.json")
    {
        var path = FindFile(tablePath);
        if (path is null)
        {
            Console.WriteLine($"[RelationshipLifecycleSystem] Transition table '{tablePath}' not found — using empty table.");
            return new RelationshipLifecycleSystem(cfg, Array.Empty<(RelationshipPattern, RelationshipPattern)>());
        }

        try
        {
            var json       = File.ReadAllText(path);
            var root       = JObject.Parse(json);
            var arr        = root["transitions"] as JArray;
            var transitions = new List<(RelationshipPattern, RelationshipPattern)>();

            if (arr != null)
            {
                foreach (var item in arr)
                {
                    var fromStr = item["from"]?.Value<string>();
                    var toStr   = item["to"]?.Value<string>();
                    if (fromStr is null || toStr is null) continue;

                    if (Enum.TryParse<RelationshipPattern>(fromStr, out var fromPat) &&
                        Enum.TryParse<RelationshipPattern>(toStr,   out var toPat))
                    {
                        transitions.Add((fromPat, toPat));
                    }
                }
            }

            Console.WriteLine($"[RelationshipLifecycleSystem] Loaded {transitions.Count} transitions from '{path}'.");
            return new RelationshipLifecycleSystem(cfg, transitions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RelationshipLifecycleSystem] Failed to parse '{path}': {ex.Message} — using empty table.");
            return new RelationshipLifecycleSystem(cfg, Array.Empty<(RelationshipPattern, RelationshipPattern)>());
        }
    }

    /// <summary>Number of transitions currently loaded (used in acceptance tests).</summary>
    public int TransitionCount => _transitions.Count;

    private static string? FindFile(string relativePath)
    {
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        return null;
    }
}
