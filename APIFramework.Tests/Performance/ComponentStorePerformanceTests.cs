using APIFramework.Core;
using APIFramework.Components;
using Xunit;
using System.Diagnostics;

namespace APIFramework.Tests.Performance;

/// <summary>
/// Performance tests for the ComponentStore refactor.
///
/// Measures:
///   1. Allocation per tick (target: ≥80% reduction vs pre-refactor baseline)
///   2. Get&lt;T&gt;() micro-benchmark (target: ≥2× faster than pre-refactor)
///
/// Note: This is v0.1 perf validation. Pre-refactor baseline is captured manually
/// before the refactor begins. Numbers are documented in the completion note.
/// </summary>
public class ComponentStorePerformanceTests
{
    [Fact]
    public void Allocation_Per_Tick_Baseline()
    {
        // Captures baseline allocation for a simple entity-component scenario.
        // This test is informational — it measures the post-refactor performance.
        // The pre-refactor baseline should be captured separately and documented.

        var registry = new ComponentStoreRegistry();
        const int entityCount = 100;
        const int ticks = 100;

        // Create entities with multiple components
        var entities = new List<Entity>();
        for (int i = 0; i < entityCount; i++)
        {
            var e = new Entity(registry);
            e.Add(new DriveComponent { EatUrgency = 0.5f });
            e.Add(new StressComponent { AcuteLevel = 50 });
            entities.Add(e);
        }

        // Measure allocation over multiple ticks
        GC.Collect();
        GC.WaitForPendingFinalizers();
        long allocBefore = GC.GetTotalAllocatedBytes(precise: true);

        for (int tick = 0; tick < ticks; tick++)
        {
            foreach (var e in entities)
            {
                var drive = e.Get<DriveComponent>();
                _ = e.Get<StressComponent>();

                // Simulate updating a component
                var updated = new DriveComponent { EatUrgency = drive.EatUrgency + 0.01f, DrinkUrgency = drive.DrinkUrgency };
                e.Set(updated);
            }
        }

        long allocAfter = GC.GetTotalAllocatedBytes(precise: true);
        long allocDelta = allocAfter - allocBefore;
        long allocPerTick = allocDelta / ticks;

        // This test documents the post-refactor allocation.
        // With the refactor, allocation should be much lower than pre-refactor.
        // This is informational; the exact number depends on GC behavior and test setup.
        // For reference, pre-refactor boxing would be ~100KB+ per tick at this scale.
        Assert.True(allocPerTick < 50000, $"Allocation per tick: {allocPerTick} bytes");
    }

    [Fact]
    public void Get_T_Micro_Benchmark()
    {
        // Benchmarks Get&lt;T&gt;() performance with a simple loop.
        // Target: ≥2× faster than pre-refactor (Dictionary boxing).

        var registry = new ComponentStoreRegistry();
        const int entityCount = 100;
        const int iterationCount = 100000;

        var entities = new List<Entity>();
        for (int i = 0; i < entityCount; i++)
        {
            var e = new Entity(registry);
            e.Add(new DriveComponent { EatUrgency = 0.5f });
            entities.Add(e);
        }

        var sw = Stopwatch.StartNew();

        for (int iter = 0; iter < iterationCount; iter++)
        {
            foreach (var e in entities)
            {
                var _ = e.Get<DriveComponent>();
            }
        }

        sw.Stop();

        long opsPerSecond = (long)((iterationCount * entityCount) / sw.Elapsed.TotalSeconds);

        // This test documents the post-refactor Get<T>() performance.
        // With the refactor (no boxing), we expect > 10M ops/sec on modern hardware.
        Assert.True(opsPerSecond > 1_000_000, $"Get<T>() performance: {opsPerSecond} ops/sec");
    }

    [Fact]
    public void Has_T_IsEfficient()
    {
        // Verifies that Has&lt;T&gt;() is fast (single hash lookup).
        // Tags use Has&lt;T&gt;() frequently; this must be O(1).

        var registry = new ComponentStoreRegistry();
        const int entityCount = 1000;
        const int iterationCount = 100000;

        var entities = new List<Entity>();
        for (int i = 0; i < entityCount; i++)
        {
            var e = new Entity(registry);
            e.Add(new TaskTag());
            entities.Add(e);
        }

        var sw = Stopwatch.StartNew();

        for (int iter = 0; iter < iterationCount; iter++)
        {
            foreach (var e in entities)
            {
                var _ = e.Has<TaskTag>();
            }
        }

        sw.Stop();

        long opsPerSecond = (long)((iterationCount * entityCount) / sw.Elapsed.TotalSeconds);

        // Has<T>() should be very fast — at least as fast as Get<T>().
        Assert.True(opsPerSecond > 1_000_000, $"Has<T>() performance: {opsPerSecond} ops/sec");
    }

    [Fact]
    public void Set_T_IsEfficient()
    {
        // Verifies that Set&lt;T&gt;() is fast (single hash table write).

        var registry = new ComponentStoreRegistry();
        const int entityCount = 100;
        const int iterationCount = 10000;

        var entities = new List<Entity>();
        for (int i = 0; i < entityCount; i++)
        {
            var e = new Entity(registry);
            e.Add(new DriveComponent { EatUrgency = 0.5f });
            entities.Add(e);
        }

        var sw = Stopwatch.StartNew();

        for (int iter = 0; iter < iterationCount; iter++)
        {
            foreach (var e in entities)
            {
                var comp = e.Get<DriveComponent>();
                var updated = new DriveComponent { EatUrgency = comp.EatUrgency + 0.01f, DrinkUrgency = comp.DrinkUrgency };
                e.Set(updated);
            }
        }

        sw.Stop();

        long opsPerSecond = (long)((iterationCount * entityCount) / sw.Elapsed.TotalSeconds);

        // Set<T>() should be fast — typical hash table write performance.
        Assert.True(opsPerSecond > 100_000, $"Set<T>() performance: {opsPerSecond} ops/sec");
    }

    [Fact]
    public void MultiType_Access_Parallel_Stores()
    {
        // Verifies that accessing multiple component types remains efficient
        // when they're stored in separate typed dictionaries.

        var registry = new ComponentStoreRegistry();
        const int entityCount = 500;
        const int iterationCount = 1000;

        var entities = new List<Entity>();
        for (int i = 0; i < entityCount; i++)
        {
            var e = new Entity(registry);
            e.Add(new DriveComponent { EatUrgency = 0.5f });
            e.Add(new StressComponent { AcuteLevel = 50 });
            entities.Add(e);
        }

        var sw = Stopwatch.StartNew();

        for (int iter = 0; iter < iterationCount; iter++)
        {
            foreach (var e in entities)
            {
                var drive = e.Get<DriveComponent>();
                var stress = e.Get<StressComponent>();
                // Simulate system update
                var updated = new DriveComponent { EatUrgency = stress.AcuteLevel * 0.01f, DrinkUrgency = drive.DrinkUrgency };
                e.Set(updated);
            }
        }

        sw.Stop();

        long opsPerSecond = (long)((iterationCount * entityCount * 3) / sw.Elapsed.TotalSeconds);

        // Multi-type access should remain efficient (each type is independent).
        Assert.True(opsPerSecond > 500_000, $"Multi-type access: {opsPerSecond} ops/sec");
    }
}
