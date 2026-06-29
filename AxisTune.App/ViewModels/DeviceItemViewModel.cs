using AxisTune.App.Localization;
using AxisTune.Input.Sdl;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AxisTune.App.ViewModels;

/// <summary>장치 목록의 한 항목.</summary>
public partial class DeviceItemViewModel : ObservableObject
{
    public DeviceItemViewModel(DetectedGamepad device)
    {
        InstanceId = device.InstanceId;
        Name = device.Name;
        Kind = device.Kind;
        IsGamepad = device.IsGamepad;
    }

    public uint InstanceId { get; }
    public string Name { get; }
    public GamepadKind Kind { get; }

    /// <summary>SDL이 표준 게임패드로 인식했는지(false면 수동 매핑 필요).</summary>
    public bool IsGamepad { get; }

    public string KindLabel => Localizer.Instance.Get(Kind switch
    {
        GamepadKind.Xbox => "Kind_Xbox",
        GamepadKind.PlayStation => "Kind_PlayStation",
        GamepadKind.SwitchPro => "Kind_SwitchPro",
        GamepadKind.JoyconPair => "Kind_JoyconPair",
        GamepadKind.JoyconSingle => "Kind_JoyconSingle",
        GamepadKind.Standard => "Kind_Standard",
        _ => "Kind_Unknown",
    });

    public string Display => IsGamepad
        ? $"{KindLabel} · {Name}"
        : $"{Localizer.Instance.Get("Dev_Manual")} · {Name}";

    public void RefreshLocalized()
    {
        OnPropertyChanged(nameof(KindLabel));
        OnPropertyChanged(nameof(Display));
    }
}
