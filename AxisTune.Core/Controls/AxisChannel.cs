namespace AxisTune.Core.Controls;

/// <summary>
/// 처리 가능한 6개의 아날로그 채널. 인덱스는 <see cref="XboxOutputState"/>의
/// 축 배열/프로파일 배열과 1:1로 대응한다.
/// </summary>
public enum AxisChannel
{
    LeftStickX = 0,
    LeftStickY = 1,
    RightStickX = 2,
    RightStickY = 3,
    LeftTrigger = 4,
    RightTrigger = 5,
}

public static class AxisChannelInfo
{
    /// <summary>처리 채널 개수.</summary>
    public const int Count = 6;
}
