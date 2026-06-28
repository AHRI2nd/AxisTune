namespace AxisTune.Input.Sdl;

/// <summary>자동 감지된 컨트롤러 분류(앱 표시/기본 매핑 선택용).</summary>
public enum GamepadKind
{
    Unknown = 0,
    Xbox,
    PlayStation,
    SwitchPro,
    JoyconPair,
    JoyconSingle,
    Standard,
}
