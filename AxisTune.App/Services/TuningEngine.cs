using System.Collections.Concurrent;
using System.Diagnostics;
using AxisTune.Core.Controls;
using AxisTune.Core.Profiles;
using AxisTune.Input.Sdl;
using AxisTune.Output.HidHide;
using AxisTune.Output.ViGem;

namespace AxisTune.App.Services;

/// <summary>
/// 입력→정제→출력 전체 파이프라인을 구동하는 오케스트레이터.
///
/// 모든 SDL 호출은 단일 전용 스레드(엔진 스레드)에서만 수행한다(SDL 조이스틱 API 제약).
/// UI 스레드의 요청은 명령 큐로 마샬링되어 엔진 스레드에서 실행된다. 진동 콜백(ViGEm)은
/// 임의 스레드에서 오므로 <see cref="Interlocked"/>로 모터값만 전달하고 적용은 엔진 스레드에서 한다.
///
/// hot path(입력 읽기→<see cref="ProcessingProfile.Apply"/>→가상 제출)는 할당이 없다.
/// </summary>
public sealed class TuningEngine : IDisposable
{
    private readonly SdlGamepadService _sdl = new();
    private readonly VirtualXboxController _virtual = new();
    private readonly HidHideController _hidHide = new();

    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly ManualResetEventSlim _wake = new(false);

    private Thread? _thread;
    private volatile bool _running;
    private volatile bool _enabled;
    private volatile bool _driverMissing;

    // hot path에서 원자적으로 교체 가능한 불변 프로파일.
    private volatile ProcessingProfile _profile = ProcessingProfile.Passthrough();

    // 엔진 스레드 전용 상태.
    private uint _selectedDevice;
    private string? _selectedName;

    // 진동 전달: ViGEm 콜백(임의 스레드) → 엔진 스레드. -1 = 대기 없음.
    private int _pendingRumble = -1;

    /// <summary>상태 변경 알림(엔진 스레드에서 발생 — 구독자가 UI 스레드로 마샬링해야 함).</summary>
    public event Action<EngineStatus>? StatusChanged;

    public bool IsEnabled => _enabled;
    public bool IsRunning => _running;

    /// <summary>HidHide 드라이버 설치 여부(UI 안내용).</summary>
    public bool IsHidHideInstalled => _hidHide.IsInstalled;

