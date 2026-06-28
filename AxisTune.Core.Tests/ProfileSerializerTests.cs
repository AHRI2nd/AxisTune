using AxisTune.Core.Axis;
using AxisTune.Core.Controls;
using AxisTune.Core.Curves;
using AxisTune.Core.Profiles;

namespace AxisTune.Core.Tests;

public class ProfileSerializerTests
{
    [Fact]
    public void CreateDefault_HasSixChannels_WithCorrectKinds()
    {
        var dto = ProfileSerializer.CreateDefault();
        Assert.Equal(AxisChannelInfo.Count, dto.Axes.Count);
        Assert.Equal(AxisKind.Bipolar, dto.Axes[(int)AxisChannel.LeftStickX].Kind);
        Assert.Equal(AxisKind.Unipolar, dto.Axes[(int)AxisChannel.LeftTrigger].Kind);
    }

    [Fact]
    public void DefaultProfile_IsPassthrough()
    {
        var profile = ProfileSerializer.ToProfile(ProfileSerializer.CreateDefault());
        var state = new XboxOutputState { LeftStickX = 0.4f, LeftTrigger = 0.7f };
        var result = profile.Apply(state);
        Assert.Equal(0.4f, result.LeftStickX, 2);
        Assert.Equal(0.7f, result.LeftTrigger, 2);
    }

    [Fact]
    public void ToAxisConfig_AppliesDeadzoneAndInvert()
    {
        var dto = new AxisConfigDto
        {
            Kind = AxisKind.Unipolar,
            InnerDeadzone = 0.2f,
            Invert = false,
            Curve = ProfileSerializer.FromCurveDefinition(CurveDefinition.Linear()),
        };
        var cfg = ProfileSerializer.ToAxisConfig(dto);
        Assert.Equal(0f, cfg.Process(0.15f), 3);
        Assert.Equal(1f, cfg.Process(1f), 3);
    }

    [Fact]
    public void Curve_RoundTrips_ThroughDto()
    {
        var original = CurveDefinition.Aggressive();
        var dto = ProfileSerializer.FromCurveDefinition(original);
        var restored = ProfileSerializer.ToCurveDefinition(dto, AxisKind.Bipolar);

        var a = original.BuildLut(256);
        var b = restored.BuildLut(256);
        for (float x = 0f; x <= 1f; x += 0.1f)
            Assert.Equal(a.Evaluate(x), b.Evaluate(x), 3);
    }

    [Fact]
    public void ToProfile_FillsMissingChannelsWithPassthrough()
    {
        var dto = new ProfileDto(); // 채널 0개
        var profile = ProfileSerializer.ToProfile(dto);
        var state = new XboxOutputState { RightStickY = -0.6f };
        var result = profile.Apply(state);
        Assert.Equal(-0.6f, result.RightStickY, 2);
    }
}
