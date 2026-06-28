using AxisTune.Input.Sdl;

namespace AxisTune.App.ViewModels;

/// <summary>장치 목록의 한 항목.</summary>
public sealed class DeviceItemViewModel
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

    public string KindLabel => Kind switch
    {
        GamepadKind.Xbox => "Xbox",
        GamepadKind.PlayStation => "PlayStation",
        GamepadKind.SwitchPro => "Switch Pro",
        GamepadKind.JoyconPair => "Joy-Con 페어",
        GamepadKind.JoyconSingle => "Joy-Con",
        GamepadKind.Standard => "표준",
        _ => "알 수 없음",
    };

    public string Display => IsGamepad ? $"{KindLabel} · {Name}" : $"수동 · {Name}";
}
