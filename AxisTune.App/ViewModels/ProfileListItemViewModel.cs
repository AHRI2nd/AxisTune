using CommunityToolkit.Mvvm.ComponentModel;

namespace AxisTune.App.ViewModels;

/// <summary>프로파일 선택 드롭다운의 한 항목.</summary>
public partial class ProfileListItemViewModel : ObservableObject
{
    public string Id { get; }

    [ObservableProperty] private string name;

    public ProfileListItemViewModel(string id, string name)
    {
        Id = id;
        this.name = name;
    }
}
