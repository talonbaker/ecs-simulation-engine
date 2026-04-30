using System;
using System.Linq;
using Xunit;
using APIFramework.Components;
using APIFramework.Core;

namespace APIFramework.Tests.Components;

public class LockedTagTests
{
    [Fact]
    public void LockedTag_CanBeAttachedToEntity()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();

        entity.Add(new LockedTag());

        Assert.True(entity.Has<LockedTag>());
    }

    [Fact]
    public void LockedTag_CanBeRemovedFromEntity()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();

        entity.Add(new LockedTag());
        Assert.True(entity.Has<LockedTag>());

        entity.Remove<LockedTag>();
        Assert.False(entity.Has<LockedTag>());
    }

    [Fact]
    public void LockedTag_CanBeQueried()
    {
        var em = new EntityManager();
        var door1 = em.CreateEntity();
        var door2 = em.CreateEntity();
        var notDoor = em.CreateEntity();

        door1.Add(new LockedTag());
        door2.Add(new LockedTag());
        notDoor.Add(new StainTag());

        var lockedDoors = em.Query<LockedTag>().ToList();
        Assert.Equal(2, lockedDoors.Count);
    }
}
