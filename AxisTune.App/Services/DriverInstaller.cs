using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace AxisTune.App.Services;

public enum DriverKind
{
    ViGEmBus,
    HidHide,
}

/// <summary>
/// 필수 드라이버(ViGEmBus/HidHide)를 공식 GitHub 릴리스에서 받아 설치한다.
/// 앱이 관리자 권한으로 실행되므로 자식 설치 프로그램도 권한을 상속받는다.
/// 실패 시 호출부가 <see cref="OpenReleasesPage"/>로 폴백한다.
/// </summary>
public static class DriverInstaller
{
    private static string Repo(DriverKind kind) => kind switch
    {
        DriverKind.ViGEmBus => "nefarius/ViGEmBus",
        DriverKind.HidHide => "nefarius/HidHide",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string ReleasesPage(DriverKind kind)
        => $"https://github.com/{Repo(kind)}/releases/latest";

    public static void OpenReleasesPage(DriverKind kind) => OpenUrl(ReleasesPage(kind));

    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // 브라우저 실행 실패는 무시.
        }
    }

    /// <summary>
    /// 최신 릴리스의 설치 프로그램(.exe 우선, 없으면 .msi)을 받아 실행한다.
    /// 성공 시 true. 자산을 못 찾거나 네트워크 실패면 false(호출부가 페이지로 폴백).
    /// </summary>
    public static async Task<bool> DownloadAndRunAsync(
        DriverKind kind, Action<string> progress, CancellationToken ct)
    {
        progress("Drv_Busy_CheckRelease");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AxisTune");
        http.Timeout = TimeSpan.FromSeconds(60);

        string api = $"https://api.github.com/repos/{Repo(kind)}/releases/latest";
        string json = await http.GetStringAsync(api, ct);

        (string Url, string Name)? chosen = SelectInstallerAsset(json);
        if (chosen is null) return false;

        progress("Drv_Busy_Download");
        byte[] bytes = await http.GetByteArrayAsync(chosen.Value.Url, ct);

        string dir = Path.Combine(Path.GetTempPath(), "AxisTune");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, chosen.Value.Name);
        await File.WriteAllBytesAsync(path, bytes, ct);

        progress("Drv_Busy_Run");
        LaunchInstaller(path);
        return true;
    }

    private static (string Url, string Name)? SelectInstallerAsset(string releaseJson)
    {
        using var doc = JsonDocument.Parse(releaseJson);
        if (!doc.RootElement.TryGetProperty("assets", out var assets)
            || assets.ValueKind != JsonValueKind.Array)
            return null;

        (string Url, string Name)? exe = null;
        (string Url, string Name)? msi = null;

        foreach (var asset in assets.EnumerateArray())
        {
            string? name = asset.GetProperty("name").GetString();
            string? url = asset.GetProperty("browser_download_url").GetString();
            if (name is null || url is null) continue;

            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                exe ??= (url, name);
            else if (name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                msi ??= (url, name);
        }

        return exe ?? msi;
    }

    private static void LaunchInstaller(string path)
    {
        if (path.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo("msiexec", $"/i \"{path}\"") { UseShellExecute = true });
        }
        else
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
