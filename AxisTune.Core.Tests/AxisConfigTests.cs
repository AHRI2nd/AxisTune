using AxisTune.Core.Axis;
using AxisTune.Core.Curves;

namespace AxisTune.Core.Tests;

public class AxisConfigTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(-0.5f)]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void Passthrough_Bipolar_ReturnsInput(float v)
    {
        var cfg = AxisConfig.Passthrough(AxisKind.Bipolar);
        Assert.Equal(v, cfg.Process(v), 3);
    }

    [Fact]
    public void InnerDeadzone_ZeroesSmallInputs_AndRescales()
    {
        var cfg = new AxisConfig(AxisKind.Unipolar, innerDeadzone: 0.2f);
        Assert.Equal(0f, cfg.Process(0.1f), 3);   // 데드존 내부
        Assert.Equal(0f, cfg.Process(0.2f), 3);   // 경계
        Assert.Equal(1f, cfg.Process(1f), 3);     // 최대 유지
        // 0.6 → (0.6-0.2)/0.8 = 0.5
        Assert.Equal(0.5f, cfg.Process(0.6f), 2);
    }

    [Fact]
    public void OuterDeadzone_SaturatesNearMax()
    {
        var cfg = new AxisConfig(AxisKind.Unipolar, outerDeadzone: 0.1f);
        Assert.Equal(1f, cfg.Process(0.9f), 3);
        Assert.Equal(1f, cfg.Process(0.95f), 3);
    }

    [Fact]
    public void InputRange_RemapsCalibrationWindow()
    {
        var cfg = new AxisConfig(AxisKind.Unipolar, inputMin: 0.1f, inputMax: 0.9f);
        Assert.Equal(0f, cfg.Process(0.1f), 3);
        Assert.Equal(1f, cfg.Process(0.9f), 3);
        Assert.Equal(0.5f, cfg.Process(0.5f), 2); // (0.5-0.1)/0.8
    }

    [Fact]
    public void Invert_Bipolar_FlipsDirection()
    {
        var cfg = new AxisConfig(AxisKind.Bipolar, invert: true);
        Assert.Equal(-0.5f, cfg.Process(0.5f), 3);
        Assert.Equal(0.7f, cfg.Process(-0.7f), 3);
    }

    [Fact]
    public void Invert_Unipolar_FlipsOutput()
    {
        var cfg = new AxisConfig(AxisKind.Unipolar, invert: true);
        Assert.Equal(0.7f, cfg.Process(0.3f), 3);
        Assert.Equal(1f, cfg.Process(0f), 3);
    }

    [Fact]
    public void Bipolar_PreservesSign_WithCurve()
    {
        var cfg = new AxisConfig(AxisKind.Bipolar, curve: CurveDefinition.Aggressive().BuildLut());
        Assert.True(cfg.Process(0.5f) > 0f);
        Assert.True(cfg.Process(-0.5f) < 0f);
        // 곡선 대칭: |f(x)| == |f(-x)|
        Assert.Equal(MathF.Abs(cfg.Process(0.5f)), MathF.Abs(cfg.Process(-0.5f)), 3);
    }

    [Fact]
    public void Output_StaysWithinBounds()
    {
        var cfg = new AxisConfig(AxisKind.Bipolar, curve: CurveDefinition.Aggressive().BuildLut());
        for (float x = -1f; x <= 1f; x += 0.05f)
        {
            float y = cfg.Process(x);
            Assert.InRange(y, -1f, 1f);
        }
    }

    [Fact]
    public void DegenerateInputRange_DoesNotThrowOrNaN()
    {
        var cfg = new AxisConfig(AxisKind.Unipolar, inputMin: 0.8f, inputMax: 0.8f);
        float y = cfg.Process(0.8f);
        Assert.False(float.IsNaN(y));
        Assert.InRange(y, 0f, 1f);
    }
}
