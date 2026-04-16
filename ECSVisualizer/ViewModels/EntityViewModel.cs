using APIFramework.Components;
using APIFramework.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace ECSVisualizer.ViewModels;

/// <summary>
/// Maps one ECS Entity's current component state into observable properties for the UI.
/// Update() is called every tick by MainViewModel — this class never touches the engine directly.
/// </summary>
public partial class EntityViewModel : ObservableObject
{
    // ── Identity ─────────────────────────────────────────────────────────────
    [ObservableProperty] private string _entityId = "";
    [ObservableProperty] private string _name     = "";

    // ── Active Tags ───────────────────────────────────────────────────────────
    [ObservableProperty] private string _activeTags    = "";
    [ObservableProperty] private bool   _hasActiveTags = false;

    // ── Metabolism — Resources ────────────────────────────────────────────────
    [ObservableProperty] private bool   _hasMetabolism    = false;
    [ObservableProperty] private float  _satiation        = 100f;
    [ObservableProperty] private float  _hydration        = 100f;
    [ObservableProperty] private string _satiationLabel   = "100%";
    [ObservableProperty] private string _hydrationLabel   = "100%";

    // ── Metabolism — Sensations ───────────────────────────────────────────────
    [ObservableProperty] private string _hungerLabel  = "Not hungry";
    [ObservableProperty] private string _thirstLabel  = "Not thirsty";

    // ── Stomach ───────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _hasStomach      = false;
    [ObservableProperty] private float  _stomachFill     = 0f;
    [ObservableProperty] private string _stomachLabel    = "";
    [ObservableProperty] private string _digestionLabel  = "";

    // ── Energy / Sleep ────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _hasEnergy        = false;
    [ObservableProperty] private float  _energy           = 100f;
    [ObservableProperty] private float  _sleepiness       = 0f;
    [ObservableProperty] private string _energyLabel      = "100%";
    [ObservableProperty] private string _sleepinessLabel  = "0%";
    [ObservableProperty] private string _sleepStateLabel  = "Awake";
    [ObservableProperty] private bool   _isSleeping       = false;

    // ── Brain / Priority Queue ────────────────────────────────────────────────
    [ObservableProperty] private bool   _hasDrives       = false;
    [ObservableProperty] private string _dominantDesire  = "NONE";
    [ObservableProperty] private string _driveScores     = "";

    // ── Esophagus Transit ─────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isInTransit     = false;
    [ObservableProperty] private float  _transitProgress = 0f;
    [ObservableProperty] private string _transitLabel    = "";

    // ─────────────────────────────────────────────────────────────────────────

    public void Update(Entity entity)
    {
        EntityId = entity.ShortId;
        Name     = entity.Has<IdentityComponent>()
            ? entity.Get<IdentityComponent>().Name
            : $"Entity {entity.ShortId}";

        // Tags
        var tags = new List<string>();
        if (entity.Has<HungerTag>())     tags.Add("HUNGRY");
        if (entity.Has<ThirstTag>())     tags.Add("THIRSTY");
        if (entity.Has<StarvingTag>())   tags.Add("STARVING");
        if (entity.Has<DehydratedTag>()) tags.Add("DEHYDRATED");
        if (entity.Has<TiredTag>())      tags.Add("TIRED");
        if (entity.Has<ExhaustedTag>())  tags.Add("EXHAUSTED");
        if (entity.Has<SleepingTag>())   tags.Add("SLEEPING");
        if (entity.Has<IrritableTag>())  tags.Add("IRRITABLE");
        ActiveTags    = tags.Count > 0 ? string.Join("  ·  ", tags) : "";
        HasActiveTags = tags.Count > 0;

        // Metabolism — resources and derived sensations
        HasMetabolism = entity.Has<MetabolismComponent>();
        if (HasMetabolism)
        {
            var meta = entity.Get<MetabolismComponent>();

            Satiation      = meta.Satiation;
            Hydration      = meta.Hydration;
            SatiationLabel = $"{meta.Satiation:F1}%";
            HydrationLabel = $"{meta.Hydration:F1}%";

            HungerLabel = meta.Hunger < 5f
                ? "Satisfied"
                : $"Hunger  {meta.Hunger:F1}%";

            ThirstLabel = meta.Thirst < 5f
                ? "Hydrated"
                : $"Thirst  {meta.Thirst:F1}%";
        }

        // Stomach
        HasStomach = entity.Has<StomachComponent>();
        if (HasStomach)
        {
            var stomach    = entity.Get<StomachComponent>();
            StomachFill    = stomach.Fill * 100f;
            StomachLabel   = $"{stomach.Fill:P0}  ({stomach.CurrentVolumeMl:F0} / {StomachComponent.MaxVolumeMl:F0} ml)";
            DigestionLabel = $"Queued — Nutr: {stomach.NutritionQueued:F1}  Hydr: {stomach.HydrationQueued:F1}";
        }

        // Energy / Sleep
        HasEnergy = entity.Has<EnergyComponent>();
        if (HasEnergy)
        {
            var en = entity.Get<EnergyComponent>();

            Energy          = en.Energy;
            Sleepiness      = en.Sleepiness;
            EnergyLabel     = $"{en.Energy:F1}%";
            SleepinessLabel = $"{en.Sleepiness:F1}%";
            IsSleeping      = en.IsSleeping;

            SleepStateLabel = en.IsSleeping ? "Sleeping"
                : en.Energy > 70f            ? "Alert"
                : en.Energy > 40f            ? "Tired"
                                             : "Exhausted";
        }

        // Brain — dominant desire and urgency scores
        HasDrives = entity.Has<DriveComponent>();
        if (HasDrives)
        {
            var d = entity.Get<DriveComponent>();
            DominantDesire = d.Dominant.ToString().ToUpperInvariant();
            DriveScores    = $"eat {d.EatUrgency:F2}  ·  drink {d.DrinkUrgency:F2}  ·  sleep {d.SleepUrgency:F2}";
        }

        // Esophagus transit
        IsInTransit = entity.Has<EsophagusTransitComponent>();
        if (IsInTransit)
        {
            var transit = entity.Get<EsophagusTransitComponent>();
            TransitProgress = transit.Progress * 100f;

            string content = entity.Has<LiquidComponent>()
                ? entity.Get<LiquidComponent>().LiquidType
                : entity.Has<BolusComponent>()
                    ? entity.Get<BolusComponent>().FoodType
                    : "Unknown";

            TransitLabel = $"{content}  —  {transit.Position}%";
        }
    }
}
