using System;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using Warden.Contracts;
using Warden.Contracts.SchemaValidation;
using Warden.Contracts.Telemetry;
using Warden.Telemetry;
using Xunit;

namespace Warden.Telemetry.Tests;

/// <summary>
/// Acceptance tests for <see cref="TelemetryProjector"/>:
///
/// AT-01 — Project on a fresh sim produces JSON that validates against world-state.schema.json.
/// AT-02 — Determinism: identical inputs → byte-identical JSON.
/// AT-03 — Species resolves correctly for a human and a cat spawned via EntityTemplates.
/// </summary>
public class TelemetryProjectorTests
{
    // ── Shared fixture helpers ────────────────────────────────────────────────

    /// <summary>
    /// Creates a minimal single-human sim (no config file needed) so tests are
    /// hermetic and fast.
    /// </summary>
    private static SimulationBootstrapper MakeSim(int humanCount = 1)
        => new(new InMemoryConfigProvider(new SimConfig()), humanCount);

    private static readonly DateTimeOffset FixedCapture =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static WorldStateDto Capture(
        SimulationBootstrapper sim,
        long           tick       = 0,
        int            seed       = 42,
        string         simVersion = "test-0.0.1")
    {
        var snap = sim.Capture();
        return TelemetryProjector.Project(
            snap, sim.EntityManager, FixedCapture, tick, seed, simVersion);
    }

    // ── AT-01 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AT-01: Project on a fresh SimulationBootstrapper produces JSON that
    /// validates against world-state.schema.json.
    /// </summary>
    [Fact]
    public void AT01_FreshSim_ProjectValidatesAgainstSchema()
    {
        var sim  = MakeSim();
        var dto  = Capture(sim);
        var json = TelemetrySerializer.SerializeSnapshot(dto);

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.True(result.IsValid,
            $"world-state schema validation failed: {string.Join("; ", result.Errors)}\n\nJSON:\n{json}");
    }

    /// <summary>
    /// AT-01 variant: zero-human sim (world objects only) also validates.
    /// </summary>
    [Fact]
    public void AT01_ZeroHumanSim_ProjectValidatesAgainstSchema()
    {
        var sim  = MakeSim(humanCount: 0);
        var dto  = Capture(sim);
        var json = TelemetrySerializer.SerializeSnapshot(dto);

        var result = SchemaValidator.Validate(json, Schema.WorldState);

        Assert.True(result.IsValid,
            $"world-state schema validation failed (0 humans): {string.Join("; ", result.Errors)}");
    }

    // ── AT-02 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AT-02: Two Project calls with identical (snap, tick, seed, simVersion, capturedAt)
    /// produce byte-identical JSON.
    /// </summary>
    [Fact]
    public void AT02_SameInputs_ProduceBytIdenticalJson()
    {
        var sim   = MakeSim();
        var snap  = sim.Capture();     // one snapshot, used twice

        var dto1  = TelemetryProjector.Project(snap, sim.EntityManager, FixedCapture, 7L, 99, "v1");
        var dto2  = TelemetryProjector.Project(snap, sim.EntityManager, FixedCapture, 7L, 99, "v1");

        var json1 = TelemetrySerializer.SerializeSnapshot(dto1);
        var json2 = TelemetrySerializer.SerializeSnapshot(dto2);

        Assert.Equal(json1, json2);
    }

    /// <summary>
    /// AT-02 variant: different tick values produce different JSON (proves tick is wired in).
    /// </summary>
    [Fact]
    public void AT02_DifferentTick_ProducesDifferentJson()
    {
        var sim  = MakeSim();
        var snap = sim.Capture();

        var json1 = TelemetrySerializer.SerializeSnapshot(
            TelemetryProjector.Project(snap, sim.EntityManager, FixedCapture, 0L, 1, "v1"));
        var json2 = TelemetrySerializer.SerializeSnapshot(
            TelemetryProjector.Project(snap, sim.EntityManager, FixedCapture, 1L, 1, "v1"));

        Assert.NotEqual(json1, json2);
    }

    // ── AT-03 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AT-03: Entity species resolves correctly for a human and a cat spawned
    /// via EntityTemplates.
    /// </summary>
    [Fact]
    public void AT03_HumanAndCat_SpeciesResolvesCorrectly()
    {
        // Boot a zero-human sim then manually spawn one human and one cat.
        var sim = MakeSim(humanCount: 0);

        var humanEntity = EntityTemplates.SpawnHuman(sim.EntityManager);
        var catEntity   = EntityTemplates.SpawnCat(sim.EntityManager);

        var snap = sim.Capture();
        var dto  = TelemetryProjector.Project(
            snap, sim.EntityManager, FixedCapture, 0L, 0, "test");

        // Find the projected entities by their IDs (First throws if not found — test fail).
        var humanDto = dto.Entities.First(e => e.Id == humanEntity.Id.ToString());
        var catDto   = dto.Entities.First(e => e.Id == catEntity.Id.ToString());

        Assert.Equal(SpeciesType.Human, humanDto.Species);
        Assert.Equal(SpeciesType.Cat,   catDto.Species);
    }

    /// <summary>
    /// AT-03 variant: without EntityManager supplied, species falls back to Unknown.
    /// </summary>
    [Fact]
    public void AT03_NoEntityManager_SpeciesFallsBackToUnknown()
    {
        var sim  = MakeSim(humanCount: 1);
        var snap = sim.Capture();

        // Use the overload without EntityManager
        var dto = TelemetryProjector.Project(snap, FixedCapture, 0L, 0, "test");

        Assert.All(dto.Entities, e =>
            Assert.Equal(SpeciesType.Unknown, e.Species));
    }

    // ── Serializer helpers ────────────────────────────────────────────────────

    /// <summary>SerializeFrame appends exactly one newline.</summary>
    [Fact]
    public void SerializeFrame_AppendsNewline()
    {
        var sim  = MakeSim(humanCount: 0);
        var dto  = Capture(sim);
        var line = TelemetrySerializer.SerializeFrame(dto);

        Assert.EndsWith("\n", line);
        // Only ONE trailing newline — the JSON body itself has no newlines (compact).
        Assert.DoesNotContain("\n", line.TrimEnd('\n'));
    }

    /// <summary>SerializeSnapshot and SerializeFrame produce the same JSON content.</summary>
    [Fact]
    public void SerializeSnapshot_AndFrame_SameContent()
    {
        var sim      = MakeSim(humanCount: 0);
        var dto      = Capture(sim);
        var snapshot = TelemetrySerializer.SerializeSnapshot(dto);
        var frame    = TelemetrySerializer.SerializeFrame(dto);

        Assert.Equal(snapshot, frame.TrimEnd('\n'));
    }
}
