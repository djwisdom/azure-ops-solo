; pfpad Installer Script
; Inno Setup 6 — per-current-user installation (no admin rights required)
; Version: 1.0.47.0

#define AppName      "Personal Flip Pad"
#define AppVersion   "1.0.48.0"
#define AppPublisher "Personal Flip Pad"
#define AppExeName   "MyCrownJewelApp.Pfpad.exe"
#define AppId        "{{B4F2C1A3-8E7D-4F56-9C2A-D1E3F7B9A042}"
#define PublishDir   "..\publish\win-x64"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/djwisdom/azure-ops-solo
AppSupportURL=https://github.com/djwisdom/azure-ops-solo/issues
AppUpdatesURL=https://github.com/djwisdom/azure-ops-solo/releases

; Per-user install — no UAC prompt, no admin required
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=

; Install into %LocalAppData%\Personal Flip Pad
DefaultDirName={localappdata}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

; Output
OutputDir=..\installer\output
OutputBaseFilename=pfpad-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Wizard styling
WizardStyle=modern
WizardSizePercent=120
DisableWelcomePage=no
DisableReadyPage=no
SetupIconFile=AppIcon.ico

; Uninstall
UninstallDisplayName={#AppName} {#AppVersion}
UninstallDisplayIcon={app}\AppIcon.ico
CreateUninstallRegKey=yes

; Misc
ShowLanguageDialog=no
ArchitecturesInstallIn64BitMode=x64os
ArchitecturesAllowed=x64os
ChangesAssociations=no
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
; Installer wizard icon (embedded into setup exe via SetupIconFile above)
Source: "AppIcon.ico"; DestDir: "{app}"; Flags: ignoreversion
; All published files — recursive
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu
Name: "{userprograms}\{#AppName}\{#AppName}";           Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{userprograms}\{#AppName}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

; Desktop (optional task)
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Offer to launch after install
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove user settings and logs left by the app
Type: filesandordirs; Name: "{localappdata}\{#AppName}\logs"
Type: filesandordirs; Name: "{localappdata}\{#AppName}\cache"

[Registry]
; Register app in HKCU Add/Remove Programs (Inno Setup does this automatically,
; but we also add an InstallLocation key for discoverability)
Root: HKCU; Subkey: "Software\{#AppPublisher}\{#AppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\{#AppPublisher}\{#AppName}"; ValueType: string; ValueName: "Version";     ValueData: "{#AppVersion}"

[Code]
// Detect existing installation and silently uninstall first in silent mode
function InitializeSetup(): Boolean;
var
  UninstallString: String;
  ResultCode: Integer;
begin
  Result := True;
  if RegQueryStringValue(HKCU,
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1',
    'UninstallString', UninstallString) then
  begin
    if WizardSilent() then
    begin
      // Silent mode: uninstall automatically without prompting
      Exec(RemoveQuotes(UninstallString), '/SILENT /NORESTART', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end
    else
    begin
      if MsgBox(
        '{#AppName} is already installed. Uninstall the previous version before continuing?',
        mbConfirmation, MB_YESNO) = IDYES then
      begin
        Exec(RemoveQuotes(UninstallString), '/SILENT', '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
      end;
    end;
  end;
end;
