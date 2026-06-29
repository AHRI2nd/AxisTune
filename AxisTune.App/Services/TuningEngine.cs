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
    private bool _deviceIsGamepad = true;
    private bool _hideActive; // 원본이 실제로 다른 앱에 숨겨졌는지
    private int _virtualUserIndex = -1; // 가상 패드의 XInput 슬롯(미보고 시 -1)
    private uint _loopTick;

    // 수동 매핑(null = 자동 게임패드 매핑). 원자적 교체.
    private volatile ControllerMapping? _mapping;
    private readonly RawJoystickState _rawState = new();

    // "눌러서 바인딩" 캡처(엔진 스레드 전용).
    private Action<CapturedInput?>? _captureCallback;
    private RawJoystickState? _captureBaseline;

    // 진동 전달: ViGEm 콜백(임의 스레드) → 엔진 스레드. -1 = 대기 없음.
    private int _pendingRumble = -1;

    // 실시간 프리뷰: 에디터가 열려 있을 때만 raw/processed를 발행(seqlock).
    private volatile bool _previewEnabled;
    private long _snapVersion;
    private XboxOutputState _snapRaw;
    private XboxOutputState _snapProcessed;

    /// <summary>상태 변경 알림(엔진 스레드에서 발생 — 구독자가 UI 스레드로 마샬링해야 함).</summary>
    public event Action<EngineStatus>? StatusChanged;

    public bool IsEnabled => _enabled;
    public bool IsRunning => _running;

    /// <summary>실시간 프리뷰 발행 여부(튜닝 화면 표시 중에만 켜서 유휴 비용 절감).</summary>
    public bool PreviewEnabled
    {
        get => _previewEnabled;
        set { _previewEnabled = value; _wake.Set(); }
    }

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
        => Post(() => callback(_sdl.EnumerateControllers()));

    public void RequestSelectDevice(uint instanceId, string deviceName, bool isGamepad, Action<bool> callback)
        => Post(() =>
        {
            _selectedDevice = instanceId;
            _selectedName = deviceName;
            _deviceIsGamepad = isGamepad;
            bool ok = OpenSelectedInternal();
            if (ok && _enabled) ReapplyHide();
            callback(ok);
            RaiseStatus();
        });

    /// <summary>활성 매핑 교체. null = 자동 게임패드 매핑. 모드가 바뀌면 장치를 재오픈.</summary>
    public void RequestSetMapping(ControllerMapping? mapping)
        => Post(() =>
        {
            bool wasManual = _mapping is not null;
            _mapping = mapping;
            bool nowManual = mapping is not null;
            // 모드(게임패드↔조이스틱)가 달라지면 재오픈 필요.
            if (wasManual != nowManual && _selectedDevice != 0)
            {
                OpenSelectedInternal();
                if (_enabled) ReapplyHide();
            }
            RaiseStatus();
        });

    /// <summary>"눌러서 바인딩": 다음으로 작동되는 물리 입력을 한 번 캡처해 콜백으로 보고.</summary>
    public void RequestCaptureInput(Action<CapturedInput?> onResult)
        => Post(() =>
        {
            if (!_sdl.HasOpenJoystick)
            {
                onResult(null);
                return;
            }
            // 기준선 스냅샷(현재 눌린 입력은 무시).
            _sdl.ReadRawState(_rawState);
            _captureBaseline = CopyRaw(_rawState);
            _captureCallback = onResult;
        });

    public void RequestCancelCapture()
        => Post(() =>
        {
            _captureCallback?.Invoke(null);
            _captureCallback = null;
            _captureBaseline = null;
        });

    public void RequestSetEnabled(bool enabled)
        => Post(() =>
        {
            if (enabled) EnableInternal();
            else DisableInternal();
        });

    /// <summary>현재 선택 장치를 적절한 모드(게임패드/조이스틱)로 연다(엔진 스레드).</summary>
    private bool OpenSelectedInternal()
    {
        if (_selectedDevice == 0) return false;
        bool manual = _mapping is not null || !_deviceIsGamepad;
        return manual
            ? _sdl.OpenJoystick(_selectedDevice)
            : _sdl.OpenGamepad(_selectedDevice);
    }

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

                bool output = _enabled && _virtual.IsConnected;
                bool capturing = _captureCallback is not null;

                // 가상 패드 XInput 슬롯 조회(미보고 시 예외 → hot path 회피 위해 ~256틱마다만 시도).
                if (output && _virtualUserIndex < 0 && (++_loopTick & 0xFF) == 0)
                {
                    int? idx = _virtual.TryGetUserIndex();
                    if (idx is >= 0)
                    {
                        _virtualUserIndex = idx.Value;
                        RaiseStatus();
                    }
                }
                // 출력/프리뷰/캡처 중 하나라도 필요할 때만 입력을 읽는다(유휴 비용 절감).
                if (_sdl.HasOpenDevice && (output || _previewEnabled || capturing))
                {
                    raw = XboxOutputState.Empty;
                    bool got;
                    if (_sdl.HasOpenJoystick)
                    {
                        got = _sdl.ReadRawState(_rawState);
                        if (got)
                        {
                            if (capturing) TryCapture();
                            (_mapping ?? ControllerMapping.Empty).Apply(_rawState, ref raw);
                        }
                    }
                    else
                    {
                        got = _sdl.ReadState(ref raw);
                    }

                    if (got)
                    {
                        XboxOutputState processed = _profile.Apply(raw);
                        if (output) _virtual.Submit(processed);
                        if (_previewEnabled) PublishSnapshot(raw, processed);
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
        _hideActive = false;
        _virtualUserIndex = -1;
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
        _hideActive = _hidHide.IsHiding;
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

    // ---- 실시간 프리뷰 스냅샷(seqlock: 쓰기는 엔진 스레드, 읽기는 UI 스레드) ----

    private void PublishSnapshot(in XboxOutputState raw, in XboxOutputState processed)
    {
        // 홀수 버전 = 쓰기 중, 짝수 = 완료. 구조체가 워드보다 커서 tearing을 막는다.
        Volatile.Write(ref _snapVersion, _snapVersion + 1);
        _snapRaw = raw;
        _snapProcessed = processed;
        Volatile.Write(ref _snapVersion, _snapVersion + 1);
    }

    /// <summary>최신 raw/processed 상태를 읽는다(UI 프리뷰용). 발행 전이면 false.</summary>
    public bool TryGetSnapshot(out XboxOutputState raw, out XboxOutputState processed)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            long v1 = Volatile.Read(ref _snapVersion);
            if ((v1 & 1) != 0) continue; // 쓰기 중
            raw = _snapRaw;
            processed = _snapProcessed;
            long v2 = Volatile.Read(ref _snapVersion);
            if (v1 == v2 && v1 != 0)
                return true;
        }
        raw = default;
        processed = default;
        return false;
    }

    // ---- "눌러서 바인딩" 캡처 ----

    private const int CaptureAxisThreshold = 12000; // ~0.37 (32767 기준)

    private void TryCapture()
    {
        var cb = _captureCallback;
        var baseline = _captureBaseline;
        if (cb is null || baseline is null) return;
        var cur = _rawState;

        int nButtons = Math.Min(cur.Buttons.Length, baseline.Buttons.Length);
        for (int i = 0; i < nButtons; i++)
            if (cur.Buttons[i] && !baseline.Buttons[i])
            {
                FinishCapture(cb, new CapturedInput(CaptureKind.Button, i, 0, default));
                return;
            }

        int nHats = Math.Min(cur.Hats.Length, baseline.Hats.Length);
        for (int i = 0; i < nHats; i++)
        {
            byte b = cur.Hats[i];
            if (b != 0 && b != baseline.Hats[i])
            {
                FinishCapture(cb, new CapturedInput(CaptureKind.Hat, i, 0, FirstHatDirection(b)));
                return;
            }
        }

        int nAxes = Math.Min(cur.Axes.Length, baseline.Axes.Length);
        for (int i = 0; i < nAxes; i++)
        {
            int delta = cur.Axes[i] - baseline.Axes[i];
            if (Math.Abs(delta) > CaptureAxisThreshold)
            {
                FinishCapture(cb, new CapturedInput(CaptureKind.Axis, i, delta > 0 ? 1 : -1, default));
                return;
            }
        }
    }

    private void FinishCapture(Action<CapturedInput?> callback, CapturedInput value)
    {
        _captureCallback = null;
        _captureBaseline = null;
        callback(value);
    }

    private static HatDirection FirstHatDirection(byte b)
    {
        if ((b & (byte)HatDirection.Up) != 0) return HatDirection.Up;
        if ((b & (byte)HatDirection.Right) != 0) return HatDirection.Right;
        if ((b & (byte)HatDirection.Down) != 0) return HatDirection.Down;
        return HatDirection.Left;
    }

    private static RawJoystickState CopyRaw(RawJoystickState s)
    {
        var copy = new RawJoystickState(s.Axes.Length, s.Buttons.Length, s.Hats.Length);
        Array.Copy(s.Axes, copy.Axes, s.Axes.Length);
        Array.Copy(s.Buttons, copy.Buttons, s.Buttons.Length);
        Array.Copy(s.Hats, copy.Hats, s.Hats.Length);
        return copy;
    }

    private void RaiseStatus(string? message = null)
    {
        EngineState state;
        if (_driverMissing) state = EngineState.DriverMissing;
        else if (!_enabled) state = EngineState.Disabled;
        else if (!_sdl.HasOpenDevice) state = EngineState.NoDevice;
        else state = EngineState.Active;

        // 동작 중인데 원본을 숨기지 못한 경우(주로 XInput 컨트롤러) 경고 키를 전달(UI에서 현지화).
        if (message is null && state == EngineState.Active && !_hideActive)
        {
            message = _hidHide.IsInstalled ? "Warn_HideFailedXInput" : "Warn_HideFailedNoHidHide";
        }

        StatusChanged?.Invoke(new EngineStatus(
            state,
            _selectedName,
            HidHideActive: _enabled && _hideActive,
            message,
            VirtualUserIndex: _virtualUserIndex));
    }

    public void Dispose()
    {
        Stop();
        _wake.Dispose();
    }
}
