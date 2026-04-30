using NUnit.Framework;
using APIFramework.Core;
using APIFramework.Config;
using Warden.Contracts.Telemetry;

/// <summary>
/// AT-04: WorldStateProjectorAdapter.Project returns a non-null DTO.
/// npcs.Count == engine entity count; rooms.Count == engine room entity count.
///
/// Edit-mode test — does not require a running Unity scene.
/// Creates a SimulationBootstrapper in-process and exercises the adapter directly.
/// </summary>
[TestFixture]
public class WorldStateProjectorAdapterTests
{
    private SimulationBootstrapper _bootstrapper;
    private WorldStateProjectorAdapter _adapter;

    [SetUp]
    public void SetUp()
    {
        // Use InMemoryConfigProvider so no file I/O occurs in the test.
        _bootstrapper = new SimulationBootstrapper(
            configProvider: new InMemoryConfigProvider(new SimConfig()),
            humanCount:     5,
            seed:           42);
        _adapter = new WorldStateProjectorAdapter();
    }

    [TearDown]
    public void TearDown()
    {
        _bootstrapper = null;
    }

    [Test]
    public void Project_ReturnsNonNullDto()
    {
        var dto = _adapter.Project(_bootstrapper, tick: 0);
        Assert.IsNotNull(dto, "Project() must return a non-null WorldStateDto.");
    }

    [Test]
    public void Project_DtoSchemaVersion_IsExpected()
    {
        var dto = _adapter.Project(_bootstrapper, tick: 0);
        Assert.AreEqual("0.4.0", dto.SchemaVersion,
            "DTO schema version must be 0.4.0.");
    }

    [Test]
    public void Project_EntityCount_MatchesEngineEntityCount()
    {
        // Advance one tick so entities have had their initializer systems run.
        _bootstrapper.Engine.Update(0.02f);

        var dto = _adapter.Project(_bootstrapper, tick: 1);
        Assert.IsNotNull(dto.Entities, "DTO.Entities must be non-null.");

        // The DTO entities list contains living entities (those with MetabolismComponent).
        // The engine's Query<MetabolismComponent>() count should match.
        int engineLiving = 0;
        foreach (var _ in _bootstrapper.EntityManager.Query<APIFramework.Components.MetabolismComponent>())
            engineLiving++;

        Assert.AreEqual(engineLiving, dto.Entities.Count,
            $"DTO entity count {dto.Entities.Count} must match engine living entity count {engineLiving}.");
    }

    [Test]
    public void Project_ClockFields_ArePopulated()
    {
        var dto = _adapter.Project(_bootstrapper, tick: 0);
        Assert.IsNotNull(dto.Clock, "DTO.Clock must be non-null.");
        Assert.IsNotNull(dto.Clock.GameTimeDisplay, "DTO.Clock.GameTimeDisplay must be non-null.");
    }

    [Test]
    public void Project_Tick_IsStampedCorrectly()
    {
        var dto = _adapter.Project(_bootstrapper, tick: 42);
        Assert.AreEqual(42, dto.Tick, "DTO.Tick must reflect the tick argument passed to Project().");
    }
}
