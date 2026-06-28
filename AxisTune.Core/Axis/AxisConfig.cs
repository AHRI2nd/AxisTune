using AxisTune.Core.Curves;

namespace AxisTune.Core.Axis;

/// <summary>
/// 단일 아날로그 축의 처리 설정(불변). <see cref="Process"/>는 실시간 hot path에서
/// 호출되며 할당·분기·나눗셈을 최소화한다(역수는 생성 시 1회 사전계산).
/// 설정 변경 시에는 새 인스턴스를 만들어 원자적으로 교체(copy-on-write)한다.
///
/// 파이프라인: 입력 보정(min/max) → 데드존(inner/outer) → 응답 곡선(LUT) → 인버트.
/// </summary>
public sealed class AxisConfig
{
    public AxisKind Kind { get; }

    /// <summary>입력 보정 하한 [0,1). 이 값 이하의 (크기)는 0으로 매핑.</summary>
    public float InputMin { get; }

    /// <summary>입력 보정 상한 (0,1]. 이 값 이상의 (크기)는 1로 매핑(포화).</summary>
    public float InputMax { get; }

    /// <summary>중앙 데드존 [0,1). 이 값 이하는 0.</summary>
    public float InnerDeadzone { get; }

    /// <summary>외곽 데드존 [0,1). 이 값만큼 위쪽은 1로 포화.</summary>
    public float OuterDeadzone { get; }

    public bool Invert { get; }

    public CurveLut Curve { get; }

    private readonly float _inputSpanInv;
    private readonly float _outerThreshold;
    private readonly float _deadSpanInv;

    public AxisConfig(
        AxisKind kind,
        float inputMin = 0f,
        float inputMax = 1f,
        float innerDeadzone = 0f,
        float outerDeadzone = 0f,
        bool invert = false,
        CurveLut? curve = null)
    {
        Kind = kind;
        InputMin = Clamp01(inputMin);
        InputMax = Clamp01(inputMax);
        InnerDeadzone = Clamp01(innerDeadzone);
        OuterDeadzone = Clamp01(outerDeadzone);
        Invert = invert;
        Curve = curve ?? CurveLut.Identity;

        float inputSpan = InputMax - InputMin;
        _inputSpanInv = inputSpan > float.Epsilon ? 1f / inputSpan : 0f;

        _outerThreshold = 1f - OuterDeadzone;
        float deadSpan = _outerThreshold - InnerDeadzone;
        _deadSpanInv = deadSpan > float.Epsilon ? 1f / deadSpan : 0f;
    }

    /// <summary>가공 없는 패스스루(항등) 설정.</summary>
    public static AxisConfig Passthrough(AxisKind kind) => new(kind);

    /// <summary>원시 정규화 입력값을 정제된 출력값으로 변환한다(hot path).</summary>
    public float Process(float raw)
    {
        if (Kind == AxisKind.Bipolar)
        {
            if (Invert) raw = -raw;
            float sign = raw < 0f ? -1f : 1f;
            float mag = raw < 0f ? -raw : raw;
            return sign * Shape(mag);
        }
        else
        {
            float v = Shape(raw < 0f ? 0f : raw);
            return Invert ? 1f - v : v;
        }
    }

    /// <summary>크기([0,1])에 보정·데드존·곡선을 순서대로 적용.</summary>
    private float Shape(float mag)
    {
        // 1) 입력 보정 윈도우 [InputMin, InputMax] → [0,1]
        if (mag <= InputMin) mag = 0f;
        else if (mag >= InputMax) mag = 1f;
        else mag = (mag - InputMin) * _inputSpanInv;

        // 2) 데드존 (inner/outer) → [0,1]
        if (mag <= InnerDeadzone) mag = 0f;
        else if (mag >= _outerThreshold) mag = 1f;
        else mag = (mag - InnerDeadzone) * _deadSpanInv;

        // 3) 응답 곡선(LUT)
        return Curve.Evaluate(mag);
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
}
