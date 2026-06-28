using AxisTune.Core.Controls;
using SDL;
using static SDL.SDL3;

namespace AxisTune.Input.Sdl;

/// <summary>
/// SDL3 기반 물리 컨트롤러 입력 서비스. 장치 열거·자동 분류, 단일 장치 열기,
/// 매 틱 상태 읽기(정규화), 물리 장치로의 진동 출력을 담당한다.
///
/// 스레드 안전성: <see cref="Initialize"/>·<see cref="ReadState"/>·<see cref="SendRumble"/>는
/// 동일한 입력 스레드에서 호출하는 것을 전제로 한다(SDL 조이스틱 API는 단일 스레드 사용 권장).
/// 진동 콜백이 다른 스레드에서 올 수 있으므로 <see cref="SendRumble"/>만 내부 락으로 보호한다.
/// </summary>
public sealed unsafe class SdlGamepadService : IDisposable
{
    // SDL 축 정수값(-32768..32767, 트리거 0..32767)을 정규화하기 위한 역수.
    private const float AxisScale = 1f / 32767f;

    // (SDL 버튼, 논리 Xbox 버튼) 매핑 테이블. 정적·불변이라 hot path에서 공유.
    private static readonly (SDL_GamepadButton Sdl, XboxButton Xbox)[] ButtonMap =
    {
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH, XboxButton.A),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST, XboxButton.B),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST, XboxButton.X),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH, XboxButton.Y),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK, XboxButton.Back),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START, XboxButton.Start),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE, XboxButton.Guide),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK, XboxButton.LeftThumb),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_STICK, XboxButton.RightThumb),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER, XboxButton.LeftShoulder),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER, XboxButton.RightShoulder),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP, XboxButton.DpadUp),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN, XboxButton.DpadDown),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT, XboxButton.DpadLeft),
        (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT, XboxButton.DpadRight),
    };

    private readonly object _rumbleSync = new();
    private SDL_Gamepad* _gamepad;
    private uint _openInstanceId;
    private bool _initialized;

    public bool IsInitialized => _initialized;
    public uint OpenInstanceId => _openInstanceId;
    public bool HasOpenDevice => _gamepad is not null;

    /// <summary>SDL을 게임패드 모드로 초기화. 입력 스레드에서 1회 호출.</summary>
    public bool Initialize()
    {
        if (_initialized) return true;

        // 힌트는 SDL_Init 이전에 설정해야 적용된다.
        // - 백그라운드 입력: 앱이 비포커스(게임이 포커스)일 때도 입력을 받기 위해 필수.
        SDL_SetHint(SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1");
        // - DualSense 등에서 풀 진동/확장 리포트 활성화.
        SDL_SetHint(SDL_HINT_JOYSTICK_ENHANCED_REPORTS, "1");
        // - Joy-Con 한 쌍을 하나의 게임패드로 결합.
        SDL_SetHint(SDL_HINT_JOYSTICK_HIDAPI_COMBINE_JOY_CONS, "1");

        if (!SDL_Init(SDL_InitFlags.SDL_INIT_GAMEPAD))
            return false;

        _initialized = true;
        return true;
    }

    /// <summary>SDL 내부 상태/이벤트를 갱신. 폴링 루프 매 틱 시작에 호출.</summary>
    public void Update() => SDL_UpdateGamepads();

    /// <summary>현재 연결된 게임패드 목록을 반환. virtualXbox는 ViGEm 가상 패드(루프백) 포함 여부.</summary>
    public IReadOnlyList<DetectedGamepad> EnumerateGamepads(bool includeVirtual = false)
    {
        var result = new List<DetectedGamepad>();
        using var ids = SDL_GetGamepads();
        if (ids is null) return result;
        for (int i = 0; i < ids.Count; i++)
        {
            var id = ids[i];
            uint instanceId = (uint)id;
            ushort vendor = SDL_GetGamepadVendorForID(id);
            ushort product = SDL_GetGamepadProductForID(id);
            var kind = MapKind(SDL_GetGamepadTypeForID(id));
            string name = SDL_GetGamepadNameForID(id) ?? "Unknown";

            var pad = new DetectedGamepad(instanceId, name, kind, vendor, product);
            if (!includeVirtual && pad.IsLikelyVirtualXbox)
                continue; // 가상 Xbox 패드는 입력원에서 제외(루프백 차단)
            result.Add(pad);
        }
        return result;
    }

    /// <summary>지정 장치를 입력원으로 연다(기존 장치는 닫는다).</summary>
    public bool OpenGamepad(uint instanceId)
    {
        CloseGamepad();
        var gp = SDL_OpenGamepad((SDL_JoystickID)instanceId);
        if (gp is null) return false;
        _gamepad = gp;
        _openInstanceId = instanceId;
        return true;
    }

    /// <summary>열린 장치의 현재 상태를 정규화하여 채운다(hot path). 장치 없으면 false.</summary>
    public bool ReadState(ref XboxOutputState state)
    {
        var gp = _gamepad;
        if (gp is null) return false;

        state.LeftStickX = Norm(SDL_GetGamepadAxis(gp, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX));
        // SDL은 아래쪽이 양수 → up = +1 규약에 맞춰 부호 반전.
        state.LeftStickY = -Norm(SDL_GetGamepadAxis(gp, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY));
        state.RightStickX = Norm(SDL_GetGamepadAxis(gp, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTX));
        state.RightStickY = -Norm(SDL_GetGamepadAxis(gp, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTY));
        state.LeftTrigger = NormTrigger(SDL_GetGamepadAxis(gp, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFT_TRIGGER));
        state.RightTrigger = NormTrigger(SDL_GetGamepadAxis(gp, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHT_TRIGGER));

        XboxButton buttons = XboxButton.None;
        var map = ButtonMap;
        for (int i = 0; i < map.Length; i++)
        {
            if (SDL_GetGamepadButton(gp, map[i].Sdl))
                buttons |= map[i].Xbox;
        }
        state.Buttons = buttons;
        return true;
    }

    /// <summary>열린 장치의 HID 인터페이스 경로(symbolic link)를 반환. HidHide 숨김 대상 해석에 사용.</summary>
    public string? GetOpenDevicePath()
    {
        var gp = _gamepad;
        if (gp is null) return null;
        return SDL_GetGamepadPath(gp);
    }

    /// <summary>열린 물리 장치에 진동을 전달한다(게임→가상→물리 왕복의 마지막 단계).</summary>
    public void SendRumble(ushort lowFrequency, ushort highFrequency, uint durationMs = 0)
    {
        lock (_rumbleSync)
        {
            var gp = _gamepad;
            if (gp is null) return;
            // durationMs=0은 다음 진동 명령까지 무한 지속(우리는 매 변화 시 갱신).
            SDL_RumbleGamepad(gp, lowFrequency, highFrequency, durationMs == 0 ? 0xFFFFFFFFu : durationMs);
        }
    }

    public void CloseGamepad()
    {
        lock (_rumbleSync)
        {
            if (_gamepad is not null)
            {
                SDL_CloseGamepad(_gamepad);
                _gamepad = null;
            }
            _openInstanceId = 0;
        }
    }

    private static float Norm(short v)
    {
        float n = v * AxisScale;
        return n < -1f ? -1f : (n > 1f ? 1f : n);
    }

    private static float NormTrigger(short v)
    {
        if (v <= 0) return 0f;
        float n = v * AxisScale;
        return n > 1f ? 1f : n;
    }

    private static GamepadKind MapKind(SDL_GamepadType type) => type switch
    {
        SDL_GamepadType.SDL_GAMEPAD_TYPE_XBOX360 or SDL_GamepadType.SDL_GAMEPAD_TYPE_XBOXONE
            => GamepadKind.Xbox,
        SDL_GamepadType.SDL_GAMEPAD_TYPE_PS3 or SDL_GamepadType.SDL_GAMEPAD_TYPE_PS4
            or SDL_GamepadType.SDL_GAMEPAD_TYPE_PS5
            => GamepadKind.PlayStation,
        SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_PRO
            => GamepadKind.SwitchPro,
        SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_PAIR
            => GamepadKind.JoyconPair,
        SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_LEFT
            or SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_RIGHT
            => GamepadKind.JoyconSingle,
        SDL_GamepadType.SDL_GAMEPAD_TYPE_STANDARD
            => GamepadKind.Standard,
        _ => GamepadKind.Unknown,
    };

    public void Dispose()
    {
        CloseGamepad();
        if (_initialized)
        {
            SDL_Quit();
            _initialized = false;
        }
    }
}
