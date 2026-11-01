#define MyAppName "KiWeave"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0-beta.5"
#endif
#ifndef MyAppSourceDir
  #define MyAppSourceDir "..\release\staging"
#endif
#define MyAppPublisher "Justa-Doge"
#define MyAppURL "https://github.com/Justa-Doge/KiWeave"
#define MyAppExeName "KiWeave.exe"

[Setup]
AppId={{A0C164B8-4A2B-4E4C-92AC-5A11CF6AE7F6}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={localappdata}\Programs\KiWeave
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
; The application build is self-contained; ship the whole staged folder or bundled runtime files go missing on clean PCs.
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\KiWeave"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Comment: "Customize function keys, hotkeys, profiles, and integrations"
Name: "{autoprograms}\KiWeave Safe Mode"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--safe-mode"; WorkingDir: "{app}"; Comment: "Open recovery tools without hooks, hotkeys, integrations, or network access"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch KiWeave"; Flags: nowait postinstall skipifsilent

[InstallDelete]
Type: files; Name: "{autoprograms}\Function Row Remapper.lnk"

[UninstallDelete]
; User mappings and profiles under LocalAppData\KiWeave are intentionally preserved.
Type: files; Name: "{app}\KiWeave.previous.exe"

[Code]
function KiWeaveRunningMutexes: String;
var
  UserName: String;
  EnvironmentUserName: String;
begin
  UserName := GetUserNameString;
  EnvironmentUserName := GetEnv('USERNAME');
  Result := 'Local\KiWeave-' + UserName;
  if (EnvironmentUserName <> '') and (EnvironmentUserName <> UserName) then
    Result := Result + ',Local\KiWeave-' + EnvironmentUserName;
end;

function KiWeaveCheckerError(const ExitCode: Integer): String;
begin
  case ExitCode of
    10: Result := 'KiWeave is currently in the foreground. Save your work, exit KiWeave, and run Setup again.';
    11: Result := 'KiWeave has unsaved edits. Save or discard them, exit KiWeave, and run Setup again.';
    12: Result := 'KiWeave has a dialog open. Close the dialog, exit KiWeave, and run Setup again.';
    13: Result := 'KiWeave did not respond to the state check. Exit it manually and run Setup again.';
    14: Result := 'The running KiWeave copy cannot verify a safe update. Exit it manually and run Setup again.';
    15: Result := 'Another KiWeave update check is already running. Wait a moment and retry Setup.';
  else
    Result := 'KiWeave could not confirm that it is safe to update (state code ' + IntToStr(ExitCode) + '). Exit it manually and run Setup again.';
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Mutexes: String;
  ExistingExe: String;
  ResultCode: Integer;
  Attempt: Integer;
begin
  Result := '';
  Mutexes := KiWeaveRunningMutexes;
  if not CheckForMutexes(Mutexes) then
    Exit;

  ExistingExe := ExpandConstant('{app}\KiWeave.exe');
  if not FileExists(ExistingExe) then
  begin
    Result := 'A KiWeave session is running from another folder. Exit it before installing this build.';
    Exit;
  end;

  if not Exec(ExistingExe, '--inspect-state', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'Setup could not check the running KiWeave session. Exit KiWeave manually and run Setup again.';
    Exit;
  end;
  if ResultCode <> 0 then
  begin
    Result := KiWeaveCheckerError(ResultCode);
    Exit;
  end;

  if not Exec(ExistingExe, '--exit-for-update', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'Setup could not ask KiWeave to close safely. Exit it manually and run Setup again.';
    Exit;
  end;
  if ResultCode <> 0 then
  begin
    Result := KiWeaveCheckerError(ResultCode);
    Exit;
  end;

  for Attempt := 1 to 20 do
  begin
    if not CheckForMutexes(Mutexes) then
      Exit;
    Sleep(250);
  end;
  Result := 'KiWeave accepted the close request but is still running. Exit it manually and run Setup again.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ExistingValue: String;
begin
  if CurStep = ssPostInstall then
  begin
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'FunctionRowRemapper', ExistingValue) or
       RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'KiWeave', ExistingValue) then
    begin
      RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'KiWeave', '"' + ExpandConstant('{app}\{#MyAppExeName}') + '" --tray');
      RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'FunctionRowRemapper');
    end;
  end;
end;
