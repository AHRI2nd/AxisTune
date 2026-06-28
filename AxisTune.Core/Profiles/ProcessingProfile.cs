using AxisTune.Core.Axis;
using AxisTune.Core.Controls;

namespace AxisTune.Core.Profiles;

/// <summary>
/// 6개 아날로그 채널의 <see cref="AxisConfig"/> 묶음(불변). 실시간 경로에서
/// <see cref="Apply"/>는 원시 상태를 정제된 상태로 변환한다(struct 복사, 힙 할당 없음).
/// 설정 변경은 <see cref="WithAxis"/>로 새 인스턴스를 만들어 원자적으로 교체한다.
/// </summary>
public sealed class ProcessingProfile
{
    private readonly AxisConfig[] _axes; // 길이 = AxisChannelInfo.Count

    public ProcessingProfile(AxisConfig[] axes)
    {
        if (axes is null) throw new ArgumentNullException(nameof(axes));
        if (axes.Length != AxisChannelInfo.Count)
            throw new ArgumentException($"축 설정은 정확히 {AxisChannelInfo.Count}개여야 합니다.", nameof(axes));
        _axes = axes;
    }

    public AxisConfig GetAxis(AxisChannel channel) => _axes[(int)channel];

    /// <summary>원시 입력 상태에 축별 처리를 적용한 새 상태를 반환(hot path).</summary>
    public XboxOutputState Apply(in XboxOutputState raw)
    {
        XboxOutputState o = raw; // 버튼은 그대로 복사(Stage 3에서 리매핑 추가)
        o.LeftStickX = _axes[0].Process(raw.LeftStickX);
        o.LeftStickY = _axes[1].Process(raw.LeftStickY);
        o.RightStickX = _axes[2].Process(raw.RightStickX);
        o.RightStickY = _axes[3].Process(raw.RightStickY);
        o.LeftTrigger = _axes[4].Process(raw.LeftTrigger);
        o.RightTrigger = _axes[5].Process(raw.RightTrigger);
        return o;
    }

    /// <summary>지정 채널만 교체한 새 프로파일을 반환(copy-on-write).</summary>
    public ProcessingProfile WithAxis(AxisChannel channel, AxisConfig config)
    {
        if (config is null) throw new ArgumentNullException(nameof(config));
        var copy = (AxisConfig[])_axes.Clone();
        copy[(int)channel] = config;
        return new ProcessingProfile(copy);
    }

    /// <summary>모든 채널이 패스스루(항등)인 기본 프로파일.</summary>
    public static ProcessingProfile Passthrough()
    {
        var axes = new AxisConfig[AxisChannelInfo.Count];
        axes[0] = AxisConfig.Passthrough(AxisKind.Bipolar);
        axes[1] = AxisConfig.Passthrough(AxisKind.Bipolar);
        axes[2] = AxisConfig.Passthrough(AxisKind.Bipolar);
        axes[3] = AxisConfig.Passthrough(AxisKind.Bipolar);
        axes[4] = AxisConfig.Passthrough(AxisKind.Unipolar);
        axes[5] = AxisConfig.Passthrough(AxisKind.Unipolar);
        return new ProcessingProfile(axes);
    }
}
