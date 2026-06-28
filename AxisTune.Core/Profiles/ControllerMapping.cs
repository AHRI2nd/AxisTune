using AxisTune.Core.Controls;

namespace AxisTune.Core.Profiles;

/// <summary>햇(POV) 방향. 값은 SDL 햇 비트마스크와 동일.</summary>
[Flags]
public enum HatDirection : byte
{
    Up = 0x01,
    Right = 0x02,
    Down = 0x04,
    Left = 0x08,
}

public readonly struct ButtonBinding
{
    public readonly int PhysicalButton;
    public readonly XboxButton Target;
    public ButtonBinding(int physicalButton, XboxButton target)
    {
        PhysicalButton = physicalButton;
        Target = target;
    }
}

public readonly struct HatBinding
{
    public readonly int Hat;
    public readonly HatDirection Direction;
    public readonly XboxButton Target;
    public HatBinding(int hat, HatDirection direction, XboxButton target)
    {
        Hat = hat;
        Direction = direction;
        Target = target;
    }
}

public readonly struct AxisBinding
{
    public readonly int PhysicalAxis;
    public readonly AxisChannel Target;
    public readonly bool Invert;
    public AxisBinding(int physicalAxis, AxisChannel target, bool invert)
    {
        PhysicalAxis = physicalAxis;
        Target = target;
        Invert = invert;
    }
}

/// <summary>
/// 미인식 컨트롤러의 원시 입력(<see cref="RawJoystickState"/>)을 논리 Xbox 상태로 변환하는
/// 사용자 정의 매핑(불변). <see cref="Apply"/>는 hot path에서 호출되며 할당이 없다.
/// </summary>
public sealed class ControllerMapping
{
    private const float AxisScale = 1f / 32767f;

    private readonly ButtonBinding[] _buttons;
    private readonly HatBinding[] _hats;
    private readonly AxisBinding[] _axes;

    public ControllerMapping(ButtonBinding[] buttons, HatBinding[] hats, AxisBinding[] axes)
    {
        _buttons = buttons ?? Array.Empty<ButtonBinding>();
        _hats = hats ?? Array.Empty<HatBinding>();
        _axes = axes ?? Array.Empty<AxisBinding>();
    }

    public IReadOnlyList<ButtonBinding> Buttons => _buttons;
    public IReadOnlyList<HatBinding> Hats => _hats;
    public IReadOnlyList<AxisBinding> Axes => _axes;

    public static ControllerMapping Empty { get; } =
        new(Array.Empty<ButtonBinding>(), Array.Empty<HatBinding>(), Array.Empty<AxisBinding>());

    /// <summary>원시 상태에 매핑을 적용해 논리 Xbox 상태(가공 전)를 만든다.</summary>
    public void Apply(RawJoystickState raw, ref XboxOutputState state)
    {
        XboxButton buttons = XboxButton.None;

        var bb = _buttons;
        for (int i = 0; i < bb.Length; i++)
        {
            int idx = bb[i].PhysicalButton;
            if ((uint)idx < (uint)raw.Buttons.Length && raw.Buttons[idx])
                buttons |= bb[i].Target;
        }

        var hb = _hats;
        for (int i = 0; i < hb.Length; i++)
        {
            int idx = hb[i].Hat;
            if ((uint)idx < (uint)raw.Hats.Length && (raw.Hats[idx] & (byte)hb[i].Direction) != 0)
                buttons |= hb[i].Target;
        }

        state.Buttons = buttons;

        var ab = _axes;
        for (int i = 0; i < ab.Length; i++)
        {
            int idx = ab[i].PhysicalAxis;
            if ((uint)idx >= (uint)raw.Axes.Length) continue;

            float v = raw.Axes[idx] * AxisScale;
            if (v < -1f) v = -1f; else if (v > 1f) v = 1f;
            if (ab[i].Invert) v = -v;

            var ch = ab[i].Target;
            if (ch is AxisChannel.LeftTrigger or AxisChannel.RightTrigger)
                state.SetAxis(ch, v * 0.5f + 0.5f); // [-1,1] → [0,1] (정지 시 완전 해제 가정)
            else
                state.SetAxis(ch, v);
        }
    }
}
