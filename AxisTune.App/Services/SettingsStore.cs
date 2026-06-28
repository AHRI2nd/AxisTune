using System.IO;
using System.Text.Json;

namespace AxisTune.App.Services;

/// <summary>%APPDATA%\AxisTune\settings.json 에 앱 설정을 로드/저장.</summary>
public static class SettingsStore
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AxisTune");

    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);
                if (settings is not null) return settings;
            }
        }
        catch
        {
            // 손상/접근 불가 시 기본값으로 진행.
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            string json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // 저장 실패는 무시(다음 저장 시 재시도).
        }
    }
}
