; Inno Setup Script for Personal Flip Pad (MyCrownJewelApp.Pfpad)
; Supports per-user (CurrentUser) and per-machine (AllUsers) installation.
;
; Build from command line:
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" setup.iss

#define MyAppName "Personal Flip Pad"
#define MyAppShortName "Pfpad"
#define MyAppExeName "MyCrownJewelApp.Pfpad.exe"
#define MyAppVersion "1.0.0.0"
#define MyAppPublisher "Personal Flip Pad"
#define MyAppURL "https://github.com/casse/azure-ops-solo"
#define MyAppAssocName "Source Code File"
#define MyAppAssocExt ".cs,.js,.ts,.py,.json,.xml,.yaml,.yml,.md,.txt,.html,.css,.ps1,.sh,.tf,.bicep"

[Setup]
; NOTE: AppId must be unique across all installers. Generated via UUID.
AppId={{F3B7A1D2-5E8C-4A9B-8D6F-2C1E3A4B5C6D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppShortName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; Per-user vs per-machine — user chooses at install time via /CURRENTUSER or /ALLUSERS
; Inno Setup 6+: use PrivilegesRequired=lowest to let the user choose
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=.
OutputBaseFilename=PersonalFlipPad-Setup-{#MyAppVersion}
SetupIconFile=
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
; Request restart only if files are in use
CloseApplications=yes
RestartApplications=no
; Upgrade support — remember previous install location and settings
UsePreviousAppDir=yes
UsePreviousGroup=yes
DisableDirPage=auto

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce

[Files]
; Main application (self-contained single-file executable + PDBs)
Source: "app\MyCrownJewelApp.Pfpad.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "app\MyCrownJewelApp.Pfpad.pdb"; DestDir: "{app}"; Flags: ignoreversion
Source: "app\MyCrownJewelApp.Core.pdb"; DestDir: "{app}"; Flags: ignoreversion

; No additional runtime files needed — this is a self-contained single-file publish.

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autoprograms}\{#MyAppName} (Uninstall)"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: postinstall nowait skipifsilent

[UninstallRun]
; Clean up user data on uninstall (optional — user may want to keep config)
Filename: "{cmd}"; Parameters: "/c rmdir /s /q ""{localappdata}\MyCrownJewelApp"" 2>nul"; Flags: runhidden; RunOnceId: "CleanUserData"

; Language: also remove per-machine data if installed as admin
Filename: "{cmd}"; Parameters: "/c rmdir /s /q ""{commonappdata}\MyCrownJewelApp"" 2>nul"; Flags: runhidden; RunOnceId: "CleanCommonData"

[Registry]
; File association for .cs (C#) — off by default, user enables via context menu
; This is handled at runtime via "Open With" -> choose app
; No hard-coded registry keys needed — the app is portable once installed.

[Code]
{ Custom wizard page: choose between Current User and All Users }
{ Inno Setup 6+ handles this with PrivilegesRequiredOverridesAllowed=dialog }
{ but we add a brief explanation. }

function InitializeSetup: Boolean;
begin
  Result := True;
end;

{ Ask user to close running instances before install }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if ShellExec('', 'taskkill', '/f /im {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    { Successfully terminated any running instance }
  end;
end;

{ Version info displayed in Add/Remove Programs }
procedure CurPageChanged(CurPageID: Integer);
begin
  { No custom UI needed — Inno Setup's built-in per-user/per-machine dialog is sufficient }
end;
