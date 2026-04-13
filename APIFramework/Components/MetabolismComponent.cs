namespace APIFramework.Components;

public struct MetabolismComponent
{
    public float Hunger;
    public float HungerRate;

    public override string ToString() => $"{Hunger:F1}% (+{HungerRate}/s)";
}