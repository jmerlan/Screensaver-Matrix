; Inno Setup script for the Matrix Screensaver.
; Built by ..\build.ps1 -Installer  (or: ISCC.exe installer\Matrix.iss after building dist\Matrix.scr)

#define AppName "Matrix Screensaver"
#define ScrFile "..\dist\Matrix.scr"
#ifndef AppVersion
  ; build.ps1 passes /DAppVersion from the .csproj; fall back to the file version.
  #define AppVersion GetVersionNumbersString(ScrFile)
#endif

[Setup]
AppId={{6B1F3C7E-2D84-4A9B-9E51-8C3A0F7D2E64}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Jay Merlan
AppPublisherURL=https://github.com/jmerlan/Screensaver-Matrix
AppSupportURL=https://github.com/jmerlan/Screensaver-Matrix/issues
VersionInfoVersion={#AppVersion}
; The screensaver itself lives in System32 (so it's always listed in Screen Saver Settings);
; {app} only holds the uninstaller.
DefaultDirName={autopf}\{#AppName}
DisableDirPage=yes
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
; Install into the real 64-bit System32, not SysWOW64.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 1903+ ships .NET Framework 4.8, which the screensaver needs.
MinVersion=10.0.18362
SetupIconFile=..\assets\matrix.ico
UninstallDisplayIcon={sys}\Matrix.scr
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
OutputDir=..\dist
OutputBaseFilename=MatrixScreensaverSetup-{#AppVersion}
CloseApplications=yes

[Tasks]
Name: "setactive"; Description: "Make Matrix my screen saver"; GroupDescription: "Options:"

[Files]
Source: "{#ScrFile}"; DestDir: "{sys}"; Flags: ignoreversion restartreplace uninsrestartdelete

[Icons]
Name: "{group}\Matrix Screensaver Settings"; Filename: "{sys}\Matrix.scr"; Parameters: "/c"; IconFilename: "{sys}\Matrix.scr"
Name: "{group}\Preview Matrix Screensaver"; Filename: "{sys}\Matrix.scr"; Parameters: "/s"; IconFilename: "{sys}\Matrix.scr"

[Registry]
; Only for the user running setup, and only if they ticked the task.
Root: HKCU; Subkey: "Control Panel\Desktop"; ValueType: string; ValueName: "SCRNSAVE.EXE"; ValueData: "{sys}\Matrix.scr"; Tasks: setactive
Root: HKCU; Subkey: "Control Panel\Desktop"; ValueType: string; ValueName: "ScreenSaveActive"; ValueData: "1"; Tasks: setactive
; Remove the screensaver's own settings on uninstall.
Root: HKCU; Subkey: "Software\MatrixScreensaver"; Flags: uninsdeletekey dontcreatekey

[Run]
Filename: "{sys}\Matrix.scr"; Parameters: "/c"; Description: "Adjust Matrix Screensaver settings now"; Flags: postinstall nowait skipifsilent runasoriginaluser unchecked
Filename: "{sys}\control.exe"; Parameters: "desk.cpl,,@screensaver"; Description: "Open Windows Screen Saver Settings"; Flags: postinstall nowait skipifsilent runasoriginaluser

[Code]
const
  SPI_SETSCREENSAVEACTIVE = 17;
  SPIF_UPDATEINIFILE = 1;
  SPIF_SENDCHANGE = 2;

function SystemParametersInfo(uiAction, uiParam, pvParam, fWinIni: Cardinal): Boolean;
  external 'SystemParametersInfoW@user32.dll stdcall';

procedure CurStepChanged(CurStep: TSetupStep);
begin
  // Tell Windows the screen saver settings changed so it takes effect without signing out.
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('setactive') then
    SystemParametersInfo(SPI_SETSCREENSAVEACTIVE, 1, 0, SPIF_UPDATEINIFILE or SPIF_SENDCHANGE);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Current: String;
begin
  // If Matrix is still the active screen saver, clear it so Windows doesn't point at a missing file.
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Control Panel\Desktop', 'SCRNSAVE.EXE', Current) and
       (CompareText(ExtractFileName(Current), 'Matrix.scr') = 0) then
    begin
      RegDeleteValue(HKCU, 'Control Panel\Desktop', 'SCRNSAVE.EXE');
      SystemParametersInfo(SPI_SETSCREENSAVEACTIVE, 0, 0, SPIF_UPDATEINIFILE or SPIF_SENDCHANGE);
    end;
end;
