using APIFramework.Core;
using Xunit;

namespace APIFramework.Tests.Core;

/// <summary>
/// Unit tests for Entity — the component bag at the heart of the ECS pattern.
///
/// What we're testing:
///   - Add / Has / Get / Remove behave correctly in isolation
///   - The onChange callback fires at exactly the right moments
///   - Overwriting a component value does NOT fire onChange (it's not a new type)
///   - ID construction (fresh Guid vs existing Guid)
///
/// Each test is entirely self-contained — it creates its own Entity instance
/// with a small private struct. No shared state between tests.
/// </summary>
public class EntityTests
{
    // -- Tiny private structs used only inside this test class -----------------
    // Using one-letter names to keep test code scannable without noise.

    private struct PositionComp { public float X; public float Y; }
    private struct VelocityComp { public float Vx; public float Vy; }
    private struct TagComp      { public bool Active; }

    // -- Has<T> ----------------------------------------------------------------

    [Fact]
    public void Has_ReturnsFalse_BeforeAnyAdd()
    {
        var entity = new Entity();
        Assert.False(entity.Has<PositionComp>());
    }

    [Fact]
    public void Has_ReturnsTrue_AfterAdd()
    {
        var entity = new Entity();
        entity.Add(new PositionComp { X = 1f, Y = 2f });
        Assert.True(entity.Has<PositionComp>());
    }

    [Fact]
    public void Has_ReturnsFalse_AfterRemove()
    {
        var entity = new Entity();
        entity.Add(new PositionComp());
        entity.Remove<PositionComp>();
        Assert.False(entity.Has<PositionComp>());
    }

    // -- Get<T> ----------------------------------------------------------------

    [Fact]
    public void Get_ReturnsExactValueAdded()
    {
        var entity = new Entity();
        entity.Add(new PositionComp { X = 3.5f, Y = -7f });

        var result = entity.Get<PositionComp>();

        Assert.Equal(3.5f,  result.X);
        Assert.Equal(-7f,   result.Y);
    }

    [Fact]
    public void Add_Twice_OverwritesValue()
    {
        // The entity should keep the most recent value, not the original.
        var entity = new Entity();
        entity.Add(new PositionComp { X = 1f, Y = 1f });
        entity.Add(new PositionComp { X = 99f, Y = 99f });

        var result = entity.Get<PositionComp>();

        Assert.Equal(99f, result.X);
        Assert.Equal(99f, result.Y);
    }

    [Fact]
    public void Get_Throws_WhenComponentAbsent()
    {
        // Accessing a missing component should throw rather than silently return default.
        var entity = new Entity();
        Assert.Throws<KeyNotFoundException>(() => entity.Get<PositionComp>());
    }

    // -- Remove<T> -------------------------------------------------------------

    [Fact]
    public void Remove_NonExistent_IsNoOp()
    {
        // Removing a component that was never added should not throw.
        var entity = new Entity();
        var ex = Record.Exception(() => entity.Remove<PositionComp>());
        Assert.Null(ex);
    }

    [Fact]
    public void Remove_DoesNotAffectOtherComponents()
    {
        var entity = new Entity();
        entity.Add(new PositionComp { X = 5f });
        entity.Add(new VelocityComp { Vx = 1f });

        entity.Remove<VelocityComp>();

        // VelocityComp is gone, PositionComp is untouched.
        Assert.False(entity.Has<VelocityComp>());
        Assert.True(entity.Has<PositionComp>());
        Assert.Equal(5f, entity.Get<PositionComp>().X);
    }

    // -- onChange callback -----------------------------------------------------

    [Fact]
    public void Add_FiresOnChange_WithIsNew_True()
    {
        // When a brand-new component type is added, onChange must be called
        // with (entity, typeof(T), added: true).
        (Entity? cbEntity, Type? cbType, bool? cbAdded) = (null, null, null);
        var entity = new Entity((e, t, a) => { cbEntity = e; cbType = t; cbAdded = a; });

        entity.Add(new PositionComp());

        Assert.NotNull(cbEntity);
        Assert.Equal(typeof(PositionComp), cbType);
        Assert.True(cbAdded);
    }

    [Fact]
    public void Add_SameTypeTwice_FiresOnChange_OnlyOnce()
    {
        // Overwriting an existing component is an in-place update.
        // The entity's membership in any external index does not change, so
        // the callback must NOT fire on the second Add.
        int callCount = 0;
        var entity = new Entity((_, _, _) => callCount++);

        entity.Add(new PositionComp { X = 1f });
        entity.Add(new PositionComp { X = 2f }); // same type, just new value

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Remove_FiresOnChange_WithIsNew_False()
    {
        (Entity? cbEntity, Type? cbType, bool? cbAdded) = (null, null, null);
        var entity = new Entity((e, t, a) => { cbEntity = e; cbType = t; cbAdded = a; });

        entity.Add(new PositionComp());
        entity.Remove<PositionComp>();

        Assert.Equal(typeof(PositionComp), cbType);
        Assert.False(cbAdded); // false = component was removed
    }

    [Fact]
    public void Remove_NonExistent_DoesNotFireOnChange()
    {
        int callCount = 0;
        var entity = new Entity((_, _, _) => callCount++);

        entity.Remove<PositionComp>(); // was never added

        Assert.Equal(0, callCount);
    }

    [Fact]
    public void Add_MultipleTypes_FiresOnChange_ForEachNewType()
    {
        var seenTypes = new List<Type>();
        var entity = new Entity((_, t, _) => seenTypes.Add(t));

        entity.Add(new PositionComp());
        entity.Add(new VelocityComp());
        entity.Add(new TagComp());

        Assert.Equal(3, seenTypes.Count);
        Assert.Contains(typeof(PositionComp), seenTypes);
        Assert.Contains(typeof(VelocityComp), seenTypes);
        Assert.Contains(typeof(TagComp),      seenTypes);
    }

    // -- Identity --------------------------------------------------------------

    [Fact]
    public void TwoEntities_HaveDifferent_Ids()
    {
        var a = new Entity();
        var b = new Entity();
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void ExistingId_Constructor_PreservesGuid()
    {
        var guid   = Guid.NewGuid();
        var entity = new Entity(guid);
        Assert.Equal(guid, entity.Id);
    }

    [Fact]
    public void ShortId_IsEightChars()
    {
        var entity = new Entity();
        Assert.Equal(8, entity.ShortId.Length);
    }
}
