[Setup]
AppId=24f5ed45-72b9-4500-b561-8f0ce49ea480
AppName=Personal Flip Pad
AppVersion=1.0.36.0
AppVerName=Personal Flip Pad 1.0.36.0
AppPublisher=Personal Flip Pad
AppPublisherURL=https://github.com/djwisdom/azure-ops-solo
AppSupportURL=https://github.com/djwisdom/azure-ops-solo/issues
AppUpdatesURL=https://github.com/djwisdom/azure-ops-solo/releases
; Per-user install — no UAC prompt required
DefaultDirName={localappdata}\Personal Flip Pad
DefaultGroupName=Personal Flip Pad
AllowNoIcons=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

OutputDir=.
OutputBaseFilename=PersonalFlipPad-Setup-1.0.36.0
; SetupIconFile=app.ico
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "associate_txt"; Description: "Associate .txt files"; GroupDescription: "File associations:"
Name: "associate_cs"; Description: "Associate .cs files"; GroupDescription: "File associations:"
Name: "associate_js"; Description: "Associate .js files"; GroupDescription: "File associations:"
Name: "associate_py"; Description: "Associate .py files"; GroupDescription: "File associations:"
Name: "associate_cpp"; Description: "Associate .cpp files"; GroupDescription: "File associations:"
Name: "associate_h"; Description: "Associate .h files"; GroupDescription: "File associations:"
Name: "associate_json"; Description: "Associate .json files"; GroupDescription: "File associations:"
Name: "associate_xml"; Description: "Associate .xml files"; GroupDescription: "File associations:"
Name: "associate_md"; Description: "Associate .md files"; GroupDescription: "File associations:"
Name: "associate_tf"; Description: "Associate .tf and .tfvars files"; GroupDescription: "File associations:"
Name: "associate_bicep"; Description: "Associate .bicep files"; GroupDescription: "File associations:"
Name: "associate_yml"; Description: "Associate .yml and .yaml files"; GroupDescription: "File associations:"
Name: "associate_cfg"; Description: "Associate .cfg and .ini files"; GroupDescription: "File associations:"
Name: "associate_j2"; Description: "Associate .j2 template files"; GroupDescription: "File associations:"

[Files]
Source: "InstallerFiles\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; WebView2 Bootstrapper — installs Evergreen runtime silently if not already present
Source: "MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: WebView2NotInstalled

[Icons]
Name: "{group}\Personal Flip Pad"; Filename: "{app}\MyCrownJewelApp.Pfpad.exe"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,Personal Flip Pad}"; Filename: "{uninstallexe}"; WorkingDir: "{app}"
Name: "{autodesktop}\Personal Flip Pad"; Filename: "{app}\MyCrownJewelApp.Pfpad.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; Install WebView2 runtime silently (per-user, no admin needed)
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; \
  StatusMsg: "Installing WebView2 Runtime (required for built-in browser)..."; \
  Flags: nowait; Check: WebView2NotInstalled
; Launch app after install
Filename: "{app}\MyCrownJewelApp.Pfpad.exe"; Description: "{cm:LaunchProgram,Personal Flip Pad}"; Flags: nowait postinstall skipifsilent

[Registry]
; Register application for "Open with" menu (per-user: HKCU\Software\Classes)
Root: HKCU; Subkey: "Software\Classes\Applications\MyCrownJewelApp.Pfpad.exe"; ValueType: string; ValueData: ""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\MyCrownJewelApp.Pfpad.exe\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\MyCrownJewelApp.Pfpad.exe\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey

