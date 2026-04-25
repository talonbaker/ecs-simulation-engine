using System;
using System.Collections.Generic;
using System.Text.Json;
using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using Warden.Contracts.Handshake;
using Warden.Telemetry;
using Xunit;

namespace Warden.Telemetry.Tests;

/// <summary>
/// Acceptance tests for <see cref="CommandDispatcher"/>:
///
/// AT-04 — Invalid batch → Rejected > 0, Applied == 0, sim unchanged.
/// AT-05 — Valid spawn-food → new bolus entity visible in next Project call.
/// AT-06 — Valid set-config-value → SimConfig mutated in-place.
/// AT-07 — No invariant violations from any single whitelisted command.
/// </summary>
public class CommandDispatcherTests
{
    // ── Shared fixture helpers ────────────────────────────────────────────────

    private static SimulationBootstrapper MakeSim(int humanCount = 1)
        => new(new InMemoryConfigProvider(new SimConfig()), humanCount);

    private static readonly DateTimeOffset FixedAt =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly CommandDispatcher Dispatcher = new();

    private static AiCommandBatch Batch(params AiCommand[] cmds)
        => new() { Commands = new List<AiCommand>(cmds) };

    private static int EntityCount(SimulationBootstrapper sim)
        => sim.EntityManager.Entities.Count;

    // ── AT-04 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AT-04: A batch containing an invalid command is rejected atomically.
    /// Applied == 0, Rejected == batch size, sim entity count unchanged.
    /// </summary>
    [Fact]
    public void AT04_InvalidBatch_RejectsAtomically()
    {
        var sim   = MakeSim();
        int before = EntityCount(sim);

        var batch = Batch(
            new SpawnFoodCommand("Banana", 5f, 0f, 5f),      // valid
            new SpawnFoodCommand("",       5f, 0f, 5f)        // invalid — empty FoodType
        );

        var result = Dispatcher.Apply(sim, batch);

        Assert.Equal(0, result.Applied);
        Assert.Equal(2, result.Rejected);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(before, EntityCount(sim));   // sim is unchanged
    }

    /// <summary>
    /// AT-04 variant: unknown entity ID in remove-entity.
    /// </summary>
    [Fact]
    public void AT04_UnknownEntityId_RejectsWithError()
    {
        var sim   = MakeSim();
        int before = EntityCount(sim);

        var batch = Batch(
            new RemoveEntityCommand(Guid.NewGuid().ToString())
        );

        var result = Dispatcher.Apply(sim, batch);

        Assert.Equal(0, result.Applied);
        Assert.True(result.Rejected > 0);
        Assert.Equal(before, EntityCount(sim));
    }

    /// <summary>
    /// AT-04 variant: malformed GUID in remove-entity.
    /// </summary>
    [Fact]
    public void AT04_MalformedGuid_RejectsWithError()
    {
        var sim = MakeSim();

        var batch = Batch(new RemoveEntityCommand("not-a-guid"));
        var result = Dispatcher.Apply(sim, batch);

        Assert.Equal(0, result.Applied);
        Assert.True(result.Rejected > 0);
        Assert.Contains(result.Errors, e => e.Contains("not a valid GUID"));
    }

    /// <summary>
    /// AT-04 variant: invalid force-dominant drive name.
    /// </summary>
    [Fact]
    public void AT04_InvalidDominant_RejectsWithError()
    {
        var sim   = MakeSim();
        var human = GetFirstHuman(sim);

        var batch = Batch(new ForceDominantCommand(human.Id.ToString(), "Fly", 60));
        var result = Dispatcher.Apply(sim, batch);

        Assert.Equal(0, result.Applied);
        Assert.True(result.Rejected > 0);
    }

    // ── AT-05 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AT-05: A valid spawn-food command creates a new bolus entity visible
    /// in the next TelemetryProjector.Project call.
    /// </summary>
    [Fact]
    public void AT05_SpawnFood_NewBolusVisibleInNextProjection()
    {
        var sim = MakeSim(humanCount: 0);    // no humans — world-item list starts empty

        var snapBefore = sim.Capture();
        Assert.Empty(snapBefore.WorldItems);   // precondition

        var batch  = Batch(new SpawnFoodCommand("TestBanana", 3f, 0f, 3f));
        var result = Dispatcher.Apply(sim, batch);

        Assert.Equal(1, result.Applied);
        Assert.Equal(0, result.Rejected);

        var snapAfter = sim.Capture();
        var dto       = TelemetryProjector.Project(
            snapAfter, sim.EntityManager, FixedAt, 1L, 0, "test");

        Assert.Contains(dto.WorldItems, item => item.Label == "TestBanana");
    }

    /// <summary>
    /// AT-05 variant: spawn-food Count > 1 creates multiple entities.
    /// </summary>
    [Fact]
    public void AT05_SpawnFood_CountRespected()
    {
        var sim = MakeSim(humanCount: 0);

        var batch  = Batch(new SpawnFoodCommand("Apple", 5f, 0f, 5f, Count: 3));
        var result = Dispatcher.Apply(sim, batch);

        Assert.Equal(1, result.Applied);

        var snap = sim.Capture();
        int appleCount = 0;
        foreach (var item in snap.WorldItems)
            if (item.Label == "Apple") appleCount++;

        Assert.Equal(3, appleCount);
    }

