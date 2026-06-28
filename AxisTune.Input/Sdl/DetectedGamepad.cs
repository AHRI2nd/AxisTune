namespace AxisTune.Input.Sdl;

/// <summary>
/// 감지된 물리 컨트롤러의 식별 정보. <see cref="Vendor"/>/<see cref="Product"/>는
/// ViGEm 가상 패드(루프백) 필터링과 HidHide 숨김 대상 선정에 사용한다.
/// </summary>
public sealed record DetectedGamepad(
    uint InstanceId,
    string Name,
    GamepadKind Kind,
    ushort Vendor,
    ushort Product,
    bool IsGamepad)
{
    /// <summary>ViGEm 가상 Xbox 360 패드(Microsoft VID 0x045E / PID 0x028E)인지 여부.</summary>
    public bool IsLikelyVirtualXbox => Vendor == 0x045E && Product == 0x028E;
}
