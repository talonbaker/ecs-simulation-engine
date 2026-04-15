namespace APIFramework.Components;

public struct IdentityComponent
{
    public string Name;
    public string Value;

    public IdentityComponent(string name, string value = "")
    {
        Name = name;
        Value = value;
    }

    // Defaulting ToString to Name ensures your UI stays clean by default
    public override string ToString() => Name;
}