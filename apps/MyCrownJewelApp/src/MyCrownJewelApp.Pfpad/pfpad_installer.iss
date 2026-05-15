[Setup]
AppName=MyCrownJewelApp Pfpad
AppVersion=1.0.33.0
DefaultDirName={autopf}\MyCrownJewelApp Pfpad
DefaultGroupName=MyCrownJewelApp Pfpad
OutputDir=installer
OutputBaseFilename=MyCrownJewelApp.Pfpad.Setup
Compression=lzma
SolidCompression=yes
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"
Name: "associate_txt"; Description: "Associate .txt files"; GroupDescription: "File associations:"

[Files]
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\MyCrownJewelApp Pfpad"; Filename: "{app}\MyCrownJewelApp.Pfpad.exe"
Name: "{autodesktop}\MyCrownJewelApp Pfpad"; Filename: "{app}\MyCrownJewelApp.Pfpad.exe"; Tasks: desktopicon

[Registry]
; ProgID for the application
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad"; ValueType: string; ValueName: ""; ValueData: "MyCrownJewelApp Pfpad"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletevalue

; Associate .txt files
Root: HKCU; Subkey: "Software\Classes\.txt"; ValueType: string; ValueName: ""; ValueData: "MyCrownJewelApp.Pfpad"; Tasks: associate_txt; Flags: uninsdeletevalue

; Add to OpenWithList
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.txt\OpenWithList"; ValueType: string; ValueName: "MyCrownJewelApp.Pfpad.exe"; ValueData: ""; Tasks: associate_txt; Flags: uninsdeletevalue

; For per-machine if admin
Root: HKLM; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad"; ValueType: string; ValueName: ""; ValueData: "MyCrownJewelApp Pfpad"; Flags: uninsdeletekey; Check: IsAdminInstallMode
Root: HKLM; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\MyCrownJewelApp.Pfpad.exe,0"; Flags: uninsdeletevalue; Check: IsAdminInstallMode
Root: HKLM; Subkey: "Software\Classes\MyCrownJewelApp.Pfpad\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\MyCrownJewelApp.Pfpad.exe"" ""%1"""; Flags: uninsdeletevalue; Check: IsAdminInstallMode
Root: HKLM; Subkey: "Software\Classes\.txt"; ValueType: string; ValueName: ""; ValueData: "MyCrownJewelApp.Pfpad"; Tasks: associate_txt; Flags: uninsdeletevalue; Check: IsAdminInstallMode
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.txt\OpenWithList"; ValueType: string; ValueName: "MyCrownJewelApp.Pfpad.exe"; ValueData: ""; Tasks: associate_txt; Flags: uninsdeletevalue; Check: IsAdminInstallMode

[Run]
Filename: "{app}\MyCrownJewelApp.Pfpad.exe"; Description: "Launch MyCrownJewelApp Pfpad"; Flags: nowait postinstall skipifsilent