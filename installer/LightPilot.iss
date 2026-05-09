#ifndef MyAppVersion
#define MyAppVersion "0.3.0"
#endif

#ifndef MyRuntime
#define MyRuntime "win-x64"
#endif

#define MyAppName "Light Pilot"
#define MyAppPublisher "edfpolo"
#define MyAppExeName "LightPilot.App.exe"
#define MySourceDir "..\artifacts\LightPilot-" + MyAppVersion + "-" + MyRuntime

[Setup]
AppId={{77E6A38A-805C-4E33-9A8E-01D17EEA911F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\LightPilot\App
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=LightPilot-{#MyAppVersion}-{#MyRuntime}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\LightPilot.App\Assets\LightPilot.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "LightPilot"; ValueData: """{app}\{#MyAppExeName}"" --background"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--background"; Description: "Start Light Pilot"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill"; Parameters: "/IM {#MyAppExeName} /F"; Flags: runhidden
