namespace AxisTune.Core.Controls;

/// <summary>
/// 정규화된 논리 Xbox 컨트롤러 상태. 스틱 축은 [-1, 1], 트리거는 [0, 1].
/// 값 타입(struct)으로 hot path에서 힙 할당 없이 복사/전달한다.
/// 실제 XUSB(short/byte)로의 스케일링은 출력 계층에서 수행한다.
/// </summary>
public struct XboxOutputState
{
    public XboxButton Buttons;

    public float LeftStickX;
    public float LeftStickY;
    public float RightStickX;
    public float RightStickY;

    /// <summary>[0, 1]</summary>
    public float LeftTrigger;

    /// <summary>[0, 1]</summary>
    public float RightTrigger;

    public static XboxOutputState Empty => default;

    /// <summary>채널 인덱스로 아날로그 값을 읽는다 (hot path, 분기 최소화).</summary>
    public readonly float GetAxis(AxisChannel channel) => channel switch
    {
        AxisChannel.LeftStickX => LeftStickX,
        AxisChannel.LeftStickY => LeftStickY,
        AxisChannel.RightStickX => RightStickX,
        AxisChannel.RightStickY => RightStickY,
        AxisChannel.LeftTrigger => LeftTrigger,
        AxisChannel.RightTrigger => RightTrigger,
        _ => 0f,
    };

    /// <summary>채널 인덱스로 아날로그 값을 쓴다.</summary>
    public void SetAxis(AxisChannel channel, float value)
    {
        switch (channel)
        {
            case AxisChannel.LeftStickX: LeftStickX = value; break;
            case AxisChannel.LeftStickY: LeftStickY = value; break;
            case AxisChannel.RightStickX: RightStickX = value; break;
            case AxisChannel.RightStickY: RightStickY = value; break;
            case AxisChannel.LeftTrigger: LeftTrigger = value; break;
            case AxisChannel.RightTrigger: RightTrigger = value; break;
        }
    }

    public void SetButton(XboxButton button, bool pressed)
    {
        if (pressed) Buttons |= button;
        else Buttons &= ~button;
    }
}
