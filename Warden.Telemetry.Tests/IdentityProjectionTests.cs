using System;
using System.Linq;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using Warden.Contracts.Telemetry;
using Warden.Telemetry;
using Xunit;

namespace Warden.Telemetry.Tests;

/// <summary>
/// AT-07 — TelemetryProjector serialises IdentityComponent.Name to EntityStateDto.Name.
/// </summary>
public class IdentityProjectionTests
{
    private static readonly DateTimeOffset FixedCapture =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static SimulationBootstrapper MakeSim()
        => new(new InMemoryConfigProvider(new SimConfig()), humanCount: 0);

    // ── AT-07: IdentityComponent.Name flows through to EntityStateDto.Name ────

    [Fact]
    public void AT07_EntityWithIdentityName_ProjectsNameToDto()
    {
        var sim    = MakeSim();
        var entity = EntityTemplates.SpawnHuman(sim.EntityManager, name: "Donna");

        var snap = sim.Capture();
        var dto  = TelemetryProjector.Project(
            snap, sim.EntityManager, FixedCapture, 0, 42, "test");

        var entityDto = dto.Entities.First(e => e.Id == entity.Id.ToString());
        Assert.Equal("Donna", entityDto.Name);
    }

    [Fact]
    public void AT07_EntityWithDifferentName_ProjectsCorrectName()
    {
        var sim    = MakeSim();
        var entity = EntityTemplates.SpawnHuman(sim.EntityManager, name: "Greg");

        var snap = sim.Capture();
        var dto  = TelemetryProjector.Project(
            snap, sim.EntityManager, FixedCapture, 0, 42, "test");

        var entityDto = dto.Entities.First(e => e.Id == entity.Id.ToString());
        Assert.Equal("Greg", entityDto.Name);
    }

    [Fact]
    public void AT07_TwoEntitiesWithDifferentNames_EachProjectsOwnName()
    {
        var sim   = MakeSim();
        var donna = EntityTemplates.SpawnHuman(sim.EntityManager, name: "Donna");
        var frank = EntityTemplates.SpawnHuman(sim.EntityManager, name: "Frank");

        var snap = sim.Capture();
        var dto  = TelemetryProjector.Project(
            snap, sim.EntityManager, FixedCapture, 0, 42, "test");

        Assert.Equal("Donna",
            dto.Entities.First(e => e.Id == donna.Id.ToString()).Name);
        Assert.Equal("Frank",
            dto.Entities.First(e => e.Id == frank.Id.ToString()).Name);
    }
}
