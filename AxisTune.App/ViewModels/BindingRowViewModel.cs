using AxisTune.Core.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AxisTune.App.ViewModels;

/// <summary>매핑 화면의 한 줄: 하나의 Xbox 타깃(버튼 또는 축)과 현재 바인딩 표시.</summary>
public partial class BindingRowViewModel : ObservableObject
{
    public string Label { get; }
    public bool IsAxis { get; }
    public XboxButton Button { get; init; }
    public AxisChannel Axis { get; init; }

    [ObservableProperty] private string bindingText = "(없음)";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaptureLabel))]
    private bool isCapturing;

    public string CaptureLabel => IsCapturing ? "대기..." : "바인딩";

    private readonly Action<BindingRowViewModel> _bind;
    private readonly Action<BindingRowViewModel> _clear;

    private BindingRowViewModel(string label, bool isAxis,
        Action<BindingRowViewModel> bind, Action<BindingRowViewModel> clear)
    {
        Label = label;
        IsAxis = isAxis;
        _bind = bind;
        _clear = clear;
    }

    public static BindingRowViewModel ForButton(string label, XboxButton button,
        Action<BindingRowViewModel> bind, Action<BindingRowViewModel> clear)
        => new(label, false, bind, clear) { Button = button };

    public static BindingRowViewModel ForAxis(string label, AxisChannel axis,
        Action<BindingRowViewModel> bind, Action<BindingRowViewModel> clear)
        => new(label, true, bind, clear) { Axis = axis };

    [RelayCommand]
    private void Bind() => _bind(this);

    [RelayCommand]
    private void Clear() => _clear(this);
}
