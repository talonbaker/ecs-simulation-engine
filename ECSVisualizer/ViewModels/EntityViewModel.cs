using CommunityToolkit.Mvvm.ComponentModel;

public partial class EntityItemViewModel : ObservableObject
{
    [ObservableProperty] private string _id;
    [ObservableProperty] private string _description;

    public EntityItemViewModel(int id, string description)
    {
        Id = $"ID: {id}";
        Description = description;
    }
}