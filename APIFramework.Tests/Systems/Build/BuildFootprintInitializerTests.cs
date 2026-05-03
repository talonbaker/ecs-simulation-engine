using APIFramework.Build;
using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Build;
using Xunit;

namespace APIFramework.Tests.Systems.Build;

/// <summary>
/// AT-05: Initializer attaches BuildFootprintComponent to a spawned prop by its propTypeId.
/// AT-06: Spawned prop without a catalog match logs a warning but does not crash; component left unattached.
/// </summary>
public class BuildFootprintInitializerTests
{
    private const string CatalogJson = @"{
  ""schemaVersion"": ""0.1.0"",
  ""propFootprints"": [
    { ""propTypeId"": ""desk"", ""widthTiles"": 2, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 0.75, ""canStackOnTop"": true, ""footprintCategory"": ""Furniture"" },
    { ""propTypeId"": ""chair"", ""widthTiles"": 1, ""depthTiles"": 1, ""bottomHeight"": 0.0, ""topHeight"": 0.45, ""canStackOnTop"": false, ""footprintCategory"": ""Furniture"" }
  ]
}";

    // AT-05: entity with known propTypeId gets correct BuildFootprintComponent.
    [Fact]
    public void Update_EntityWithKnownPropTypeId_AttachesFootprint()
    {
        var em      = new EntityManager();
        var catalog = BuildFootprintCatalog.ParseJson(CatalogJson);
        var system  = new BuildFootprintInitializerSystem(catalog);

        var entity = em.CreateEntity();
        entity.Add(new PropTypeIdComponent { PropTypeId = "desk" });

        system.Update(em, 0f);

        Assert.True(entity.Has<BuildFootprintComponent>());
        var fp = entity.Get<BuildFootprintComponent>();
        Assert.Equal(2, fp.WidthTiles);
        Assert.Equal(1, fp.DepthTiles);
        Assert.Equal(0.75f, fp.TopHeight, precision: 5);
        Assert.True(fp.CanStackOnTop);
        Assert.Equal("Furniture", fp.FootprintCategory);
    }

    // AT-05: chair entity gets correct footprint.
    [Fact]
    public void Update_ChairEntity_AttachesChairFootprint()
    {
        var em      = new EntityManager();
        var catalog = BuildFootprintCatalog.ParseJson(CatalogJson);
        var system  = new BuildFootprintInitializerSystem(catalog);

        var entity = em.CreateEntity();
        entity.Add(new PropTypeIdComponent { PropTypeId = "chair" });

        system.Update(em, 0f);

        Assert.True(entity.Has<BuildFootprintComponent>());
        var fp = entity.Get<BuildFootprintComponent>();
        Assert.False(fp.CanStackOnTop);
        Assert.Equal(0.45f, fp.TopHeight, precision: 5);
    }

    // AT-06: entity with unknown propTypeId does NOT get a component; no crash.
    [Fact]
    public void Update_EntityWithUnknownPropTypeId_DoesNotCrash_NoComponentAttached()
    {
        var em      = new EntityManager();
        var catalog = BuildFootprintCatalog.ParseJson(CatalogJson);
        var system  = new BuildFootprintInitializerSystem(catalog);

        var entity = em.CreateEntity();
        entity.Add(new PropTypeIdComponent { PropTypeId = "vending-machine" });

        // Must not throw.
        system.Update(em, 0f);

        Assert.False(entity.Has<BuildFootprintComponent>());
    }

    // Idempotent: running twice does not double-attach or overwrite.
    [Fact]
    public void Update_RunTwice_IdempotentAttachment()
    {
        var em      = new EntityManager();
        var catalog = BuildFootprintCatalog.ParseJson(CatalogJson);
        var system  = new BuildFootprintInitializerSystem(catalog);

        var entity = em.CreateEntity();
        entity.Add(new PropTypeIdComponent { PropTypeId = "desk" });

        system.Update(em, 0f);
        system.Update(em, 0f);

        Assert.True(entity.Has<BuildFootprintComponent>());
        Assert.Equal(2, entity.Get<BuildFootprintComponent>().WidthTiles);
    }

    // Entity without PropTypeIdComponent is not touched.
    [Fact]
    public void Update_EntityWithoutPropTypeId_NotTouched()
    {
        var em      = new EntityManager();
        var catalog = BuildFootprintCatalog.ParseJson(CatalogJson);
        var system  = new BuildFootprintInitializerSystem(catalog);

        var entity = em.CreateEntity();
        // No PropTypeIdComponent added.

        system.Update(em, 0f);

        Assert.False(entity.Has<BuildFootprintComponent>());
    }
}
