using System.Text.Json.Serialization;
using AxisTune.Core.Axis;
using AxisTune.Core.Controls;
using AxisTune.Core.Curves;

namespace AxisTune.Core.Profiles;

/// <summary>직렬화용 제어점.</summary>
public sealed class CurvePointDto
{
    public float X { get; set; }
    public float Y { get; set; }
}

/// <summary>직렬화용 곡선 정의.</summary>
public sealed class CurveDto
{
    public CurveInterpolation Interpolation { get; set; } = CurveInterpolation.MonotoneCubic;
    public List<CurvePointDto> Points { get; set; } = new();
}

/// <summary>직렬화용 단일 축 설정.</summary>
public sealed class AxisConfigDto
{
    public AxisKind Kind { get; set; }
    public float InputMin { get; set; }
    public float InputMax { get; set; } = 1f;
    public float InnerDeadzone { get; set; }
    public float OuterDeadzone { get; set; }
    public bool Invert { get; set; }
    public CurveDto Curve { get; set; } = new();
}

/// <summary>직렬화용 처리 프로파일(6개 채널).</summary>
public sealed class ProfileDto
{
    public List<AxisConfigDto> Axes { get; set; } = new();
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(ProfileDto))]
public partial class ProfileJsonContext : JsonSerializerContext
{
}
