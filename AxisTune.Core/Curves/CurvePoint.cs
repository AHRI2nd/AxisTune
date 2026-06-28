namespace AxisTune.Core.Curves;

/// <summary>
/// 응답 곡선의 제어점. X(입력)·Y(출력) 모두 [0, 1] 정규화 좌표.
/// </summary>
public readonly struct CurvePoint
{
    public readonly float X;
    public readonly float Y;

    public CurvePoint(float x, float y)
    {
        X = x;
        Y = y;
    }
}
