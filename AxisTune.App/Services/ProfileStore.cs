using System.IO;
using System.Text.Json;
using AxisTune.Core.Profiles;

namespace AxisTune.App.Services;

/// <summary>%APPDATA%\AxisTune\profile.json 에 처리 프로파일(DTO)을 로드/저장.</summary>
public static class ProfileStore
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AxisTune");

    private static readonly string FilePath = Path.Combine(Dir, "profile.json");

    public static ProfileDto Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                var dto = JsonSerializer.Deserialize(json, ProfileJsonContext.Default.ProfileDto);
                if (dto is not null) return dto;
            }
        }
        catch
        {
            // 손상/접근 불가 → 기본값.
        }
        return ProfileSerializer.CreateDefault();
    }

    public static void Save(ProfileDto dto)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            string json = JsonSerializer.Serialize(dto, ProfileJsonContext.Default.ProfileDto);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // 저장 실패 무시.
        }
    }
}
