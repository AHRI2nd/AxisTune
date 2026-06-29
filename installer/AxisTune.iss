; AxisTune Inno Setup script
; Compiled by CI with ISCC: ISCC.exe /DMyAppVersion=0.1.0 installer\AxisTune.iss
; (paths are relative to the installer\ folder)

#define MyAppName "AxisTune"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#define MyAppPublisher "AxisTune"
#define MyAppExeName "AxisTune.exe"
#ifndef SourceDir
  #define SourceDir "..\publish\app"
#endif

[Setup]
AppId={{8F3A1C2E-7B5D-4E9A-9C1F-2A6B3D4E5F60}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\installer-output
OutputBaseFilename=AxisTune-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Admin rights are required for ViGEm/HidHide driver control and Program Files install.
PrivilegesRequired=admin
SetupIconFile=..\AxisTune.App\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; shellexec를 써야 requireAdministrator 앱이 (이미 elevated인) 설치 프로그램에서
; ERROR_ELEVATION_REQUIRED 없이 실행된다. runasoriginaluser로 설치 관리자가 아닌
; 로그인 사용자 컨텍스트로 실행.
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent shellexec runasoriginaluser

[Code]
const
  EVENT_MODIFY_STATE = $0002;

function OpenEventApi(dwDesiredAccess: Cardinal; bInheritHandle: Cardinal; lpName: string): Cardinal;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEventApi(hEvent: Cardinal): Cardinal;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandleApi(hObject: Cardinal): Cardinal;
  external 'CloseHandle@kernel32.dll stdcall';

// 실행 중인 AxisTune를 멈춘다: 먼저 정상 종료 신호(드라이버 복원), 안 죽으면 강제종료.
procedure StopRunningApp();
var
  h: Cardinal;
  rc: Integer;
begin
  h := OpenEventApi(EVENT_MODIFY_STATE, 0, 'AxisTune-8F3A1C2E-Quit');
  if h <> 0 then
  begin
    SetEventApi(h);
    CloseHandleApi(h);
    Sleep(3000); // 드라이버 정리 후 정상 종료 대기
  end;
  // 남아 있으면 강제 종료(파일 잠금 해제)
  Exec('taskkill.exe', '/F /IM {#MyAppExeName} /T', '', SW_HIDE, ewWaitUntilTerminated, rc);
  Sleep(500);
end;

// 제거 시작 전: 실행 중이면 종료시켜 파일 잠금을 푼다.
function InitializeUninstall(): Boolean;
begin
  StopRunningApp();
  Result := True;
end;

// 덮어쓰기 설치(업그레이드) 시에도 실행 중이면 먼저 종료.
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp();
  Result := '';
end;
