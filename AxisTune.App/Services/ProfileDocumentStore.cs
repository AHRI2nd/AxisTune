using System.IO;
using System.Text.Json;
using AxisTune.Core.Profiles;

namespace AxisTune.App.Services;

/// <summary>%APPDATA%\AxisTune\profiles.json 에 다중 프로파일 문서를 로드/저장.</summary>
public static class ProfileDocumentStore
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AxisTune");

    private static readonly string FilePath = Path.Combine(Dir, "profiles.json");

    public static ProfileDocumentDto Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                var doc = JsonSerializer.Deserialize(json, ProfileJsonContext.Default.ProfileDocumentDto);
                if (doc is { Profiles.Count: > 0 }) return doc;
            }
        }
        catch
        {
            // 손상/접근 불가 → 기본 문서.
        }
        return ProfileSerializer.CreateDefaultDocument();
    }

    public static void Save(ProfileDocumentDto document)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            string json = JsonSerializer.Serialize(document, ProfileJsonContext.Default.ProfileDocumentDto);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // 저장 실패 무시.
        }
    }
}