; File associations (per-user: HKCU\Software\Classes)
Root: HKCU; Subkey: "Software\Classes\.txt"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.txt"; Flags: uninsdeletevalue; Tasks: associate_txt
Root: HKCU; Subkey: "Software\Classes\.cs"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.cs"; Flags: uninsdeletevalue; Tasks: associate_cs
Root: HKCU; Subkey: "Software\Classes\.js"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.js"; Flags: uninsdeletevalue; Tasks: associate_js
Root: HKCU; Subkey: "Software\Classes\.py"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.py"; Flags: uninsdeletevalue; Tasks: associate_py
Root: HKCU; Subkey: "Software\Classes\.cpp"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.cpp"; Flags: uninsdeletevalue; Tasks: associate_cpp
Root: HKCU; Subkey: "Software\Classes\.h"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.h"; Flags: uninsdeletevalue; Tasks: associate_h
Root: HKCU; Subkey: "Software\Classes\.json"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.json"; Flags: uninsdeletevalue; Tasks: associate_json
Root: HKCU; Subkey: "Software\Classes\.xml"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.xml"; Flags: uninsdeletevalue; Tasks: associate_xml
Root: HKCU; Subkey: "Software\Classes\.md"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.md"; Flags: uninsdeletevalue; Tasks: associate_md
Root: HKCU; Subkey: "Software\Classes\.tf"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.tf"; Flags: uninsdeletevalue; Tasks: associate_tf
Root: HKCU; Subkey: "Software\Classes\.tfvars"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.tfvars"; Flags: uninsdeletevalue; Tasks: associate_tf
Root: HKCU; Subkey: "Software\Classes\.bicep"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.bicep"; Flags: uninsdeletevalue; Tasks: associate_bicep
Root: HKCU; Subkey: "Software\Classes\.yml"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.yml"; Flags: uninsdeletevalue; Tasks: associate_yml
Root: HKCU; Subkey: "Software\Classes\.yaml"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.yaml"; Flags: uninsdeletevalue; Tasks: associate_yml
Root: HKCU; Subkey: "Software\Classes\.cfg"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.cfg"; Flags: uninsdeletevalue; Tasks: associate_cfg
Root: HKCU; Subkey: "Software\Classes\.ini"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.ini"; Flags: uninsdeletevalue; Tasks: associate_cfg
Root: HKCU; Subkey: "Software\Classes\.j2"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.j2"; Flags: uninsdeletevalue; Tasks: associate_j2

; ProgID definitions (per-user)
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.txt"; ValueType: string; ValueData: "Text Document"; Flags: uninsdeletekey; Tasks: associate_txt
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.txt\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_txt
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.txt\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_txt

Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.cs"; ValueType: string; ValueData: "C# Source File"; Flags: uninsdeletekey; Tasks: associate_cs
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.cs\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_cs
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.cs\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_cs

Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.js"; ValueType: string; ValueData: "JavaScript File"; Flags: uninsdeletekey; Tasks: associate_js
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.js\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_js
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.js\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_js

Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.py"; ValueType: string; ValueData: "Python File"; Flags: uninsdeletekey; Tasks: associate_py
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.py\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_py
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.py\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_py

Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.cpp"; ValueType: string; ValueData: "C++ Source File"; Flags: uninsdeletekey; Tasks: associate_cpp
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.cpp\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_cpp
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.cpp\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_cpp

Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.h"; ValueType: string; ValueData: "C/C++ Header File"; Flags: uninsdeletekey; Tasks: associate_h
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.h\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_h
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.h\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_h

Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.json"; ValueType: string; ValueData: "JSON File"; Flags: uninsdeletekey; Tasks: associate_json
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.json\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_json
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.json\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_json

Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.xml"; ValueType: string; ValueData: "XML File"; Flags: uninsdeletekey; Tasks: associate_xml
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.xml\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_xml
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.xml\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_xml

Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.md"; ValueType: string; ValueData: "Markdown File"; Flags: uninsdeletekey; Tasks: associate_md
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.md\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_md
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.md\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_md

Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.tf"; ValueType: string; ValueData: "Terraform Configuration"; Flags: uninsdeletekey; Tasks: associate_tf
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.tf\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_tf
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.tf\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_tf

Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.tfvars"; ValueType: string; ValueData: "Terraform Variables"; Flags: uninsdeletekey; Tasks: associate_tf
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.tfvars\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_tf
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.tfvars\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_tf

Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.bicep"; ValueType: string; ValueData: "Azure Bicep Template"; Flags: uninsdeletekey; Tasks: associate_bicep
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.bicep\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_bicep
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.bicep\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_bicep

Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.yml"; ValueType: string; ValueData: "YAML Document"; Flags: uninsdeletekey; Tasks: associate_yml
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.yml\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_yml
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.yml\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_yml

Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.yaml"; ValueType: string; ValueData: "YAML Document"; Flags: uninsdeletekey; Tasks: associate_yml
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.yaml\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_yml
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.yaml\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_yml

Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.cfg"; ValueType: string; ValueData: "Configuration File"; Flags: uninsdeletekey; Tasks: associate_cfg
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.cfg\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_cfg
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.cfg\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_cfg

Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.ini"; ValueType: string; ValueData: "Configuration File"; Flags: uninsdeletekey; Tasks: associate_cfg
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.ini\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_cfg
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.ini\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_cfg

Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.j2"; ValueType: string; ValueData: "Jinja2 Template"; Flags: uninsdeletekey; Tasks: associate_j2
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.j2\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_j2
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad.j2\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_j2

; Add to "Open with" context menu (per-user)
Root: HKCU; Subkey: "Software\Classes\*\shellex\ContextMenuHandlers\MyCrownJewelApp.Pfpad"; ValueType: string; ValueData: ""; Flags: uninsdeletekey

