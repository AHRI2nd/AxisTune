using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace AxisTune.App.Services;

/// <summary>업데이트 확인 결과.</summary>
public sealed record UpdateInfo(string CurrentVersion, string LatestVersion, string ReleaseUrl, bool IsNewer);

/// <summary>GitHub Releases에서 최신 버전을 조회해 현재 버전과 비교한다.</summary>
public static class UpdateChecker
{
    private const string Repo = "AHRI2nd/AxisTune";

    /// <summary>현재 앱 버전("x.y.z").</summary>
    public static string CurrentVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    public static string ReleasesPage => $"https://github.com/{Repo}/releases/latest";

    /// <summary>최신 릴리스를 조회. 실패 시 null(네트워크/파싱 오류는 조용히 무시).</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("AxisTune");
            http.Timeout = TimeSpan.FromSeconds(15);

            string json = await http.GetStringAsync(
                $"https://api.github.com/repos/{Repo}/releases/latest", ct);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("tag_name", out var tagEl)) return null;

            string? tag = tagEl.GetString();
            if (string.IsNullOrWhiteSpace(tag)) return null;

            string url = root.TryGetProperty("html_url", out var urlEl) && urlEl.GetString() is { } u
                ? u
                : ReleasesPage;

            string current = CurrentVersion;
            string latest = Normalize(tag);
            return new UpdateInfo(current, latest, url, IsNewer(current, latest));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>"v1.2.3-beta" → "1.2.3" (접두 v 제거, 프리릴리스/빌드 메타 제거).</summary>
    private static string Normalize(string tag)
    {
        string s = tag.Trim();
        if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V')) s = s[1..];
        int cut = s.IndexOfAny(new[] { '-', '+' });
        if (cut >= 0) s = s[..cut];
        return s;
    }

    private static bool IsNewer(string current, string latest)
        => Version.TryParse(Pad(current), out var c)
           && Version.TryParse(Pad(latest), out var l)
           && l > c;

    // Version.TryParse는 최소 2개 컴포넌트 필요 → 부족하면 0으로 채움.
    private static string Pad(string v)
    {
        var parts = v.Split('.');
        return parts.Length switch
        {
            1 => v + ".0",
            _ => v,
        };
    }
}
