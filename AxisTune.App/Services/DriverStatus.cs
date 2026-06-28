using Nefarius.ViGEm.Client;

namespace AxisTune.App.Services;

/// <summary>드라이버 설치/사용 가능 여부 점검(UI 안내용).</summary>
public static class DriverStatus
{
    /// <summary>ViGEmBus가 설치되어 가상 패드를 만들 수 있는지.</summary>
    public static bool IsViGEmAvailable()
    {
        try
        {
            using var client = new ViGEmClient();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