; Installation metadata
Root: HKCU; Subkey: "Software\PersonalFlipPad"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\PersonalFlipPad"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\PersonalFlipPad"; ValueType: string; ValueName: "Version"; ValueData: "1.0.36.0"; Flags: uninsdeletekey

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
// Check if WebView2 Evergreen Runtime is already installed for the current user.
// Checks both machine-wide and per-user locations.
function WebView2NotInstalled(): Boolean;
var
  Version: string;
begin
  Result := True;
  // Machine-wide Evergreen runtime
  if RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) then
    if Version <> '' then begin Result := False; Exit; end;
  // Per-user Evergreen runtime
  if RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) then
    if Version <> '' then begin Result := False; Exit; end;
end;


[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "associate_txt"; Description: "Associate .txt files"; GroupDescription: "File associations:"
Name: "associate_cs"; Description: "Associate .cs files"; GroupDescription: "File associations:"
Name: "associate_js"; Description: "Associate .js files"; GroupDescription: "File associations:"
Name: "associate_py"; Description: "Associate .py files"; GroupDescription: "File associations:"
Name: "associate_cpp"; Description: "Associate .cpp files"; GroupDescription: "File associations:"
Name: "associate_h"; Description: "Associate .h files"; GroupDescription: "File associations:"
Name: "associate_json"; Description: "Associate .json files"; GroupDescription: "File associations:"
Name: "associate_xml"; Description: "Associate .xml files"; GroupDescription: "File associations:"
Name: "associate_md"; Description: "Associate .md files"; GroupDescription: "File associations:"
Name: "associate_tf"; Description: "Associate .tf and .tfvars files"; GroupDescription: "File associations:"
Name: "associate_bicep"; Description: "Associate .bicep files"; GroupDescription: "File associations:"
Name: "associate_yml"; Description: "Associate .yml and .yaml files"; GroupDescription: "File associations:"
Name: "associate_cfg"; Description: "Associate .cfg and .ini files"; GroupDescription: "File associations:"
Name: "associate_j2"; Description: "Associate .j2 template files"; GroupDescription: "File associations:"

[Files]
Source: "InstallerFiles\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Personal Flip Pad"; Filename: "{app}\MyCrownJewelApp.Pfpad.exe"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,Personal Flip Pad}"; Filename: "{uninstallexe}"; WorkingDir: "{app}"
Name: "{autodesktop}\Personal Flip Pad"; Filename: "{app}\MyCrownJewelApp.Pfpad.exe"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\Personal Flip Pad"; Filename: "{app}\MyCrownJewelApp.Pfpad.exe"; WorkingDir: "{app}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\MyCrownJewelApp.Pfpad.exe"; Description: "{cm:LaunchProgram,Personal Flip Pad}"; Flags: nowait postinstall skipifsilent

[Registry]
; Register application for "Open with" menu
Root: HKCR; Subkey: "Applications\MyCrownJewelApp.Pfpad.exe"; ValueType: string; ValueData: ""; Flags: uninsdeletekey
Root: HKCR; Subkey: "Applications\MyCrownJewelApp.Pfpad.exe\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey
Root: HKCR; Subkey: "Applications\MyCrownJewelApp.Pfpad.exe\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey

; File associations based on tasks
Root: HKCR; Subkey: ".txt"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.txt"; Flags: uninsdeletevalue; Tasks: associate_txt
Root: HKCR; Subkey: ".cs"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.cs"; Flags: uninsdeletevalue; Tasks: associate_cs
Root: HKCR; Subkey: ".js"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.js"; Flags: uninsdeletevalue; Tasks: associate_js
Root: HKCR; Subkey: ".py"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.py"; Flags: uninsdeletevalue; Tasks: associate_py
Root: HKCR; Subkey: ".cpp"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.cpp"; Flags: uninsdeletevalue; Tasks: associate_cpp
Root: HKCR; Subkey: ".h"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.h"; Flags: uninsdeletevalue; Tasks: associate_h
Root: HKCR; Subkey: ".json"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.json"; Flags: uninsdeletevalue; Tasks: associate_json
Root: HKCR; Subkey: ".xml"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.xml"; Flags: uninsdeletevalue; Tasks: associate_xml
Root: HKCR; Subkey: ".md"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.md"; Flags: uninsdeletevalue; Tasks: associate_md
Root: HKCR; Subkey: ".tf"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.tf"; Flags: uninsdeletevalue; Tasks: associate_tf
Root: HKCR; Subkey: ".tfvars"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.tfvars"; Flags: uninsdeletevalue; Tasks: associate_tf
Root: HKCR; Subkey: ".bicep"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.bicep"; Flags: uninsdeletevalue; Tasks: associate_bicep
Root: HKCR; Subkey: ".yml"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.yml"; Flags: uninsdeletevalue; Tasks: associate_yml
Root: HKCR; Subkey: ".yaml"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.yaml"; Flags: uninsdeletevalue; Tasks: associate_yml
Root: HKCR; Subkey: ".cfg"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.cfg"; Flags: uninsdeletevalue; Tasks: associate_cfg
Root: HKCR; Subkey: ".ini"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.ini"; Flags: uninsdeletevalue; Tasks: associate_cfg
Root: HKCR; Subkey: ".j2"; ValueType: string; ValueData: "MyCrownJewelApp.Pfpad.j2"; Flags: uninsdeletevalue; Tasks: associate_j2

