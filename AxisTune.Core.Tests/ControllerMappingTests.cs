using AxisTune.Core.Controls;
using AxisTune.Core.Profiles;

namespace AxisTune.Core.Tests;

public class ControllerMappingTests
{
    [Fact]
    public void ButtonBinding_SetsTargetButton()
    {
        var mapping = new ControllerMapping(
            new[] { new ButtonBinding(2, XboxButton.A) },
            Array.Empty<HatBinding>(),
            Array.Empty<AxisBinding>());

        var raw = new RawJoystickState(0, 4, 0);
        raw.Buttons[2] = true;

        var state = XboxOutputState.Empty;
        mapping.Apply(raw, ref state);
        Assert.True(state.Buttons.HasFlag(XboxButton.A));
    }

    [Fact]
    public void HatBinding_SetsDpad()
    {
        var mapping = new ControllerMapping(
            Array.Empty<ButtonBinding>(),
            new[] { new HatBinding(0, HatDirection.Up, XboxButton.DpadUp) },
            Array.Empty<AxisBinding>());

        var raw = new RawJoystickState(0, 0, 1);
        raw.Hats[0] = (byte)HatDirection.Up;

        var state = XboxOutputState.Empty;
        mapping.Apply(raw, ref state);
        Assert.True(state.Buttons.HasFlag(XboxButton.DpadUp));
    }

    [Fact]
    public void AxisBinding_MapsStick_WithInvert()
    {
        var mapping = new ControllerMapping(
            Array.Empty<ButtonBinding>(),
            Array.Empty<HatBinding>(),
            new[] { new AxisBinding(1, AxisChannel.LeftStickY, invert: true) });

        var raw = new RawJoystickState(2, 0, 0);
        raw.Axes[1] = 16384; // 약 +0.5

        var state = XboxOutputState.Empty;
        mapping.Apply(raw, ref state);
        Assert.Equal(-0.5f, state.LeftStickY, 2); // invert
    }

    [Fact]
    public void AxisBinding_Trigger_MapsFullAxisToZeroOne()
    {
        var mapping = new ControllerMapping(
            Array.Empty<ButtonBinding>(),
            Array.Empty<HatBinding>(),
            new[] { new AxisBinding(0, AxisChannel.LeftTrigger, invert: false) });

        var raw = new RawJoystickState(1, 0, 0);

        // 완전 해제(-32767) → 0
        raw.Axes[0] = -32767;
        var state = XboxOutputState.Empty;
        mapping.Apply(raw, ref state);
        Assert.Equal(0f, state.LeftTrigger, 2);

        // 완전 누름(+32767) → 1
        raw.Axes[0] = 32767;
        state = XboxOutputState.Empty;
        mapping.Apply(raw, ref state);
        Assert.Equal(1f, state.LeftTrigger, 2);
    }

    [Fact]
    public void Mapping_RoundTrips_ThroughDto()
    {
        var original = new ControllerMapping(
            new[] { new ButtonBinding(1, XboxButton.B) },
            new[] { new HatBinding(0, HatDirection.Left, XboxButton.DpadLeft) },
            new[] { new AxisBinding(3, AxisChannel.RightStickX, true) });

        var dto = ProfileSerializer.FromControllerMapping(original);
        var restored = ProfileSerializer.ToControllerMapping(dto);

        Assert.Equal(XboxButton.B, restored.Buttons[0].Target);
        Assert.Equal(HatDirection.Left, restored.Hats[0].Direction);
        Assert.True(restored.Axes[0].Invert);
        Assert.Equal(AxisChannel.RightStickX, restored.Axes[0].Target);
    }

    [Fact]
    public void OutOfRangeIndices_AreIgnored()
    {
        var mapping = new ControllerMapping(
            new[] { new ButtonBinding(99, XboxButton.A) },
            Array.Empty<HatBinding>(),
            new[] { new AxisBinding(99, AxisChannel.LeftStickX, false) });

        var raw = new RawJoystickState(2, 2, 0);
        var state = XboxOutputState.Empty;
        mapping.Apply(raw, ref state); // 예외 없이 무시
        Assert.Equal(XboxButton.None, state.Buttons);
        Assert.Equal(0f, state.LeftStickX, 3);
    }
}
