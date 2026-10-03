#define MyAppName "KiWeave"
#define MyAppVersion "1.0.0-beta.1"
#define MyAppPublisher "Justa-Doge"
#define MyAppURL "https://github.com/Justa-Doge/KeyWeave"
#define MyAppExeName "FunctionRowRemapper.exe"

[Setup]
AppId={{A0C164B8-4A2B-4E4C-92AC-5A11CF6AE7F6}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={localappdata}\Programs\FunctionRowRemapper
DefaultGroupName=KiWeave
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\release
OutputBaseFilename=KiWeave-Setup-{#MyAppVersion}
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=..\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\bin\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\NOTICE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\CHANGELOG.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\KiWeave"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Comment: "Customize function keys, hotkeys, profiles, and integrations"
Name: "{autoprograms}\KiWeave Safe Mode"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--safe-mode"; WorkingDir: "{app}"; Comment: "Open recovery tools without hooks, hotkeys, integrations, or network access"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch KiWeave"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; User mappings and profiles under LocalAppData\FunctionRowRemapper are intentionally preserved.
Type: files; Name: "{app}\FunctionRowRemapper.previous.exe"
