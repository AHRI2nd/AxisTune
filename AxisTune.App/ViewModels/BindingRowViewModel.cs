using AxisTune.App.Localization;
using AxisTune.Core.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AxisTune.App.ViewModels;

/// <summary>매핑 화면의 한 줄: 하나의 Xbox 타깃(버튼 또는 축)과 현재 바인딩 표시.</summary>
public partial class BindingRowViewModel : ObservableObject
{
    /// <summary>현지화 키 또는 그대로 표시할 기호(A, B, D-Pad ↑ 등).</summary>
    public string LabelKey { get; init; } = string.Empty;
    public string Label => Localizer.Instance.Get(LabelKey);

    public bool IsAxis { get; }
    public XboxButton Button { get; init; }
    public AxisChannel Axis { get; init; }

    [ObservableProperty] private string bindingText = "(none)";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaptureLabel))]
    private bool isCapturing;

    public string CaptureLabel => Localizer.Instance.Get(IsCapturing ? "Map_Waiting" : "Map_Bind");

    private readonly Action<BindingRowViewModel> _bind;
    private readonly Action<BindingRowViewModel> _clear;

    private BindingRowViewModel(bool isAxis,
        Action<BindingRowViewModel> bind, Action<BindingRowViewModel> clear)
    {
        IsAxis = isAxis;
        _bind = bind;
        _clear = clear;
    }

    public static BindingRowViewModel ForButton(string labelKey, XboxButton button,
        Action<BindingRowViewModel> bind, Action<BindingRowViewModel> clear)
        => new(false, bind, clear) { LabelKey = labelKey, Button = button };

    public static BindingRowViewModel ForAxis(string labelKey, AxisChannel axis,
        Action<BindingRowViewModel> bind, Action<BindingRowViewModel> clear)
        => new(true, bind, clear) { LabelKey = labelKey, Axis = axis };

    public void RefreshLocalized()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(CaptureLabel));
    }

    [RelayCommand]
    private void Bind() => _bind(this);

    [RelayCommand]
    private void Clear() => _clear(this);
}
