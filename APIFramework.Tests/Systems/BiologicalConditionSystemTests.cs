using APIFramework.Components;
using Xunit;
using APIFramework.Config;
using APIFramework.Core;
using APIFramework.Systems;

namespace APIFramework.Tests.Systems;

/// <summary>
/// Unit tests for BiologicalConditionSystem — the observer that turns physiological
/// measurements into perception tags.
///
/// BiologicalConditionSystem is purely observational: it reads Satiation/Hydration
/// from MetabolismComponent and writes or clears tags. It never spawns entities,
/// never modifies resource values. That makes it the cleanest possible system to test:
/// set a Satiation/Hydration value, call Update, check which tags appear.
///
/// Tags managed:
///   ThirstTag        — Thirst (=100-Hydration) >= ThirstTagThreshold (default 30)
///   DehydratedTag    — Thirst >= DehydratedTagThreshold (default 70)
///   HungerTag        — Hunger (=100-Satiation) >= HungerTagThreshold (default 30)
///   StarvingTag      — Hunger >= StarvingTagThreshold (default 80)
///   IrritableTag     — Hunger > IrritableThreshold OR Thirst > IrritableThreshold (default 60)
/// </summary>
public class BiologicalConditionSystemTests
{
    // -- Standard thresholds used in all tests ---------------------------------

    private static readonly BiologicalConditionSystemConfig Cfg = new()
    {
        ThirstTagThreshold    = 30f,
        DehydratedTagThreshold= 70f,
        HungerTagThreshold    = 30f,
        StarvingTagThreshold  = 80f,
        IrritableThreshold    = 60f,
    };

    private static BiologicalConditionSystem Sys => new(Cfg);

    // -- Helpers ----------------------------------------------------------------

    /// <summary>
    /// Build an entity with the given satiation / hydration.
    /// Reminder: Hunger = 100 - Satiation, Thirst = 100 - Hydration.
    /// So passing satiation:60 means Hunger = 40.
    /// </summary>
    private static (EntityManager em, Entity entity) Build(
        float satiation = 100f,
        float hydration = 100f)
    {
        var em     = new EntityManager();
        var entity = em.CreateEntity();
        entity.Add(new MetabolismComponent { Satiation = satiation, Hydration = hydration });
        return (em, entity);
    }

    // -- ThirstTag --------------------------------------------------------------

    [Fact]
    public void ThirstTag_Applied_When_Thirst_MeetsThreshold()
    {
        // Thirst >= 30 → ThirstTag. Hydration=70 → Thirst=30.
        var (em, entity) = Build(hydration: 70f);
        Sys.Update(em, deltaTime: 1f);
        Assert.True(entity.Has<ThirstTag>());
    }

    [Fact]
    public void ThirstTag_NotApplied_When_Thirst_BelowThreshold()
    {
        // Hydration=75 → Thirst=25 < 30.
        var (em, entity) = Build(hydration: 75f);
        Sys.Update(em, deltaTime: 1f);
        Assert.False(entity.Has<ThirstTag>());
    }

    [Fact]
    public void ThirstTag_Removed_When_Hydration_Recovers()
    {
        // Start with tag applied, then hydration recovers above threshold.
        var (em, entity) = Build(hydration: 70f);
        entity.Add(new ThirstTag()); // pre-existing

        // Update the component to well-hydrated
        entity.Add(new MetabolismComponent { Hydration = 90f }); // Thirst = 10
        Sys.Update(em, deltaTime: 1f);

        Assert.False(entity.Has<ThirstTag>());
    }

    // -- DehydratedTag ---------------------------------------------------------

    [Fact]
    public void DehydratedTag_Applied_When_Thirst_MeetsThreshold()
    {
        // Thirst >= 70 → DehydratedTag. Hydration=30 → Thirst=70.
        var (em, entity) = Build(hydration: 30f);
        Sys.Update(em, deltaTime: 1f);
        Assert.True(entity.Has<DehydratedTag>());
        Assert.True(entity.Has<ThirstTag>()); // ThirstTag also fires (30 < 70 threshold too)
    }

    [Fact]
    public void DehydratedTag_NotApplied_When_Thirst_BelowThreshold()
    {
        // Hydration=40 → Thirst=60 < 70.
        var (em, entity) = Build(hydration: 40f);
        Sys.Update(em, deltaTime: 1f);
        Assert.False(entity.Has<DehydratedTag>());
    }

    // -- HungerTag -------------------------------------------------------------

    [Fact]
    public void HungerTag_Applied_When_Hunger_MeetsThreshold()
    {
        // Hunger >= 30 → HungerTag. Satiation=70 → Hunger=30.
        var (em, entity) = Build(satiation: 70f);
        Sys.Update(em, deltaTime: 1f);
        Assert.True(entity.Has<HungerTag>());
    }

