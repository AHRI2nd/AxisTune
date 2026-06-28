using System.Runtime.InteropServices;

namespace AxisTune.App.Services;

/// <summary>
/// Windows 멀티미디어 타이머 해상도 제어. 입력 루프가 도는 동안에만 1ms 해상도를
/// 요청해 <c>Thread.Sleep(1)</c>의 실제 지연을 ~15ms에서 ~1ms로 낮춘다(저지연 핵심).
/// 사용 후 반드시 <see cref="End"/>로 원복(시스템 전역 설정이므로).
/// </summary>
internal static class NativeTiming
{
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uPeriod);

    private const uint PeriodMs = 1;
    private static bool _active;

    public static void Begin()
    {
        if (_active) return;
        if (TimeBeginPeriod(PeriodMs) == 0) // TIMERR_NOERROR
            _active = true;
    }

    public static void End()
    {
        if (!_active) return;
        TimeEndPeriod(PeriodMs);
        _active = false;
    }
}
