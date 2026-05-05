using APIFramework.Components;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems.Lighting;
using Xunit;

namespace APIFramework.Tests.Systems.Lighting;

/// <summary>
/// AT-04: Flickering produces both on-ticks and off-ticks within 1000 samples.
/// AT-05: Dying decays intensity toward 0 and transitions to Off at intensity 0.
/// AT-06: On and Off sources do not transition.
/// </summary>
public class LightSourceStateSystemTests
{
    private static LightingConfig DefaultCfg() => new();

    private static (EntityManager em, LightSourceStateSystem sys) Setup(int seed = 42)
    {
        var em  = new EntityManager();
        var rng = new SeededRandom(seed);
        var sys = new LightSourceStateSystem(rng, DefaultCfg());
        return (em, sys);
    }

    private static Entity SpawnSource(EntityManager em, LightState state, int intensity = 80)
    {
        var e = em.CreateEntity();
        e.Add(new LightSourceTag());
        e.Add(new LightSourceComponent
        {
            Id                = System.Guid.NewGuid().ToString(),
            Kind              = LightKind.OverheadFluorescent,
            State             = state,
            Intensity         = intensity,
            ColorTemperatureK = 4000,
            TileX             = 5,
            TileY             = 5,
            RoomId            = "r1",
        });
        return e;
    }

    // -- AT-04: Flickering -----------------------------------------------------

    [Fact]
    public void FlickeringSource_ProducesBothOnAndOffTicks_Over1000Samples()
    {
        var (em, sys) = Setup(seed: 99);
        var src = SpawnSource(em, LightState.Flickering, intensity: 80);

        int onCount  = 0;
        int offCount = 0;
        const int samples = 1000;

        for (int i = 0; i < samples; i++)
        {
            sys.Update(em, 1f);
            double eff = sys.GetEffectiveIntensity(src);
            if (eff > 0) onCount++;
            else         offCount++;
        }

        Assert.True(onCount  > 0, $"Flickering source never produced an on-tick in {samples} samples");
        Assert.True(offCount > 0, $"Flickering source never produced an off-tick in {samples} samples");

        // With flickerOnProb=0.70, expected on-rate is 70%. Loose bounds: 50%–90%
        double onRate = (double)onCount / samples;
        Assert.InRange(onRate, 0.50, 0.90);
    }

    [Fact]
    public void FlickeringSource_ComponentIntensity_NotModified()
    {
        var (em, sys) = Setup(seed: 7);
        var src = SpawnSource(em, LightState.Flickering, intensity: 80);

        for (int i = 0; i < 100; i++)
            sys.Update(em, 1f);

        Assert.Equal(80, src.Get<LightSourceComponent>().Intensity);
    }

    // -- AT-05: Dying ---------------------------------------------------------

    [Fact]
    public void DyingSource_IntensityDecaysTowardZero_OverManyTicks()
    {
        var (em, sys) = Setup(seed: 13);
        var src = SpawnSource(em, LightState.Dying, intensity: 100);

        // Run for 500 ticks — dyingDecayProb=0.05 → expected ~25 decrements in 500 ticks
        for (int i = 0; i < 500; i++)
            sys.Update(em, 1f);

        int finalIntensity = src.Get<LightSourceComponent>().Intensity;
        Assert.True(finalIntensity < 100, $"Expected Dying source to decay below 100 in 500 ticks; got {finalIntensity}");
    }

    [Fact]
    public void DyingSource_TransitionsToOff_WhenIntensityReachesZero()
    {
        var (em, sys) = Setup(seed: 17);
        // Start at very low intensity to reach 0 quickly
        var src = SpawnSource(em, LightState.Dying, intensity: 1);

        // With dyingDecayProb=0.05, intensity=1, should reach 0 within a few hundred ticks
        bool transitioned = false;
        for (int i = 0; i < 500 && !transitioned; i++)
        {
            sys.Update(em, 1f);
            if (src.Get<LightSourceComponent>().State == LightState.Off)
                transitioned = true;
        }

        Assert.True(transitioned, "Dying source with intensity 1 did not transition to Off within 500 ticks");
        Assert.Equal(0, src.Get<LightSourceComponent>().Intensity);
    }

    [Fact]
    public void DyingSource_EffectiveIntensity_MatchesCurrentIntensity()
    {
        var (em, sys) = Setup(seed: 5);
        var src = SpawnSource(em, LightState.Dying, intensity: 50);

        sys.Update(em, 1f);

        int    storedIntensity  = src.Get<LightSourceComponent>().Intensity;
        double effectiveIntens  = sys.GetEffectiveIntensity(src);

        Assert.Equal(storedIntensity, (int)effectiveIntens);
    }

    // -- AT-06: On and Off are stable -----------------------------------------

    [Fact]
    public void OnSource_DoesNotTransition_Over1000Ticks()
    {
        var (em, sys) = Setup();
        var src = SpawnSource(em, LightState.On, intensity: 80);

        for (int i = 0; i < 1000; i++)
            sys.Update(em, 1f);

        Assert.Equal(LightState.On, src.Get<LightSourceComponent>().State);
        Assert.Equal(80,            src.Get<LightSourceComponent>().Intensity);
    }

    [Fact]
    public void OffSource_DoesNotTransition_Over1000Ticks()
    {
        var (em, sys) = Setup();
        var src = SpawnSource(em, LightState.Off, intensity: 0);

        for (int i = 0; i < 1000; i++)
            sys.Update(em, 1f);

        Assert.Equal(LightState.Off, src.Get<LightSourceComponent>().State);
    }

    [Fact]
    public void OnSource_EffectiveIntensity_EqualsComponentIntensity()
    {
        var (em, sys) = Setup();
        var src = SpawnSource(em, LightState.On, intensity: 75);

        sys.Update(em, 1f);

        Assert.Equal(75.0, sys.GetEffectiveIntensity(src));
    }

    [Fact]
    public void OffSource_EffectiveIntensity_IsZero()
    {
        var (em, sys) = Setup();
        var src = SpawnSource(em, LightState.Off, intensity: 0);

        sys.Update(em, 1f);

        Assert.Equal(0.0, sys.GetEffectiveIntensity(src));
    }
}
