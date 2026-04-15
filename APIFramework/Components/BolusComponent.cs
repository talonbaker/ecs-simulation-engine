namespace APIFramework.Components;

// Represents the physical food ball with nutritional data
public struct BolusComponent
{
    public float Volume;           // Physical size in the esophagus
    public float NutritionValue;   // How much this satisfies hunger
    public float Toughness;        // 0.0 (soft) to 1.0 (requires heavy chewing)
    public string FoodType;        // "Apple", "Steak", etc.

    public override string ToString() => $"{FoodType} (Nutr: {NutritionValue} | Vol: {Volume})";
}