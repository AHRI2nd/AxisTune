namespace AxisTune.Core.Curves;

/// <summary>
/// 제어점 집합으로 정의된 응답 곡선. 편집·직렬화 대상이며, 실시간 평가용
/// <see cref="CurveLut"/>를 <see cref="BuildLut"/>로 굽는다(bake). 빌드는 설정 변경 시에만 수행.
/// </summary>
public sealed class CurveDefinition
{
    public const int DefaultResolution = 1024;

    private readonly CurvePoint[] _points; // X 오름차순 정렬, 길이 >= 2

    public CurveInterpolation Interpolation { get; }
    public IReadOnlyList<CurvePoint> Points => _points;

    public CurveDefinition(
        IReadOnlyList<CurvePoint> points,
        CurveInterpolation interpolation = CurveInterpolation.MonotoneCubic)
    {
        if (points is null) throw new ArgumentNullException(nameof(points));
        if (points.Count < 2) throw new ArgumentException("최소 2개의 제어점이 필요합니다.", nameof(points));

        var arr = new CurvePoint[points.Count];
        for (int i = 0; i < points.Count; i++)
            arr[i] = new CurvePoint(Clamp01(points[i].X), Clamp01(points[i].Y));
        Array.Sort(arr, static (a, b) => a.X.CompareTo(b.X));

        _points = arr;
        Interpolation = interpolation;
    }

    /// <summary>실시간 평가용 룩업 테이블을 생성.</summary>
    public CurveLut BuildLut(int resolution = DefaultResolution)
    {
        if (resolution < 1) throw new ArgumentOutOfRangeException(nameof(resolution));

        var table = new float[resolution + 1];
        bool cubic = Interpolation == CurveInterpolation.MonotoneCubic && _points.Length >= 3;
        float[]? tangents = cubic ? ComputeMonotoneTangents(_points) : null;

        for (int i = 0; i <= resolution; i++)
        {
            float x = (float)i / resolution;
            float y = cubic ? SampleHermite(_points, tangents!, x) : SampleLinear(_points, x);
            table[i] = Clamp01(y);
        }
        return new CurveLut(table);
    }

    // ---- 보간 구현 (빌드 타임 전용) ----

    private static float SampleLinear(CurvePoint[] p, float x)
    {
        if (x <= p[0].X) return p[0].Y;
        int last = p.Length - 1;
        if (x >= p[last].X) return p[last].Y;

        int j = FindSegment(p, x);
        float dx = p[j + 1].X - p[j].X;
        if (dx <= float.Epsilon) return p[j + 1].Y;
        float t = (x - p[j].X) / dx;
        return p[j].Y + (p[j + 1].Y - p[j].Y) * t;
    }

    private static float SampleHermite(CurvePoint[] p, float[] m, float x)
    {
        if (x <= p[0].X) return p[0].Y;
        int last = p.Length - 1;
        if (x >= p[last].X) return p[last].Y;

        int j = FindSegment(p, x);
        float h = p[j + 1].X - p[j].X;
        if (h <= float.Epsilon) return p[j + 1].Y;

        float s = (x - p[j].X) / h;
        float s2 = s * s;
        float s3 = s2 * s;
        float h00 = 2f * s3 - 3f * s2 + 1f;
        float h10 = s3 - 2f * s2 + s;
        float h01 = -2f * s3 + 3f * s2;
        float h11 = s3 - s2;
        return h00 * p[j].Y + h10 * h * m[j] + h01 * p[j + 1].Y + h11 * h * m[j + 1];
    }

    private static int FindSegment(CurvePoint[] p, float x)
    {
        // 제어점 수가 적으므로 선형 탐색으로 충분(빌드 타임).
        for (int j = p.Length - 2; j >= 0; j--)
            if (x >= p[j].X) return j;
        return 0;
    }

    /// <summary>Fritsch–Carlson 단조 접선 계산.</summary>
    private static float[] ComputeMonotoneTangents(CurvePoint[] p)
    {
        int n = p.Length;
        var secant = new float[n - 1];
        for (int i = 0; i < n - 1; i++)
        {
            float dx = p[i + 1].X - p[i].X;
            secant[i] = dx <= float.Epsilon ? 0f : (p[i + 1].Y - p[i].Y) / dx;
        }

        var t = new float[n];
        t[0] = secant[0];
        t[n - 1] = secant[n - 2];
        for (int i = 1; i < n - 1; i++)
            t[i] = (secant[i - 1] + secant[i]) * 0.5f;

        // 단조성 강제
        for (int i = 0; i < n - 1; i++)
        {
            if (secant[i] == 0f)
            {
                t[i] = 0f;
                t[i + 1] = 0f;
                continue;
            }
            float alpha = t[i] / secant[i];
            float beta = t[i + 1] / secant[i];
            float mag = alpha * alpha + beta * beta;
            if (mag > 9f)
            {
                float tau = 3f / MathF.Sqrt(mag);
                t[i] = tau * alpha * secant[i];
                t[i + 1] = tau * beta * secant[i];
            }
        }
        return t;
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    // ---- 프리셋 ----

    /// <summary>항등 y = x.</summary>
    public static CurveDefinition Linear() => new(
        new[] { new CurvePoint(0f, 0f), new CurvePoint(1f, 1f) },
        CurveInterpolation.Linear);

    /// <summary>중앙은 둔감, 가장자리에서 급가속(에임 정밀 조준에 유용).</summary>
    public static CurveDefinition Aggressive() => new(
        new[]
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(0.5f, 0.25f),
            new CurvePoint(0.8f, 0.6f),
            new CurvePoint(1f, 1f),
        },
        CurveInterpolation.MonotoneCubic);

    /// <summary>중앙 반응이 빠르고 가장자리에서 완만(부드러운 가속).</summary>
    public static CurveDefinition Smooth() => new(
        new[]
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(0.2f, 0.4f),
            new CurvePoint(0.5f, 0.7f),
            new CurvePoint(1f, 1f),
        },
        CurveInterpolation.MonotoneCubic);
}
