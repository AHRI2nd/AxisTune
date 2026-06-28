namespace AxisTune.App.Services;

/// <summary>엔진의 현재 동작 단계(트레이 아이콘/상태 텍스트 표시용).</summary>
public enum EngineState
{
    /// <summary>드라이버 Off(가상 출력 안 함).</summary>
    Disabled,

    /// <summary>드라이버 On이지만 입력 장치가 선택되지 않음.</summary>
    NoDevice,

    /// <summary>드라이버 On, 정상 동작 중(정제된 가상 입력 전달).</summary>
    Active,

    /// <summary>ViGEmBus 미설치/접근 불가로 가상 출력 불가.</summary>
    DriverMissing,
}

/// <summary>UI로 전달되는 엔진 상태 스냅샷(불변).</summary>
public readonly record struct EngineStatus(
    EngineState State,
    string? DeviceName,
    bool HidHideActive,
    string? Message);