    [Fact]
    public void HungerTag_NotApplied_When_Hunger_BelowThreshold()
    {
        // Satiation=75 → Hunger=25 < 30.
        var (em, entity) = Build(satiation: 75f);
        Sys.Update(em, deltaTime: 1f);
        Assert.False(entity.Has<HungerTag>());
    }

    [Fact]
    public void HungerTag_Removed_When_Satiation_Recovers()
    {
        var (em, entity) = Build(satiation: 70f);
        entity.Add(new HungerTag());

        // Simulate eating restored satiation
        entity.Add(new MetabolismComponent { Satiation = 90f, Hydration = 100f }); // Hunger = 10
        Sys.Update(em, deltaTime: 1f);

        Assert.False(entity.Has<HungerTag>());
    }

    // -- StarvingTag -----------------------------------------------------------

    [Fact]
    public void StarvingTag_Applied_When_Hunger_MeetsThreshold()
    {
        // Hunger >= 80 → StarvingTag. Satiation=20 → Hunger=80.
        var (em, entity) = Build(satiation: 20f);
        Sys.Update(em, deltaTime: 1f);
        Assert.True(entity.Has<StarvingTag>());
        Assert.True(entity.Has<HungerTag>()); // HungerTag also fires
    }

    [Fact]
    public void StarvingTag_NotApplied_When_Hunger_BelowThreshold()
    {
        // Satiation=30 → Hunger=70 < 80.
        var (em, entity) = Build(satiation: 30f);
        Sys.Update(em, deltaTime: 1f);
        Assert.False(entity.Has<StarvingTag>());
    }

    // -- IrritableTag ----------------------------------------------------------

    [Fact]
    public void IrritableTag_Applied_When_Hunger_ExceedsThreshold()
    {
        // Hunger > 60 → IrritableTag. Satiation=39 → Hunger=61.
        var (em, entity) = Build(satiation: 39f);
        Sys.Update(em, deltaTime: 1f);
        Assert.True(entity.Has<IrritableTag>());
    }

    [Fact]
    public void IrritableTag_Applied_When_Thirst_ExceedsThreshold()
    {
        // Thirst > 60 → IrritableTag. Hydration=39 → Thirst=61.
        var (em, entity) = Build(hydration: 39f);
        Sys.Update(em, deltaTime: 1f);
        Assert.True(entity.Has<IrritableTag>());
    }

    [Fact]
    public void IrritableTag_NotApplied_When_BothBelowThreshold()
    {
        // Both hunger and thirst at 50 < 60.
        var (em, entity) = Build(satiation: 50f, hydration: 50f);
        Sys.Update(em, deltaTime: 1f);
        Assert.False(entity.Has<IrritableTag>());
    }

    [Fact]
    public void IrritableTag_Removed_When_BothDropBelowThreshold()
    {
        var (em, entity) = Build(satiation: 39f); // hunger=61, irritable
        entity.Add(new IrritableTag());

        // Satiation recovers — Hunger = 30, Thirst = 0
        entity.Add(new MetabolismComponent { Satiation = 70f, Hydration = 100f });
        Sys.Update(em, deltaTime: 1f);

        Assert.False(entity.Has<IrritableTag>());
    }

    // -- Guard: deltaTime <= 0 --------------------------------------------------

    [Fact]
    public void Update_WithZeroDeltaTime_IsNoOp()
    {
        // The system skips processing when deltaTime <= 0.
        // Tags that were present before should remain; new ones should not appear.
        var (em, entity) = Build(satiation: 20f); // Hunger = 80, normally → StarvingTag
        // But no prior tick, so no tags yet

        Sys.Update(em, deltaTime: 0f);

        // With deltaTime=0 the system returns early — no tag changes
        Assert.False(entity.Has<StarvingTag>());
        Assert.False(entity.Has<HungerTag>());
    }

    // -- Entity without MetabolismComponent ------------------------------------

    [Fact]
    public void EntityWithout_MetabolismComponent_IsIgnored()
    {
        var em = new EntityManager();
        em.CreateEntity(); // bare entity

        var ex = Record.Exception(() => Sys.Update(em, deltaTime: 1f));
        Assert.Null(ex);
    }

    // -- Boundary: exactly at threshold ----------------------------------------

    [Fact]
    public void ThirstTag_Applied_AtExactThreshold()
    {
        // Thirst == 30 exactly (Hydration = 70) should trigger the tag.
        var (em, entity) = Build(hydration: 70f); // Thirst = 30.0
        Sys.Update(em, deltaTime: 1f);
        Assert.True(entity.Has<ThirstTag>());
    }

    [Fact]
    public void ThirstTag_NotApplied_OneBelow_Threshold()
    {
        // Thirst = 29.9 — just under. No tag.
        var (em, entity) = Build(hydration: 70.1f); // Thirst = 29.9
        Sys.Update(em, deltaTime: 1f);
        Assert.False(entity.Has<ThirstTag>());
    }
}
