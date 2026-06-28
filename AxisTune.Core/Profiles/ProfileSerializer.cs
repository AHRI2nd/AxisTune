using AxisTune.Core.Axis;
using AxisTune.Core.Controls;
using AxisTune.Core.Curves;

namespace AxisTune.Core.Profiles;

/// <summary>직렬화 DTO와 런타임 <see cref="ProcessingProfile"/>/<see cref="AxisConfig"/> 간 변환.</summary>
public static class ProfileSerializer
{
    /// <summary>DTO에서 단일 축의 런타임 설정을 만든다(곡선 LUT 빌드 포함).</summary>
    public static AxisConfig ToAxisConfig(AxisConfigDto dto, int lutResolution = CurveDefinition.DefaultResolution)
    {
        CurveLut lut = ToCurveDefinition(dto.Curve, dto.Kind).BuildLut(lutResolution);
        return new AxisConfig(
            dto.Kind, dto.InputMin, dto.InputMax,
            dto.InnerDeadzone, dto.OuterDeadzone, dto.Invert, lut);
    }

    public static CurveDefinition ToCurveDefinition(CurveDto dto, AxisKind kind)
    {
        if (dto.Points is { Count: >= 2 })
        {
            var points = new CurvePoint[dto.Points.Count];
            for (int i = 0; i < dto.Points.Count; i++)
                points[i] = new CurvePoint(dto.Points[i].X, dto.Points[i].Y);
            return new CurveDefinition(points, dto.Interpolation);
        }
        return CurveDefinition.Linear();
    }

    public static CurveDto FromCurveDefinition(CurveDefinition def)
    {
        var dto = new CurveDto { Interpolation = def.Interpolation };
        foreach (var p in def.Points)
            dto.Points.Add(new CurvePointDto { X = p.X, Y = p.Y });
        return dto;
    }

    /// <summary>전체 DTO에서 런타임 프로파일을 만든다(부족한 채널은 패스스루로 보정).</summary>
    public static ProcessingProfile ToProfile(ProfileDto dto, int lutResolution = CurveDefinition.DefaultResolution)
    {
        var axes = new AxisConfig[AxisChannelInfo.Count];
        for (int i = 0; i < AxisChannelInfo.Count; i++)
        {
            if (dto.Axes is not null && i < dto.Axes.Count && dto.Axes[i] is not null)
                axes[i] = ToAxisConfig(dto.Axes[i], lutResolution);
            else
                axes[i] = AxisConfig.Passthrough(DefaultKind(i));
        }
        return new ProcessingProfile(axes);
    }

    /// <summary>모든 채널이 패스스루(선형)인 기본 DTO.</summary>
    public static ProfileDto CreateDefault()
    {
        var dto = new ProfileDto();
        for (int i = 0; i < AxisChannelInfo.Count; i++)
        {
            dto.Axes.Add(new AxisConfigDto
            {
                Kind = DefaultKind(i),
                Curve = FromCurveDefinition(CurveDefinition.Linear()),
            });
        }
        return dto;
    }

    /// <summary>채널 인덱스의 기본 부호 특성(0~3 스틱=양극성, 4~5 트리거=단극성).</summary>
    public static AxisKind DefaultKind(int channelIndex)
        => channelIndex >= 4 ? AxisKind.Unipolar : AxisKind.Bipolar;

    // ---- 수동 매핑 변환 ----

    public static ControllerMapping ToControllerMapping(ControllerMappingDto? dto)
    {
        if (dto is null) return ControllerMapping.Empty;

        var buttons = new ButtonBinding[dto.Buttons.Count];
        for (int i = 0; i < buttons.Length; i++)
            buttons[i] = new ButtonBinding(dto.Buttons[i].PhysicalButton, dto.Buttons[i].Target);

        var hats = new HatBinding[dto.Hats.Count];
        for (int i = 0; i < hats.Length; i++)
            hats[i] = new HatBinding(dto.Hats[i].Hat, dto.Hats[i].Direction, dto.Hats[i].Target);

        var axes = new AxisBinding[dto.Axes.Count];
        for (int i = 0; i < axes.Length; i++)
            axes[i] = new AxisBinding(dto.Axes[i].PhysicalAxis, dto.Axes[i].Target, dto.Axes[i].Invert);

        return new ControllerMapping(buttons, hats, axes);
    }

    public static ControllerMappingDto FromControllerMapping(ControllerMapping mapping)
    {
        var dto = new ControllerMappingDto();
        foreach (var b in mapping.Buttons)
            dto.Buttons.Add(new ButtonBindingDto { PhysicalButton = b.PhysicalButton, Target = b.Target });
        foreach (var h in mapping.Hats)
            dto.Hats.Add(new HatBindingDto { Hat = h.Hat, Direction = h.Direction, Target = h.Target });
        foreach (var a in mapping.Axes)
            dto.Axes.Add(new AxisBindingDto { PhysicalAxis = a.PhysicalAxis, Target = a.Target, Invert = a.Invert });
        return dto;
    }

    // ---- 다중 프로파일 문서 ----

    public static NamedProfileDto CreateDefaultProfile(string name)
        => new() { Name = name, Axes = CreateDefault(), Mapping = null };

    public static ProfileDocumentDto CreateDefaultDocument()
    {
        var profile = CreateDefaultProfile("기본");
        return new ProfileDocumentDto
        {
            ActiveProfileId = profile.Id,
            Profiles = { profile },
        };
    }
}
