using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Core;

/// <summary>
/// Integration tests for Entity component access via the new ComponentStoreRegistry.
///
/// Verifies that Entity.Get&lt;T&gt;() / Has&lt;T&gt;() / Set&lt;T&gt;() / Add&lt;T&gt;() / Remove&lt;T&gt;()
/// work correctly when delegating to the registry, and that components from real
/// engine types are stored and retrieved accurately.
/// </summary>
public class EntityComponentAccessTests
{
    private struct DriveComp { public float EatUrgency; public float DrinkUrgency; }
    private struct StressComp { public int AcuteLevel; public int ChronicLevel; }
    private struct TaskTag { }

    [Fact]
    public void Entity_Get_ReturnsValueStored()
    {
        var registry = new ComponentStoreRegistry();
        var entity = new Entity(registry);

        entity.Add(new DriveComp { EatUrgency = 0.5f, DrinkUrgency = 0.3f });
        var retrieved = entity.Get<DriveComp>();

        Assert.Equal(0.5f, retrieved.EatUrgency);
        Assert.Equal(0.3f, retrieved.DrinkUrgency);
    }

    [Fact]
    public void Entity_Has_ReportsTrueAfterAdd()
    {
        var registry = new ComponentStoreRegistry();
        var entity = new Entity(registry);

        Assert.False(entity.Has<DriveComp>());

        entity.Add(new DriveComp { EatUrgency = 0.5f, DrinkUrgency = 0.3f });

        Assert.True(entity.Has<DriveComp>());
    }

    [Fact]
    public void Entity_Has_ReportsFalseAfterRemove()
    {
        var registry = new ComponentStoreRegistry();
        var entity = new Entity(registry);

        entity.Add(new DriveComp { EatUrgency = 0.5f, DrinkUrgency = 0.3f });
        entity.Remove<DriveComp>();

        Assert.False(entity.Has<DriveComp>());
    }

    [Fact]
    public void Entity_Set_OverwritesExistingComponent()
    {
        var registry = new ComponentStoreRegistry();
        var entity = new Entity(registry);

        var initial = new DriveComp { EatUrgency = 0.5f, DrinkUrgency = 0.3f };
        entity.Add(initial);
        entity.Set(new DriveComp { EatUrgency = 0.8f, DrinkUrgency = 0.6f });

        var retrieved = entity.Get<DriveComp>();
        Assert.Equal(0.8f, retrieved.EatUrgency);
        Assert.Equal(0.6f, retrieved.DrinkUrgency);
    }

    [Fact]
    public void Entity_Add_Twice_OverwritesButNoDoubleCallback()
    {
        var registry = new ComponentStoreRegistry();
        var callCount = 0;
        var entity = new Entity(registry, (_, _, added) => { if (added) callCount++; });

        entity.Add(new DriveComp { EatUrgency = 0.5f, DrinkUrgency = 0.3f });
        Assert.Equal(1, callCount);

        // Second Add should overwrite but NOT fire callback
        entity.Add(new DriveComp { EatUrgency = 0.8f, DrinkUrgency = 0.6f });
        Assert.Equal(1, callCount);  // Still 1, not 2

        var retrieved = entity.Get<DriveComp>();
        Assert.Equal(0.8f, retrieved.EatUrgency);
    }

    [Fact]
    public void Entity_Remove_FiresCallback()
    {
        var registry = new ComponentStoreRegistry();
        var removedTypes = new List<Type>();
        var entity = new Entity(registry, (_, type, added) => { if (!added) removedTypes.Add(type); });

        entity.Add(new DriveComp { EatUrgency = 0.5f, DrinkUrgency = 0.3f });
        entity.Remove<DriveComp>();

        Assert.Single(removedTypes);
        Assert.Contains(typeof(DriveComp), removedTypes);
    }

    [Fact]
    public void Entity_MultipleComponents_Independent()
    {
        var registry = new ComponentStoreRegistry();
        var entity = new Entity(registry);

        entity.Add(new DriveComp { EatUrgency = 0.5f, DrinkUrgency = 0.3f });
        entity.Add(new StressComp { AcuteLevel = 50, ChronicLevel = 30 });

        Assert.True(entity.Has<DriveComp>());
        Assert.True(entity.Has<StressComp>());

        var drive = entity.Get<DriveComp>();
        var stress = entity.Get<StressComp>();

        Assert.Equal(0.5f, drive.EatUrgency);
        Assert.Equal(50, stress.AcuteLevel);
    }

    [Fact]
    public void Entity_RemoveOneDoesNotAffectAnother()
    {
        var registry = new ComponentStoreRegistry();
        var entity = new Entity(registry);

        entity.Add(new DriveComp { EatUrgency = 0.5f, DrinkUrgency = 0.3f });
        entity.Add(new StressComp { AcuteLevel = 50, ChronicLevel = 30 });

        entity.Remove<DriveComp>();

        Assert.False(entity.Has<DriveComp>());
        Assert.True(entity.Has<StressComp>());
        Assert.Equal(50, entity.Get<StressComp>().AcuteLevel);
    }

    [Fact]
    public void Entity_TagComponent_Behavior()
    {
        var registry = new ComponentStoreRegistry();
        var entity = new Entity(registry);

        Assert.False(entity.Has<TaskTag>());

        entity.Add(new TaskTag());
        Assert.True(entity.Has<TaskTag>());

        entity.Remove<TaskTag>();
        Assert.False(entity.Has<TaskTag>());
    }

    [Fact]
    public void Entity_SharedRegistry_BothEntitiesIndependent()
    {
        var registry = new ComponentStoreRegistry();
        var entity1 = new Entity(registry);
        var entity2 = new Entity(registry);

        entity1.Add(new DriveComp { EatUrgency = 0.5f, DrinkUrgency = 0.3f });
        entity2.Add(new DriveComp { EatUrgency = 0.8f, DrinkUrgency = 0.6f });

        var drive1 = entity1.Get<DriveComp>();
        var drive2 = entity2.Get<DriveComp>();

        Assert.Equal(0.5f, drive1.EatUrgency);
        Assert.Equal(0.8f, drive2.EatUrgency);
    }
}
