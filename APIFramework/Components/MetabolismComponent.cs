namespace APIFramework.Components;

public struct MetabolismComponent
{
    // Current states; 0-100% (Standard urge)
    public float Hunger;
    public float Thirst;
    public float BodyTemp;

    public float Nutrition;      // 100% is full, 0% is starving
    public float Hydration;      // 100% is hydrated, 0% is dehydrated
    public float Energy;         // Affects movement/logic speed

    // Rates (Adjusted for 5-hour cycles)
    // 100% / 300 minutes (5 hours) = ~0.33 per minute
    public float HungerRate;
    public float ThirstRate;
    public override string ToString()
    {
        // Replaced H/T with full words for clarity
        return $"Hunger: {Hunger:F1}% (+{HungerRate}) | Thirst: {Thirst:F1}% (+{ThirstRate}) | Temp: {BodyTemp:F1}°C";
    }
}