using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;
using APIFramework.Systems.Narrative;
using ECSCli.Ai;
using Warden.Contracts;
using Xunit;

namespace ECSCli.Tests;

/// <summary>
/// Integration tests for <c>AiNarrativeStreamCommand</c>.
/// AT-10: valid JSONL output — each line parses as a NarrativeEventCandidate.
/// AT-11: ≥ 1 candidate of DriveSpike, WillpowerLow, and ConversationStarted
///        within 600 game-seconds.
/// </summary>
public sealed class AiNarrativeStreamCommandTests
{
    // -- Helpers ---------------------------------------------------------------

    private static SimulationBootstrapper BuildSim(
        int driveSpikeThreshold = 1,
        int humanCount          = 2,
        int seed                = 0)
    {
        var cfg = new SimConfig();
        cfg.Narrative.DriveSpikeThreshold    = driveSpikeThreshold;
        cfg.Narrative.WillpowerLowThreshold  = 20;
        cfg.Narrative.WillpowerDropThreshold = 1;

        return new SimulationBootstrapper(
            new InMemoryConfigProvider(cfg), humanCount: humanCount, seed: seed);
    }

    private static void AddSocialAndProximity(
        SimulationBootstrapper sim,
        int                    willpowerCurrent = 50)
    {
        foreach (var e in sim.EntityManager.Query<HumanTag>().ToList())
        {
            EntityTemplates.WithSocial(
                e, willpower: new WillpowerComponent(willpowerCurrent, 50));
            EntityTemplates.WithProximity(e);
        }
    }

    // -- AT-10: valid JSONL format ---------------------------------------------

    [Fact]
    public void RunCore_WritesValidJsonlWithRequiredFields()
    {
        const double GameDuration = 10.0;

        var sim = BuildSim(driveSpikeThreshold: 1, humanCount: 2, seed: 0);

        // Place both humans at the same location so ConversationStarted fires on tick 1
        foreach (var e in sim.EntityManager.Query<HumanTag>().ToList())
        {
            var pos = e.Get<PositionComponent>();
            pos.X = 5f;
            pos.Z = 5f;
            e.Add(pos);
        }

        AddSocialAndProximity(sim);

        using var writer = new StringWriter();
        AiNarrativeStreamCommand.RunCore(sim, writer, duration: GameDuration);

        var lines = writer.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        Assert.True(lines.Length > 0, "Expected at least one JSONL line.");

        foreach (var line in lines)
        {
            using var doc  = JsonDocument.Parse(line);
            var        root = doc.RootElement;

            Assert.True(root.TryGetProperty("tick",           out _), $"Missing 'tick' in: {line}");
            Assert.True(root.TryGetProperty("kind",           out _), $"Missing 'kind' in: {line}");
            Assert.True(root.TryGetProperty("participantIds", out _), $"Missing 'participantIds' in: {line}");
            Assert.True(root.TryGetProperty("detail",         out _), $"Missing 'detail' in: {line}");
        }
    }

    // -- AT-11: all three common kinds in 600 game-seconds --------------------

    [Fact]
    public void RunCore_ProducesAllThreeKindsIn600GameSeconds()
    {
        const double GameDuration = 600.0;
        const float  DeltaTime    = 1f / 60f;

        // DriveSpikeThreshold=1 → drives spike on any 1-point change (fires quickly)
        // WillpowerLowThreshold=20 → starting willpower=25; one suppression drops it to 15 (<20)
        var sim = BuildSim(driveSpikeThreshold: 1, humanCount: 10, seed: 0);
        AddSocialAndProximity(sim, willpowerCurrent: 25);

        // Prime: run one tick so the detector caches current drive/willpower values.
        // WillpowerSystem runs before NarrativeEventDetector, so this tick the detector
        // primes _prevWillpower with willpower=25 (queue is still empty at this point).
        sim.Engine.Update(DeltaTime);

        // Inject one SuppressionTick per NPC after the prime, before tick 2.
        // WillpowerSystem will apply this on tick 2 (25 → 15), crossing the threshold.
        foreach (var npc in sim.EntityManager.Query<NpcTag>().ToList())
        {
            sim.WillpowerEvents.Enqueue(new WillpowerEventSignal(
                NarrativeEventDetector.EntityIntId(npc),
                WillpowerEventKind.SuppressionTick,
                Magnitude: 10));
        }

        var candidates = new List<NarrativeEventCandidate>();
        sim.NarrativeBus.OnCandidateEmitted += candidates.Add;

        // RunCore drives the remaining ticks until TotalTime >= 600
        using var discard = new StringWriter();
        AiNarrativeStreamCommand.RunCore(sim, discard, duration: GameDuration);

        Assert.Contains(candidates, c => c.Kind == NarrativeEventKind.DriveSpike);
        Assert.Contains(candidates, c => c.Kind == NarrativeEventKind.WillpowerLow);
        Assert.Contains(candidates, c => c.Kind == NarrativeEventKind.ConversationStarted);
    }
}
