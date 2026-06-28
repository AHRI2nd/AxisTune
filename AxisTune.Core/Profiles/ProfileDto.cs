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

// ---- 수동 매핑 직렬화 ----

public sealed class ButtonBindingDto
{
    public int PhysicalButton { get; set; }
    public XboxButton Target { get; set; }
}

public sealed class HatBindingDto
{
    public int Hat { get; set; }
    public HatDirection Direction { get; set; }
    public XboxButton Target { get; set; }
}

public sealed class AxisBindingDto
{
    public int PhysicalAxis { get; set; }
    public AxisChannel Target { get; set; }
    public bool Invert { get; set; }
}

public sealed class ControllerMappingDto
{
    public List<ButtonBindingDto> Buttons { get; set; } = new();
    public List<HatBindingDto> Hats { get; set; } = new();
    public List<AxisBindingDto> Axes { get; set; } = new();
}

// ---- 이름 있는 프로파일 + 다중 프로파일 문서 ----

/// <summary>축 설정 + (선택적) 수동 매핑을 담는 이름 있는 프로파일.</summary>
public sealed class NamedProfileDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "프로파일";
    public ProfileDto Axes { get; set; } = new();

    /// <summary>null이면 자동 게임패드 매핑 사용, 있으면 수동 매핑 적용.</summary>
    public ControllerMappingDto? Mapping { get; set; }
}

/// <summary>모든 프로파일과 활성 프로파일 ID를 담는 최상위 문서.</summary>
public sealed class ProfileDocumentDto
{
    public string? ActiveProfileId { get; set; }
    public List<NamedProfileDto> Profiles { get; set; } = new();
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(ProfileDocumentDto))]
[JsonSerializable(typeof(ProfileDto))]
public partial class ProfileJsonContext : JsonSerializerContext
{
}
