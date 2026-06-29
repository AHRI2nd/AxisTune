using AxisTune.Core.Controls;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace AxisTune.Output.ViGem;

/// <summary>
/// ViGEmBus 기반 가상 Xbox 360 컨트롤러. 정규화된 <see cref="XboxOutputState"/>를
/// XUSB(short/byte)로 변환해 제출하고, 게임이 보낸 진동 피드백을 이벤트로 노출한다.
///
/// <see cref="Submit"/>는 hot path에서 호출되며 할당이 없다(<c>AutoSubmitReport=false</c> +
/// 틱당 1회 <c>SubmitReport</c>로 보고서 1개만 전송).
/// </summary>
public sealed class VirtualXboxController : IDisposable
{
    private ViGEmClient? _client;
    private IXbox360Controller? _pad;

    /// <summary>게임→가상 패드로 들어온 진동 명령(large, small 모터 0..255).</summary>
    public event Action<byte, byte>? RumbleReceived;

    public bool IsConnected => _pad is not null;

    /// <summary>ViGEmBus에 연결하고 가상 패드를 플러그인한다. ViGEmBus 미설치 시 예외.</summary>
    public void Connect()
    {
        if (_pad is not null) return;

        _client = new ViGEmClient();
        var pad = _client.CreateXbox360Controller();
        pad.AutoSubmitReport = false; // 틱당 1회만 명시적으로 제출(중복 보고 방지)
        pad.FeedbackReceived += OnFeedback;
        pad.Connect();
        _pad = pad;
    }

    /// <summary>정제된 상태를 가상 패드에 1회 제출(hot path).</summary>
    public void Submit(in XboxOutputState state)
    {
        var pad = _pad;
        if (pad is null) return;

        pad.LeftThumbX = ToStickAxis(state.LeftStickX);
        pad.LeftThumbY = ToStickAxis(state.LeftStickY);
        pad.RightThumbX = ToStickAxis(state.RightStickX);
        pad.RightThumbY = ToStickAxis(state.RightStickY);
        pad.LeftTrigger = ToTrigger(state.LeftTrigger);
        pad.RightTrigger = ToTrigger(state.RightTrigger);
        // XboxButton 값은 XUSB wButtons 비트마스크와 동일 → 그대로 캐스팅.
        pad.SetButtonsFull((ushort)state.Buttons);
        pad.SubmitReport();
    }

    /// <summary>
    /// 이 가상 패드에 할당된 XInput 슬롯(User Index, 0~3)을 반환. 아직 게임/시스템이
    /// 읽지 않아 미보고 상태면 null(ViGEm이 예외를 던짐).
    /// </summary>
    public int? TryGetUserIndex()
    {
        var pad = _pad;
        if (pad is null) return null;
        try { return pad.UserIndex; }
        catch { return null; }
    }

    public void Disconnect()
    {
        if (_pad is not null)
        {
            _pad.FeedbackReceived -= OnFeedback;
            try { _pad.Disconnect(); }
            catch { /* 이미 분리되었거나 버스가 사라진 경우 무시 */ }
            _pad = null;
        }
        _client?.Dispose();
        _client = null;
    }

    private void OnFeedback(object? sender, Xbox360FeedbackReceivedEventArgs e)
        => RumbleReceived?.Invoke(e.LargeMotor, e.SmallMotor);

    // [-1,1] → [-32768,32767]
    private static short ToStickAxis(float v)
    {
        int s = (int)MathF.Round(v * 32767f);
        if (s > 32767) s = 32767;
        else if (s < -32768) s = -32768;
        return (short)s;
    }

    // [0,1] → [0,255]
    private static byte ToTrigger(float v)
    {
        int b = (int)MathF.Round(v * 255f);
        if (b > 255) b = 255;
        else if (b < 0) b = 0;
        return (byte)b;
    }

    public void Dispose() => Disconnect();
}
