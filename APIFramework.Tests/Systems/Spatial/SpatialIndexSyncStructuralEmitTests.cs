using System;
using System.Collections.Generic;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Spatial;

/// <summary>
/// AT-09: When a StructuralTag desk moves, SpatialIndexSyncSystem emits EntityMoved
/// with correct prev/cur tiles. NPC movement emits nothing on the structural bus.
/// </summary>
public class SpatialIndexSyncStructuralEmitTests
{
    private static ISpatialIndex MakeIndex()
    {
        var config = new APIFramework.Config.SpatialConfig
        {
            CellSizeTiles = 4,
            WorldSize = new APIFramework.Config.SpatialWorldSizeConfig { Width = 64, Height = 64 }
        };
        return new GridSpatialIndex(config);
    }

    [Fact]
    public void StructuralTagEntityMoves_EmitsEntityMoved_WithCorrectTiles()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var sys = new SpatialIndexSyncSystem(MakeIndex(), bus);

        var desk = em.CreateEntity();
        desk.Add(default(StructuralTag));
        desk.Add(new PositionComponent { X = 2f, Z = 3f });

        var events = new List<StructuralChangeEvent>();
        bus.Subscribe(e => events.Add(e));

        sys.Update(em, 0.016f);  // EntityAdded
        Assert.Single(events);
        Assert.Equal(StructuralChangeKind.EntityAdded, events[0].Kind);
        Assert.Equal(desk.Id, events[0].EntityId);

        events.Clear();

        // Move the desk
        desk.Add(new PositionComponent { X = 5f, Z = 7f });
        sys.Update(em, 0.016f);  // EntityMoved

        Assert.Single(events);
        Assert.Equal(StructuralChangeKind.EntityMoved, events[0].Kind);
        Assert.Equal(desk.Id, events[0].EntityId);
        Assert.Equal(2, events[0].PreviousTileX);
        Assert.Equal(3, events[0].PreviousTileY);
        Assert.Equal(5, events[0].CurrentTileX);
        Assert.Equal(7, events[0].CurrentTileY);
    }

    [Fact]
    public void NonStructuralEntityMoves_EmitsNothing()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var sys = new SpatialIndexSyncSystem(MakeIndex(), bus);

        var npc = em.CreateEntity();
        npc.Add(default(NpcTag));
        npc.Add(new PositionComponent { X = 1f, Z = 1f });

        var events = new List<StructuralChangeEvent>();
        bus.Subscribe(e => events.Add(e));

        sys.Update(em, 0.016f);  // first register — no structural event
        npc.Add(new PositionComponent { X = 2f, Z = 1f });
        sys.Update(em, 0.016f);  // move — no structural event

        Assert.Empty(events);
    }

    [Fact]
    public void StructuralEntityDestroyed_EmitsEntityRemoved()
    {
        var em  = new EntityManager();
        var bus = new StructuralChangeBus();
        var idx = MakeIndex();
        var sys = new SpatialIndexSyncSystem(idx, bus);
        em.EntityDestroyed += sys.OnEntityDestroyed;

        var desk = em.CreateEntity();
        desk.Add(default(StructuralTag));
        desk.Add(new PositionComponent { X = 4f, Z = 4f });

        sys.Update(em, 0.016f);  // register

        var events = new List<StructuralChangeEvent>();
        bus.Subscribe(e => events.Add(e));

        em.DestroyEntity(desk);

        Assert.Single(events);
        Assert.Equal(StructuralChangeKind.EntityRemoved, events[0].Kind);
        Assert.Equal(desk.Id, events[0].EntityId);
    }
}