    /// <summary>현재 처리 프로파일. 설정 시 다음 틱부터 적용(원자적 교체).</summary>
    public ProcessingProfile Profile
    {
        get => _profile;
        set => _profile = value ?? throw new ArgumentNullException(nameof(value));
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
            Name = "AxisTune-Engine",
        };
        _thread.Start();
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _wake.Set();
        _thread?.Join(2000);
        _thread = null;
    }

    // ---- UI 스레드에서 호출하는 비동기 요청(명령 큐 경유) ----

    public void RequestEnumerate(Action<IReadOnlyList<DetectedGamepad>> callback)
        => Post(() => callback(_sdl.EnumerateGamepads()));

    public void RequestSelectDevice(uint instanceId, string deviceName, Action<bool> callback)
        => Post(() =>
        {
            bool ok = _sdl.OpenGamepad(instanceId);
            if (ok)
            {
                _selectedDevice = instanceId;
                _selectedName = deviceName;
                if (_enabled) ReapplyHide();
            }
            callback(ok);
            RaiseStatus();
        });

    public void RequestSetEnabled(bool enabled)
        => Post(() =>
        {
            if (enabled) EnableInternal();
            else DisableInternal();
        });

    private void Post(Action command)
    {
        _commands.Enqueue(command);
        _wake.Set();
    }

    // ---- 엔진 스레드 ----

    private void ThreadMain()
    {
        if (!_sdl.Initialize())
        {
            _driverMissing = true;
            RaiseStatus("SDL 초기화 실패");
            return;
        }

        _virtual.RumbleReceived += OnRumbleReceived;
        NativeTiming.Begin();
        RaiseStatus();

        var raw = XboxOutputState.Empty;
        try
        {
            while (_running)
            {
                while (_commands.TryDequeue(out var cmd))
                {
                    try { cmd(); } catch { /* 명령 실패가 루프를 죽이지 않게 */ }
                }

                _sdl.Update();
                ForwardPendingRumble();

                if (_enabled && _virtual.IsConnected && _sdl.HasOpenDevice)
                {
                    raw = XboxOutputState.Empty;
                    if (_sdl.ReadState(ref raw))
                    {
                        XboxOutputState processed = _profile.Apply(raw);
                        _virtual.Submit(processed);
                    }
                    Thread.Sleep(1); // ~1ms (NativeTiming으로 해상도 1ms 확보)
                }
                else
                {
                    // 유휴: CPU를 양보하고 명령/진동 신호를 기다린다.
                    _wake.Wait(15);
                    _wake.Reset();
                }
            }
        }
        finally
        {
            // 정리는 반드시 같은 스레드에서(HidHide 복원 + SDL 종료).
            DisableInternal();
            _virtual.RumbleReceived -= OnRumbleReceived;
            NativeTiming.End();
            _sdl.Dispose();
        }
    }

    private void EnableInternal()
    {
        if (!_sdl.HasOpenDevice)
        {
            _enabled = true; // 의도는 On이지만 장치 없음 → NoDevice 상태로 표시
            RaiseStatus();
            return;
        }

        try
        {
            _virtual.Connect(); // ViGEmBus 미설치 시 예외
            _driverMissing = false;
        }
        catch (Exception ex)
        {
            _driverMissing = true;
            _enabled = false;
            RaiseStatus($"ViGEmBus 연결 실패: {ex.Message}");
            return;
        }

        ReapplyHide();
        _enabled = true;
        RaiseStatus();
    }

    private void DisableInternal()
    {
        try { _virtual.Disconnect(); } catch { }
        try { _hidHide.Restore(); } catch { }
        _enabled = false;
        RaiseStatus();
    }

    /// <summary>현재 선택 장치를 HidHide로 숨기고 앱을 화이트리스트에 등록.</summary>
    private void ReapplyHide()
    {
        try
        {
            _hidHide.Restore(); // 이전 숨김 해제 후 재적용
            string exe = Environment.ProcessPath ?? "";
            _hidHide.EnsureWhitelisted(exe);
            var targets = DeviceLocator.ResolveHideTargets(_sdl.GetOpenDevicePath());
            if (targets.Count > 0)
                _hidHide.HideInstances(targets);
        }
        catch { /* 숨김 실패는 치명적이지 않음(출력은 계속) */ }
    }

    private void OnRumbleReceived(byte large, byte small)
    {
        int packed = (large << 8) | small;
        Interlocked.Exchange(ref _pendingRumble, packed);
        _wake.Set();
    }

    private void ForwardPendingRumble()
    {
        int packed = Interlocked.Exchange(ref _pendingRumble, -1);
        if (packed < 0) return;
        // 바이트(0..255) → SDL ushort(0..65535) 스케일(×257).
        ushort low = (ushort)(((packed >> 8) & 0xFF) * 257);
        ushort high = (ushort)((packed & 0xFF) * 257);
        _sdl.SendRumble(low, high);
    }

    private void RaiseStatus(string? message = null)
    {
        EngineState state;
        if (_driverMissing) state = EngineState.DriverMissing;
        else if (!_enabled) state = EngineState.Disabled;
        else if (!_sdl.HasOpenDevice) state = EngineState.NoDevice;
        else state = EngineState.Active;

        StatusChanged?.Invoke(new EngineStatus(
            state,
            _selectedName,
            HidHideActive: _enabled && _hidHide.IsOperational,
            message));
    }

    public void Dispose()
    {
        Stop();
        _wake.Dispose();
    }
}
