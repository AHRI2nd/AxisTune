namespace AxisTune.Core.Curves;

/// <summary>
/// 사전계산된 응답 곡선 룩업 테이블. 실시간 경로에서 <see cref="Evaluate"/>는
/// 배열 조회 + 1회 선형 보간만 수행하므로 할당이 없고 분기가 적다.
/// 불변(immutable)이므로 여러 스레드에서 안전하게 공유/원자적 교체 가능.
/// </summary>
public sealed class CurveLut
{
    private readonly float[] _table; // 길이 = Resolution + 1, 도메인 [0,1] 균등 샘플
    private readonly int _maxIndex;

    internal CurveLut(float[] table)
    {
        if (table.Length < 2)
            throw new ArgumentException("LUT는 최소 2개 엔트리가 필요합니다.", nameof(table));
        _table = table;
        _maxIndex = table.Length - 1;
    }

    public int Resolution => _maxIndex;

    /// <summary>입력 x([0,1])에 대한 곡선 출력([0,1])을 보간하여 반환.</summary>
    public float Evaluate(float x)
    {
        if (x <= 0f) return _table[0];
        if (x >= 1f) return _table[_maxIndex];

        float pos = x * _maxIndex;
        int i = (int)pos;
        float frac = pos - i;
        float a = _table[i];
        float b = _table[i + 1];
        return a + (b - a) * frac;
    }

    /// <summary>테이블 원본에 대한 읽기 전용 접근(시각화/테스트용).</summary>
    public ReadOnlySpan<float> Table => _table;

    /// <summary>항등(identity) 곡선 y = x. 공유 인스턴스.</summary>
    public static CurveLut Identity { get; } = BuildIdentity(256);

    private static CurveLut BuildIdentity(int resolution)
    {
        var table = new float[resolution + 1];
        for (int i = 0; i <= resolution; i++)
            table[i] = (float)i / resolution;
        return new CurveLut(table);
    }
}
