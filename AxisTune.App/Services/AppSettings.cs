using System.Text.Json.Serialization;
using AxisTune.App.Localization;

namespace AxisTune.App.Services;

/// <summary>영구 저장되는 앱 설정(Stage 1 범위). 프로파일/매핑은 Stage 2~3에서 확장.</summary>
public sealed class AppSettings
{
    /// <summary>UI 언어.</summary>
    public AppLanguage Language { get; set; } = AppLanguage.Korean;

    /// <summary>Windows 시작 시 자동 실행(실제 등록 상태는 <see cref="StartupManager"/>가 소유).</summary>
    public bool RunAtStartup { get; set; }

    /// <summary>창 닫기 시 트레이로 최소화(기본 true).</summary>
    public bool MinimizeToTrayOnClose { get; set; } = true;

    /// <summary>앱 시작 시 드라이버를 자동으로 On.</summary>
    public bool AutoEnableOnStartup { get; set; }

    /// <summary>마지막으로 선택한 장치(있으면 시작 시 자동 선택).</summary>
    public uint? LastDeviceInstanceId { get; set; }

    public string? LastDeviceName { get; set; }

    /// <summary>마지막 창 크기(DIP). null이면 기본값 사용.</summary>
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SettingsJsonContext : JsonSerializerContext
{
}
