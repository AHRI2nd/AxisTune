namespace AxisTune.Core.Curves;

/// <summary>제어점 사이를 잇는 보간 방식.</summary>
public enum CurveInterpolation
{
    /// <summary>구간 선형 보간. 단조성이 보장되고 예측 가능.</summary>
    Linear = 0,

    /// <summary>
    /// 단조(monotone) 3차 보간(Fritsch–Carlson). 부드러우면서도
    /// 오버슈트 없이 단조성을 유지 — 커브 에디터에 적합.
    /// </summary>
    MonotoneCubic = 1,
}
