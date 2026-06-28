using AxisTune.Core.Curves;

namespace AxisTune.Core.Tests;

public class CurveTests
{
    [Fact]
    public void Identity_MapsEndpointsAndMidpoint()
    {
        var lut = CurveLut.Identity;
        Assert.Equal(0f, lut.Evaluate(0f), 3);
        Assert.Equal(1f, lut.Evaluate(1f), 3);
        Assert.Equal(0.5f, lut.Evaluate(0.5f), 2);
    }

    [Fact]
    public void Evaluate_ClampsOutOfRangeInput()
    {
        var lut = CurveLut.Identity;
        Assert.Equal(0f, lut.Evaluate(-5f), 3);
        Assert.Equal(1f, lut.Evaluate(5f), 3);
    }

    [Fact]
    public void Linear_Preset_IsIdentity()
    {
        var lut = CurveDefinition.Linear().BuildLut();
        for (float x = 0f; x <= 1f; x += 0.05f)
            Assert.Equal(x, lut.Evaluate(x), 2);
    }

    [Theory]
    [InlineData(CurveInterpolation.Linear)]
    [InlineData(CurveInterpolation.MonotoneCubic)]
    public void BuiltLut_EndpointsAreZeroAndOne(CurveInterpolation interp)
    {
        var def = new CurveDefinition(
            new[] { new CurvePoint(0f, 0f), new CurvePoint(0.5f, 0.3f), new CurvePoint(1f, 1f) },
            interp);
        var lut = def.BuildLut();
        Assert.Equal(0f, lut.Evaluate(0f), 3);
        Assert.Equal(1f, lut.Evaluate(1f), 3);
    }

    [Fact]
    public void MonotoneCubic_IsNonDecreasing()
    {
        // 가운데가 처지는 제어점이라도 단조성이 깨지지 않아야 한다.
        var def = CurveDefinition.Aggressive();
        var lut = def.BuildLut(512);
        var table = lut.Table;
        for (int i = 1; i < table.Length; i++)
            Assert.True(table[i] >= table[i - 1] - 1e-4f,
                $"단조성 위반: index {i} ({table[i]} < {table[i - 1]})");
    }

    [Fact]
    public void Smooth_RisesFasterThanLinearNearCenter()
    {
        var smooth = CurveDefinition.Smooth().BuildLut();
        // 중앙 부근에서 출력이 입력보다 크다(빠른 초기 반응).
        Assert.True(smooth.Evaluate(0.2f) > 0.2f);
    }
}
