using APIFramework.Components;
using APIFramework.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ECSVisualizer.ViewModels;

/// <summary>
/// Represents a world-item entity (food or liquid) that is currently sitting
/// in the simulation world — i.e. NOT inside an esophagus transit.
/// Updated each tick by MainViewModel.
/// </summary>
public partial class WorldEntityViewModel : ObservableObject
{
    // ── Identity ──────────────────────────────────────────────────────────────
    [ObservableProperty] private string _entityId  = "";
    [ObservableProperty] private string _itemLabel = "";

    // ── Rot state ─────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _hasRot       = false;
    [ObservableProperty] private float  _rotLevel     = 0f;
    [ObservableProperty] private string _rotLabel     = "";
    [ObservableProperty] private bool   _isRotten     = false;   // RotTag present

    // ── Nutritional summary ───────────────────────────────────────────────────
    [ObservableProperty] private string _nutritionLabel = "";

    // ─────────────────────────────────────────────────────────────────────────

    public void Update(Entity entity)
    {
        EntityId = entity.ShortId;

        // ── Label: prefer food type, fall back to liquid type ─────────────────
        if (entity.Has<BolusComponent>())
        {
            var bolus = entity.Get<BolusComponent>();
            ItemLabel      = bolus.FoodType ?? "Food";
            NutritionLabel = $"{bolus.Nutrients.Calories:F0} kcal  ·  {bolus.Volume:F0}ml";
        }
        else if (entity.Has<LiquidComponent>())
        {
            var liquid = entity.Get<LiquidComponent>();
            ItemLabel      = liquid.LiquidType ?? "Liquid";
            NutritionLabel = $"{liquid.VolumeMl:F0}ml  ·  water {liquid.Nutrients.Water:F0}ml";
        }
        else
        {
            ItemLabel      = "Unknown";
            NutritionLabel = "";
        }

        // ── Rot ───────────────────────────────────────────────────────────────
        HasRot   = entity.Has<RotComponent>();
        IsRotten = entity.Has<RotTag>();
        if (HasRot)
        {
            var rot   = entity.Get<RotComponent>();
            RotLevel  = rot.RotLevel;
            RotLabel  = IsRotten
                ? $"ROTTEN  {rot.RotLevel:F0}%"
                : rot.IsDecaying
                    ? $"Decaying  {rot.RotLevel:F1}%"
                    : $"Fresh  ({rot.AgeSeconds:F0}s old)";
        }
        else
        {
            RotLevel = 0f;
            RotLabel = "";
        }
    }
}
