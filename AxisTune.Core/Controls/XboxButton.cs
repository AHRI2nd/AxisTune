namespace AxisTune.Core.Controls;

/// <summary>
/// 논리 Xbox 360 버튼. 값은 XUSB <c>wButtons</c> 비트마스크와 동일하게 맞춰
/// 출력 계층(ViGEm)에서 <see cref="ushort"/>로 그대로 캐스팅할 수 있게 한다.
/// </summary>
[Flags]
public enum XboxButton : ushort
{
    None = 0,
    DpadUp = 0x0001,
    DpadDown = 0x0002,
    DpadLeft = 0x0004,
    DpadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    Guide = 0x0400,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}