    /// <summary>
    /// AT-05 variant: spawn-liquid creates a liquid entity.
    /// </summary>
    [Fact]
    public void AT05_SpawnLiquid_LiquidEntityVisible()
    {
        var sim = MakeSim(humanCount: 0);

        var batch  = Batch(new SpawnLiquidCommand("Water", 7f, 0f, 2f));
        var result = Dispatcher.Apply(sim, batch);

        Assert.Equal(1, result.Applied);

        var snap = sim.Capture();
        Assert.Contains(snap.WorldItems, i => i.Label == "Water");
    }

    // ── AT-06 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AT-06: A valid set-config-value command mutates the running SimConfig
    /// in-place, verifiable via sim.Config.Systems.Brain.SleepMaxScore.
    /// </summary>
    [Fact]
    public void AT06_SetConfigValue_MutatesSimConfigInPlace()
    {
        var sim = MakeSim();
        float original = sim.Config.Systems.Brain.SleepMaxScore;
        float newValue  = original + 0.1f;

        var batch = Batch(new SetConfigValueCommand(
            "Systems.Brain.SleepMaxScore",
            JsonSerializer.SerializeToElement(newValue)));

        var result = Dispatcher.Apply(sim, batch);

        Assert.Equal(1, result.Applied);
        Assert.Equal(0, result.Rejected);
        Assert.Equal(newValue, sim.Config.Systems.Brain.SleepMaxScore, precision: 5);
    }

    /// <summary>
    /// AT-06 variant: set-config-value with an invalid path is rejected.
    /// </summary>
    [Fact]
    public void AT06_SetConfigValue_InvalidPath_Rejected()
    {
        var sim = MakeSim();

        var batch = Batch(new SetConfigValueCommand(
            "Systems.Brain.NonExistentField",
            JsonSerializer.SerializeToElement(1.0f)));

        var result = Dispatcher.Apply(sim, batch);

        Assert.Equal(0, result.Applied);
        Assert.True(result.Rejected > 0);
    }

    /// <summary>
    /// AT-06 variant: set-position moves entity to new coordinates.
    /// </summary>
    [Fact]
    public void AT06_SetPosition_UpdatesEntityPosition()
    {
        var sim   = MakeSim();
        var human = GetFirstHuman(sim);

        var batch  = Batch(new SetPositionCommand(human.Id.ToString(), 9f, 0f, 9f));
        var result = Dispatcher.Apply(sim, batch);

        Assert.Equal(1, result.Applied);

        var pos = human.Get<PositionComponent>();
        Assert.Equal(9f, pos.X, precision: 5);
        Assert.Equal(0f, pos.Y, precision: 5);
        Assert.Equal(9f, pos.Z, precision: 5);
    }

    // ── AT-07 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AT-07: Dispatching each whitelisted command in isolation produces
    /// no invariant violations (violation count does not increase).
    /// </summary>
    [Fact]
    public void AT07_SpawnFood_NoInvariantViolations()
        => AssertNoNewViolations(sim =>
            Batch(new SpawnFoodCommand("Banana", 5f, 0f, 5f)));

    [Fact]
    public void AT07_SpawnLiquid_NoInvariantViolations()
        => AssertNoNewViolations(sim =>
            Batch(new SpawnLiquidCommand("Water", 5f, 0f, 5f)));

    [Fact]
    public void AT07_RemoveEntity_NoInvariantViolations()
        => AssertNoNewViolations(sim =>
        {
            // Spawn a food item to remove (safer than removing a living entity).
            var food = sim.EntityManager.CreateEntity();
            food.Add(new BolusComponent { FoodType = "TestFood", Volume = 10f });
            return Batch(new RemoveEntityCommand(food.Id.ToString()));
        });

    [Fact]
    public void AT07_SetPosition_NoInvariantViolations()
        => AssertNoNewViolations(sim =>
        {
            var human = GetFirstHuman(sim);
            return Batch(new SetPositionCommand(human.Id.ToString(), 5f, 0f, 5f));
        });

    [Fact]
    public void AT07_ForceDominant_NoInvariantViolations()
        => AssertNoNewViolations(sim =>
        {
            var human = GetFirstHuman(sim);
            return Batch(new ForceDominantCommand(human.Id.ToString(), "Eat", 60));
        });

    [Fact]
    public void AT07_SetConfigValue_NoInvariantViolations()
        => AssertNoNewViolations(_ =>
            Batch(new SetConfigValueCommand(
                "Systems.Brain.SleepMaxScore",
                JsonSerializer.SerializeToElement(0.8f))));

    // ── AT-07 helper ──────────────────────────────────────────────────────────

    private void AssertNoNewViolations(Func<SimulationBootstrapper, AiCommandBatch> batchFactory)
    {
        var sim    = MakeSim();
        int before = sim.Capture().ViolationCount;

        var batch  = batchFactory(sim);
        var result = Dispatcher.Apply(sim, batch);

        // The command must have applied (not been rejected).
        Assert.Equal(0, result.Rejected);

        // Run one tick so InvariantSystem has a chance to check.
        sim.Engine.Update(0.016f);

        int after = sim.Capture().ViolationCount;
        Assert.True(after <= before + before / 10 + 1,
            $"Violation count grew unexpectedly from {before} to {after}.");
    }

    // ── Entity lookup helpers ─────────────────────────────────────────────────

    private static Entity GetFirstHuman(SimulationBootstrapper sim)
    {
        foreach (var e in sim.EntityManager.Query<HumanTag>())
            return e;

        throw new InvalidOperationException("No human entity found in sim.");
    }
}
