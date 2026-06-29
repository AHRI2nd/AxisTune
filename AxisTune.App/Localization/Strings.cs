namespace AxisTune.App.Localization;

/// <summary>한국어/영어 문자열 테이블. 키가 없으면 키 자체를 반환(누락 가시화).</summary>
internal static class Strings
{
    public static string Get(AppLanguage language, string key)
    {
        var table = language == AppLanguage.English ? En : Ko;
        return table.TryGetValue(key, out var value) ? value : key;
    }

    private static readonly Dictionary<string, string> Ko = new()
    {
        // 헤더 / 상태
        ["Header_Driver"] = "드라이버",
        ["Status_Ready"] = "준비",
        ["Status_Active"] = "동작 중 — 정제된 가상 입력 전달",
        ["Status_NoDevice"] = "장치를 선택하세요",
        ["Status_DriverMissing"] = "ViGEmBus 설치 필요",
        ["Status_Disabled"] = "드라이버 꺼짐",
        ["Detail_HidHideActive"] = "물리 장치 숨김 활성",
        ["Warn_HideFailedXInput"] = "원본 입력을 숨기지 못했습니다 — XInput 컨트롤러는 숨길 수 없습니다(DInput 모드 권장).",
        ["Warn_HideFailedNoHidHide"] = "원본 입력을 숨기지 못했습니다 — HidHide가 설치되어 있지 않습니다.",
        ["Slot_Known"] = "가상 패드: XInput 슬롯 {0}",
        ["Slot_Pending"] = "가상 패드: XInput 슬롯 확인 중… (게임이 읽으면 표시)",

        // 배너
        ["Banner_DriversMissing"] = "⚠ 필수 드라이버가 설치되어 있지 않습니다. 드라이버 탭에서 설치하세요.",
        ["Banner_InstallDrivers"] = "드라이버 설치",

        // 업데이트
        ["Update_Available"] = "새 버전 {0} 사용 가능 (현재 {1})",
        ["Update_Download"] = "다운로드",
        ["Update_Dismiss"] = "닫기",
        ["Set_CheckUpdates"] = "시작 시 업데이트 확인",
        ["Set_CheckUpdates_Sub"] = "GitHub 릴리스에서 새 버전 확인",
        ["Update_CheckNow"] = "지금 확인",
        ["Update_Current"] = "현재 버전 {0}",
        ["Update_UpToDate"] = "최신 버전입니다",
        ["Update_Checking"] = "확인 중…",

        // 프로파일 바
        ["Profile_Label"] = "프로파일",
        ["Profile_Add"] = "추가",
        ["Profile_Delete"] = "삭제",
        ["Profile_NewName"] = "프로파일 {0}",

        // 탭
        ["Tab_Devices"] = "장치",
        ["Tab_Tuning"] = "튜닝",
        ["Tab_Mapping"] = "매핑",
        ["Tab_Drivers"] = "드라이버",

        // 장치 탭
        ["Dev_InputDevice"] = "입력 장치",
        ["Dev_Refresh"] = "새로고침",
        ["Dev_Placeholder"] = "감지된 컨트롤러를 선택하세요",
        ["Dev_AutoNote"] = "Xbox · PlayStation · Switch(Pro/Joy-Con)는 자동 감지됩니다.",
        ["Settings_Title"] = "설정",
        ["Settings_ProfileName"] = "활성 프로파일 이름",
        ["Settings_Language"] = "언어 / Language",
        ["Set_RunAtStartup"] = "Windows 시작 시 자동 실행",
        ["Set_RunAtStartup_Sub"] = "로그인 시 백그라운드(트레이)로 실행",
        ["Set_MinTray"] = "창 닫기 시 트레이로 최소화",
        ["Set_MinTray_Sub"] = "끄면 닫기(X) 시 완전 종료",
        ["Set_AutoEnable"] = "시작 시 드라이버 자동 On",
        ["Set_AutoEnable_Sub"] = "앱 실행과 동시에 가상 출력 활성화",

        // 튜닝 탭
        ["Tune_CurveHint"] = "더블클릭: 점 추가 · 드래그: 이동 · 우클릭: 점 삭제",
        ["Tune_PresetLinear"] = "프리셋: 선형",
        ["Tune_PresetAggressive"] = "공격적",
        ["Tune_PresetSmooth"] = "부드럽게",
        ["Tune_InputMin"] = "입력 최소",
        ["Tune_InputMax"] = "입력 최대",
        ["Tune_InnerDeadzone"] = "중앙 데드존",
        ["Tune_OuterDeadzone"] = "외곽 데드존",
        ["Tune_Interp"] = "곡선 보간",
        ["Tune_Interp_Linear"] = "선형",
        ["Tune_Interp_Cubic"] = "부드럽게(단조 3차)",
        ["Tune_Invert"] = "축 반전",
        ["Tune_PreviewTitle"] = "실시간 미리보기",
        ["Tune_PreviewIn"] = "입력 ",
        ["Tune_PreviewArrow"] = "   →   출력 ",
        ["Tune_PreviewNote"] = "튜닝 탭에서 컨트롤러를 움직이면 곡선 위에 현재 위치가 표시됩니다.",

        // 매핑 탭
        ["Map_Use"] = "수동 매핑 사용",
        ["Map_Use_Sub"] = "미인식 컨트롤러를 Xbox 컨트롤로 직접 지정합니다.",
        ["Map_Hint"] = "[바인딩]을 누른 뒤 물리 버튼/축을 움직이면 해당 입력이 지정됩니다. (장치 선택 필요)",
        ["Map_Buttons"] = "버튼",
        ["Map_ClearAll"] = "전체 지우기",
        ["Map_Axes"] = "축(스틱·트리거)",
        ["Map_Bind"] = "바인딩",
        ["Map_Waiting"] = "대기...",
        ["Map_None"] = "(없음)",
        ["Map_Btn"] = "버튼 {0}",
        ["Map_Hat"] = "햇 {0} {1}",
        ["Map_Axis"] = "축 {0}{1}",
        ["MapT_LS"] = "LS(누름)",
        ["MapT_RS"] = "RS(누름)",

        // 다이어그램 컨텍스트 패널
        ["Diagram_Hint"] = "게임패드에서 컨트롤을 클릭해 설정하세요",
        ["Diagram_ButtonInMapping"] = "버튼은 매핑 탭에서 설정합니다",
        ["Diagram_AxisToEdit"] = "편집할 축",

        // 드라이버 탭
        ["Drv_Intro"] = "AxisTune는 두 개의 오픈소스 드라이버(Nefarius)를 사용합니다. 아래 [설치]를 누르면 공식 릴리스를 받아 설치 프로그램을 실행합니다. 설치 후 재부팅이 필요할 수 있습니다.",
        ["Drv_Install"] = "설치",
        ["Drv_DownloadPage"] = "다운로드 페이지",
        ["Drv_Footer"] = "설치 상태가 '설치됨'으로 바뀌지 않으면 재부팅 후 앱을 다시 실행하세요.",
        ["Drv_ViGEm_Desc"] = "가상 Xbox 360 컨트롤러 출력에 필요",
        ["Drv_HidHide_Desc"] = "게임으로부터 물리 컨트롤러를 숨기는 데 필요",
        ["Drv_Installed"] = "설치됨",
        ["Drv_NotInstalled"] = "설치 안 됨",
        ["Drv_Checking"] = "확인 중…",
        ["Drv_Busy_CheckRelease"] = "최신 릴리스 확인 중…",
        ["Drv_Busy_Download"] = "설치 파일 다운로드 중…",
        ["Drv_Busy_Run"] = "설치 프로그램 실행…",
        ["Drv_Busy_Reboot"] = "설치 후 재부팅이 필요할 수 있습니다.",
        ["Drv_Busy_OpenPage"] = "릴리스 페이지를 엽니다…",
        ["Drv_Busy_Failed"] = "다운로드 실패 — 페이지를 열었습니다.",

        // 장치 종류
        ["Kind_Xbox"] = "Xbox",
        ["Kind_PlayStation"] = "PlayStation",
        ["Kind_SwitchPro"] = "Switch Pro",
        ["Kind_JoyconPair"] = "Joy-Con 페어",
        ["Kind_JoyconSingle"] = "Joy-Con",
        ["Kind_Standard"] = "표준",
        ["Kind_Unknown"] = "알 수 없음",
        ["Dev_Manual"] = "수동",

        // 채널
        ["Ch_LSX"] = "왼쪽 스틱 · X",
        ["Ch_LSY"] = "왼쪽 스틱 · Y",
        ["Ch_RSX"] = "오른쪽 스틱 · X",
        ["Ch_RSY"] = "오른쪽 스틱 · Y",
        ["Ch_LT"] = "왼쪽 트리거 (LT)",
        ["Ch_RT"] = "오른쪽 트리거 (RT)",

        // 트레이
        ["Tray_Open"] = "열기",
        ["Tray_Exit"] = "종료",
        ["Tray_DriverOn"] = "드라이버 켜기",
        ["Tray_DriverOff"] = "드라이버 끄기",
        ["Tray_Tip"] = "AxisTune — {0}",
        ["TrayState_Active"] = "동작 중",
        ["TrayState_NoDevice"] = "장치 선택 필요",
        ["TrayState_DriverMissing"] = "ViGEmBus 필요",
        ["TrayState_Disabled"] = "드라이버 꺼짐",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        // Header / status
        ["Header_Driver"] = "Driver",
        ["Status_Ready"] = "Ready",
        ["Status_Active"] = "Running — clean virtual input",
        ["Status_NoDevice"] = "Select a device",
        ["Status_DriverMissing"] = "ViGEmBus required",
        ["Status_Disabled"] = "Driver off",
        ["Detail_HidHideActive"] = "Physical device hidden",
        ["Warn_HideFailedXInput"] = "Couldn't hide the original — XInput controllers can't be hidden (use DInput mode).",
        ["Warn_HideFailedNoHidHide"] = "Couldn't hide the original — HidHide is not installed.",
        ["Slot_Known"] = "Virtual pad: XInput slot {0}",
        ["Slot_Pending"] = "Virtual pad: detecting XInput slot… (shown once a game reads it)",

        // Banner
        ["Banner_DriversMissing"] = "⚠ Required drivers are not installed. Install them in the Drivers tab.",
        ["Banner_InstallDrivers"] = "Install drivers",

        // Updates
        ["Update_Available"] = "Version {0} available (current {1})",
        ["Update_Download"] = "Download",
        ["Update_Dismiss"] = "Dismiss",
        ["Set_CheckUpdates"] = "Check for updates on startup",
        ["Set_CheckUpdates_Sub"] = "Look for a newer version on GitHub Releases",
        ["Update_CheckNow"] = "Check now",
        ["Update_Current"] = "Current version {0}",
        ["Update_UpToDate"] = "You're up to date",
        ["Update_Checking"] = "Checking…",

        // Profile bar
        ["Profile_Label"] = "Profile",
        ["Profile_Add"] = "Add",
        ["Profile_Delete"] = "Delete",
        ["Profile_NewName"] = "Profile {0}",

        // Tabs
        ["Tab_Devices"] = "Devices",
        ["Tab_Tuning"] = "Tuning",
        ["Tab_Mapping"] = "Mapping",
        ["Tab_Drivers"] = "Drivers",

        // Devices tab
        ["Dev_InputDevice"] = "Input device",
        ["Dev_Refresh"] = "Refresh",
        ["Dev_Placeholder"] = "Select a detected controller",
        ["Dev_AutoNote"] = "Xbox · PlayStation · Switch (Pro/Joy-Con) are auto-detected.",
        ["Settings_Title"] = "Settings",
        ["Settings_ProfileName"] = "Active profile name",
        ["Settings_Language"] = "Language / 언어",
        ["Set_RunAtStartup"] = "Run at Windows startup",
        ["Set_RunAtStartup_Sub"] = "Launch to the tray on sign-in",
        ["Set_MinTray"] = "Minimize to tray on close",
        ["Set_MinTray_Sub"] = "If off, closing (X) quits the app",
        ["Set_AutoEnable"] = "Enable driver on startup",
        ["Set_AutoEnable_Sub"] = "Turn on virtual output when the app starts",

        // Tuning tab
        ["Tune_CurveHint"] = "Double-click: add point · Drag: move · Right-click: delete",
        ["Tune_PresetLinear"] = "Preset: Linear",
        ["Tune_PresetAggressive"] = "Aggressive",
        ["Tune_PresetSmooth"] = "Smooth",
        ["Tune_InputMin"] = "Input min",
        ["Tune_InputMax"] = "Input max",
        ["Tune_InnerDeadzone"] = "Inner deadzone",
        ["Tune_OuterDeadzone"] = "Outer deadzone",
        ["Tune_Interp"] = "Curve interpolation",
        ["Tune_Interp_Linear"] = "Linear",
        ["Tune_Interp_Cubic"] = "Smooth (monotone cubic)",
        ["Tune_Invert"] = "Invert axis",
        ["Tune_PreviewTitle"] = "Live preview",
        ["Tune_PreviewIn"] = "In ",
        ["Tune_PreviewArrow"] = "   →   Out ",
        ["Tune_PreviewNote"] = "Move the controller while on this tab to see the current point on the curve.",

        // Mapping tab
        ["Map_Use"] = "Use manual mapping",
        ["Map_Use_Sub"] = "Bind an unrecognized controller to Xbox controls.",
        ["Map_Hint"] = "Click [Bind], then move a physical button/axis to assign it. (Select a device first.)",
        ["Map_Buttons"] = "Buttons",
        ["Map_ClearAll"] = "Clear all",
        ["Map_Axes"] = "Axes (sticks & triggers)",
        ["Map_Bind"] = "Bind",
        ["Map_Waiting"] = "Waiting...",
        ["Map_None"] = "(none)",
        ["Map_Btn"] = "Button {0}",
        ["Map_Hat"] = "Hat {0} {1}",
        ["Map_Axis"] = "Axis {0}{1}",
        ["MapT_LS"] = "LS (press)",
        ["MapT_RS"] = "RS (press)",

        // Diagram context panel
        ["Diagram_Hint"] = "Click a control on the gamepad to configure it",
        ["Diagram_ButtonInMapping"] = "Buttons are configured in the Mapping tab",
        ["Diagram_AxisToEdit"] = "Axis to edit",

        // Drivers tab
        ["Drv_Intro"] = "AxisTune uses two open-source drivers (Nefarius). Click [Install] below to download the official release and run its installer. A reboot may be required afterward.",
        ["Drv_Install"] = "Install",
        ["Drv_DownloadPage"] = "Download page",
        ["Drv_Footer"] = "If the status doesn't change to 'Installed', reboot and relaunch the app.",
        ["Drv_ViGEm_Desc"] = "Required for virtual Xbox 360 output",
        ["Drv_HidHide_Desc"] = "Required to hide the physical controller from games",
        ["Drv_Installed"] = "Installed",
        ["Drv_NotInstalled"] = "Not installed",
        ["Drv_Checking"] = "Checking…",
        ["Drv_Busy_CheckRelease"] = "Checking latest release…",
        ["Drv_Busy_Download"] = "Downloading installer…",
        ["Drv_Busy_Run"] = "Launching installer…",
        ["Drv_Busy_Reboot"] = "A reboot may be required after install.",
        ["Drv_Busy_OpenPage"] = "Opening the release page…",
        ["Drv_Busy_Failed"] = "Download failed — opened the page.",

        // Device kinds
        ["Kind_Xbox"] = "Xbox",
        ["Kind_PlayStation"] = "PlayStation",
        ["Kind_SwitchPro"] = "Switch Pro",
        ["Kind_JoyconPair"] = "Joy-Con pair",
        ["Kind_JoyconSingle"] = "Joy-Con",
        ["Kind_Standard"] = "Standard",
        ["Kind_Unknown"] = "Unknown",
        ["Dev_Manual"] = "Manual",

        // Channels
        ["Ch_LSX"] = "Left Stick · X",
        ["Ch_LSY"] = "Left Stick · Y",
        ["Ch_RSX"] = "Right Stick · X",
        ["Ch_RSY"] = "Right Stick · Y",
        ["Ch_LT"] = "Left Trigger (LT)",
        ["Ch_RT"] = "Right Trigger (RT)",

        // Tray
        ["Tray_Open"] = "Open",
        ["Tray_Exit"] = "Exit",
        ["Tray_DriverOn"] = "Turn driver on",
        ["Tray_DriverOff"] = "Turn driver off",
        ["Tray_Tip"] = "AxisTune — {0}",
        ["TrayState_Active"] = "Running",
        ["TrayState_NoDevice"] = "Select a device",
        ["TrayState_DriverMissing"] = "ViGEmBus required",
        ["TrayState_Disabled"] = "Driver off",
    };
}
