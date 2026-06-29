; AxisTune Inno Setup 스크립트
; CI에서 ISCC로 컴파일: ISCC.exe /DMyAppVersion=0.1.0 installer\AxisTune.iss
; (경로는 installer\ 폴더 기준)

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
; ViGEm/HidHide 드라이버 제어 및 Program Files 설치를 위해 관리자 권한 필요.
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
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
