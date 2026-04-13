namespace APIFramework.Components;

// The physical object once taken from the fridge
public struct FoodObjectComponent
{
    public string Name;
    public float NutritionPerBite;
    public int BitesRemaining;
    public float Toughness; // Used for the chewing logic later
}
