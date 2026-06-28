namespace AxisTune.Core.Controls;

/// <summary>
/// 미인식(비-게임패드) 컨트롤러의 원시 입력 스냅샷. 장치별 축/버튼/햇 개수에 맞춰
/// 버퍼를 재사용한다(hot path 할당 0). 값 의미는 SDL 조이스틱 규약을 따른다
/// (축 -32768..32767, 햇은 비트마스크 바이트).
/// </summary>
public sealed class RawJoystickState
{
    public short[] Axes { get; private set; }
    public bool[] Buttons { get; private set; }
    public byte[] Hats { get; private set; }

    public RawJoystickState(int axisCount = 0, int buttonCount = 0, int hatCount = 0)
    {
        Axes = new short[Math.Max(0, axisCount)];
        Buttons = new bool[Math.Max(0, buttonCount)];
        Hats = new byte[Math.Max(0, hatCount)];
    }

    /// <summary>장치 변경 시 버퍼 크기를 보정(필요할 때만 재할당).</summary>
    public void EnsureSize(int axisCount, int buttonCount, int hatCount)
    {
        if (Axes.Length != axisCount) Axes = new short[Math.Max(0, axisCount)];
        if (Buttons.Length != buttonCount) Buttons = new bool[Math.Max(0, buttonCount)];
        if (Hats.Length != hatCount) Hats = new byte[Math.Max(0, hatCount)];
    }

    public void Clear()
    {
        Array.Clear(Axes);
        Array.Clear(Buttons);
        Array.Clear(Hats);
    }
}