; ProgID definitions
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.txt"; ValueType: string; ValueData: "Text Document"; Flags: uninsdeletekey; Tasks: associate_txt
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.txt\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_txt
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.txt\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_txt

Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.cs"; ValueType: string; ValueData: "C# Source File"; Flags: uninsdeletekey; Tasks: associate_cs
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.cs\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_cs
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.cs\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_cs

Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.js"; ValueType: string; ValueData: "JavaScript File"; Flags: uninsdeletekey; Tasks: associate_js
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.js\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_js
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.js\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_js

Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.py"; ValueType: string; ValueData: "Python File"; Flags: uninsdeletekey; Tasks: associate_py
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.py\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_py
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.py\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_py

Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.cpp"; ValueType: string; ValueData: "C++ Source File"; Flags: uninsdeletekey; Tasks: associate_cpp
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.cpp\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_cpp
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.cpp\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_cpp

Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.h"; ValueType: string; ValueData: "C/C++ Header File"; Flags: uninsdeletekey; Tasks: associate_h
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.h\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_h
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.h\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_h

Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.json"; ValueType: string; ValueData: "JSON File"; Flags: uninsdeletekey; Tasks: associate_json
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.json\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_json
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.json\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_json

Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.xml"; ValueType: string; ValueData: "XML File"; Flags: uninsdeletekey; Tasks: associate_xml
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.xml\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_xml
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.xml\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_xml

Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.md"; ValueType: string; ValueData: "Markdown File"; Flags: uninsdeletekey; Tasks: associate_md
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.md\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_md
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.md\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_md

Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.tf"; ValueType: string; ValueData: "Terraform Configuration"; Flags: uninsdeletekey; Tasks: associate_tf
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.tf\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_tf
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.tf\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_tf

Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.tfvars"; ValueType: string; ValueData: "Terraform Variables"; Flags: uninsdeletekey; Tasks: associate_tf
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.tfvars\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_tf
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.tfvars\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_tf

Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.bicep"; ValueType: string; ValueData: "Azure Bicep Template"; Flags: uninsdeletekey; Tasks: associate_bicep
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.bicep\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_bicep
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.bicep\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_bicep

Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.yml"; ValueType: string; ValueData: "YAML Document"; Flags: uninsdeletekey; Tasks: associate_yml
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.yml\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_yml
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.yml\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_yml

Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.yaml"; ValueType: string; ValueData: "YAML Document"; Flags: uninsdeletekey; Tasks: associate_yml
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.yaml\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_yml
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.yaml\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_yml

Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.cfg"; ValueType: string; ValueData: "Configuration File"; Flags: uninsdeletekey; Tasks: associate_cfg
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.cfg\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_cfg
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.cfg\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_cfg

Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.ini"; ValueType: string; ValueData: "Configuration File"; Flags: uninsdeletekey; Tasks: associate_cfg
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.ini\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_cfg
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.ini\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_cfg

Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.j2"; ValueType: string; ValueData: "Jinja2 Template"; Flags: uninsdeletekey; Tasks: associate_j2
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.j2\DefaultIcon"; ValueType: string; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletekey; Tasks: associate_j2
Root: HKCR; Subkey: "MyCrownJewelApp.Pfpad.j2\shell\open\command"; ValueType: string; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletekey; Tasks: associate_j2

; Add to "Open with" context menu for all files
Root: HKCR; Subkey: "*\shellex\ContextMenuHandlers\MyCrownJewelApp.Pfpad"; ValueType: string; ValueData: ""; Flags: uninsdeletekey
Root: HKCR; Subkey: "Folder\shellex\ContextMenuHandlers\MyCrownJewelApp.Pfpad"; ValueType: string; ValueData: ""; Flags: uninsdeletekey
Root: HKCR; Subkey: "Directory\Background\shellex\ContextMenuHandlers\MyCrownJewelApp.Pfpad"; ValueType: string; ValueData: ""; Flags: uninsdeletekey

; Store installation information
Root: HKCU; Subkey: "Software\Microsoft\PersonalFlipPad"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\PersonalFlipPad"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\PersonalFlipPad"; ValueType: string; ValueName: "Version"; ValueData: "1.0.31.0"; Flags: uninsdeletekey

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
