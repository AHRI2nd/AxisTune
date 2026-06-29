using AxisTune.Core.Controls;

namespace AxisTune.App.Controls;

/// <summary>게임패드 다이어그램에서 클릭 가능한 컨트롤 식별자.</summary>
public enum ControllerControl
{
    None,
    LeftStick,
    RightStick,
    LeftTrigger,
    RightTrigger,
    LeftBumper,
    RightBumper,
    A,
    B,
    X,
    Y,
    Back,
    Start,
    Guide,
    DpadUp,
    DpadDown,
    DpadLeft,
    DpadRight,
}

public static class ControllerControlMap
{
    /// <summary>버튼형 컨트롤 → XboxButton(없으면 None).</summary>
    public static XboxButton ToButton(this ControllerControl c) => c switch
    {
        ControllerControl.A => XboxButton.A,
        ControllerControl.B => XboxButton.B,
        ControllerControl.X => XboxButton.X,
        ControllerControl.Y => XboxButton.Y,
        ControllerControl.LeftBumper => XboxButton.LeftShoulder,
        ControllerControl.RightBumper => XboxButton.RightShoulder,
        ControllerControl.Back => XboxButton.Back,
        ControllerControl.Start => XboxButton.Start,
        ControllerControl.Guide => XboxButton.Guide,
        ControllerControl.DpadUp => XboxButton.DpadUp,
        ControllerControl.DpadDown => XboxButton.DpadDown,
        ControllerControl.DpadLeft => XboxButton.DpadLeft,
        ControllerControl.DpadRight => XboxButton.DpadRight,
        ControllerControl.LeftStick => XboxButton.LeftThumb,
        ControllerControl.RightStick => XboxButton.RightThumb,
        _ => XboxButton.None,
    };

    public static bool IsStick(this ControllerControl c)
        => c is ControllerControl.LeftStick or ControllerControl.RightStick;

    public static bool IsTrigger(this ControllerControl c)
        => c is ControllerControl.LeftTrigger or ControllerControl.RightTrigger;
}
