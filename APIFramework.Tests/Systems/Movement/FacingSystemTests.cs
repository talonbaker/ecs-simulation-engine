using APIFramework.Components;
using APIFramework.Core;
using APIFramework.Systems.Movement;
using APIFramework.Systems.Spatial;
using Xunit;

namespace APIFramework.Tests.Systems.Movement;

/// <summary>
/// AT-13 to AT-14: FacingSystem — facing from velocity and conversation partner override.
/// </summary>
public class FacingSystemTests
{
    private static (EntityManager em, ProximityEventBus bus, FacingSystem sys) Build()
    {
        var em  = new EntityManager();
        var bus = new ProximityEventBus();
        var sys = new FacingSystem(bus);
        return (em, bus, sys);
    }

    private static Entity MakeMovingNpc(EntityManager em, float velX, float velZ)
    {
        var e = em.CreateEntity();
        e.Add(new PositionComponent { X = 5f, Y = 0f, Z = 5f });
        e.Add(new MovementComponent { Speed = 1f, SpeedModifier = 1f, LastVelocityX = velX, LastVelocityZ = velZ });
        e.Add(new FacingComponent { DirectionDeg = 0f, Source = FacingSource.Idle });
        return e;
    }

    // AT-13: Facing follows velocity when no conversation partner
    [Fact]
    public void MovingNorth_FacesNorth()
    {
        var (em, _, sys) = Build();
        // Moving north: Z decreasing → velZ < 0 → direction 0°
        var npc = MakeMovingNpc(em, velX: 0f, velZ: -1f);
        sys.Update(em, 1f);

        float dir = npc.Get<FacingComponent>().DirectionDeg;
        Assert.InRange(dir, -1f, 1f);
        Assert.Equal(FacingSource.MovementVelocity, npc.Get<FacingComponent>().Source);
    }

    [Fact]
    public void MovingEast_FacesEast()
    {
        var (em, _, sys) = Build();
        var npc = MakeMovingNpc(em, velX: 1f, velZ: 0f);
        sys.Update(em, 1f);

        float dir = npc.Get<FacingComponent>().DirectionDeg;
        Assert.InRange(dir, 89f, 91f);
    }

    [Fact]
    public void MovingSouth_FacesSouth()
    {
        var (em, _, sys) = Build();
        var npc = MakeMovingNpc(em, velX: 0f, velZ: 1f);
        sys.Update(em, 1f);

        float dir = npc.Get<FacingComponent>().DirectionDeg;
        Assert.InRange(dir, 179f, 181f);
    }

    [Fact]
    public void MovingWest_FacesWest()
    {
        var (em, _, sys) = Build();
        var npc = MakeMovingNpc(em, velX: -1f, velZ: 0f);
        sys.Update(em, 1f);

        float dir = npc.Get<FacingComponent>().DirectionDeg;
        Assert.InRange(dir, 269f, 271f);
    }

    // AT-13: Stationary NPC (velocity = 0) is not updated by FacingSystem
    [Fact]
    public void StationaryNpc_FacingUnchanged()
    {
        var (em, _, sys) = Build();
        var npc = MakeMovingNpc(em, velX: 0f, velZ: 0f);
        npc.Add(new FacingComponent { DirectionDeg = 42f, Source = FacingSource.Idle });

        sys.Update(em, 1f);

        Assert.Equal(42f, npc.Get<FacingComponent>().DirectionDeg);
    }

    // AT-14: Facing overrides to conversation partner when in conversation range
    [Fact]
    public void InConversationRange_FacesPartner()
    {
        var (em, bus, sys) = Build();

        var npc     = MakeMovingNpc(em, velX: 1f, velZ: 0f); // moving east
        var partner = em.CreateEntity();
        partner.Add(new PositionComponent { X = 5f, Y = 0f, Z = 8f }); // partner is to the south

        // Fire the conversation-range event to register the partner
        bus.RaiseEnteredConversationRange(new ProximityEnteredConversationRange(npc, partner, 1));

        sys.Update(em, 1f);

        var facing = npc.Get<FacingComponent>();
        // Partner is at (5, 8) and NPC is at (5, 5): direction is south (180°)
        Assert.InRange(facing.DirectionDeg, 179f, 181f);
        Assert.Equal(FacingSource.ConversationPartner, facing.Source);
    }

    // Leaving conversation range removes partner override
    [Fact]
    public void LeftConversationRange_FacingReverts()
    {
        var (em, bus, sys) = Build();

        var npc     = MakeMovingNpc(em, velX: 1f, velZ: 0f);
        var partner = em.CreateEntity();
        partner.Add(new PositionComponent { X = 5f, Y = 0f, Z = 8f });

        bus.RaiseEnteredConversationRange(new ProximityEnteredConversationRange(npc, partner, 1));
        sys.Update(em, 1f);

        // Verify override is active
        Assert.Equal(FacingSource.ConversationPartner, npc.Get<FacingComponent>().Source);

        // Now leave conversation range
        bus.RaiseLeftConversationRange(new ProximityLeftConversationRange(npc, partner, 2));
        sys.Update(em, 1f);

        // Should revert to movement velocity
        Assert.Equal(FacingSource.MovementVelocity, npc.Get<FacingComponent>().Source);
    }

    // VectorToAngle helper correctness
    [Theory]
    [InlineData(0f,  -1f, 0f)]    // north
    [InlineData(1f,   0f, 90f)]   // east
    [InlineData(0f,   1f, 180f)]  // south
    [InlineData(-1f,  0f, 270f)]  // west
    public void VectorToAngle_CorrectConversion(float dx, float dz, float expectedDeg)
    {
        float actual = FacingSystem.VectorToAngle(dx, dz);
        Assert.InRange(actual, expectedDeg - 1f, expectedDeg + 1f);
    }
}
