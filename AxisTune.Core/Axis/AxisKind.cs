namespace AxisTune.Core.Axis;

/// <summary>축의 부호 특성.</summary>
public enum AxisKind
{
    /// <summary>양극성 스틱 축. 값 도메인 [-1, 1] (부호 보존, 크기에 곡선 적용).</summary>
    Bipolar = 0,

    /// <summary>단극성 트리거 축. 값 도메인 [0, 1].</summary>
    Unipolar = 1,
}
