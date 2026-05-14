[Setup]
AppId=YOUR-APP-ID-HERE
AppName=Personal Flip Pad
AppVersion=1.0.32.0
AppVerName=Personal Flip Pad 1.0.32.0
AppPublisher=Personal Flip Pad
AppPublisherURL=https://github.com/djwisdom/azure-ops-solo
AppSupportURL=https://github.com/djwisdom/azure-ops-solo/issues
AppUpdatesURL=https://github.com/djwisdom/azure-ops-solo/releases
DefaultDirName={autopf}\Personal Flip Pad
DefaultGroupName=Personal Flip Pad
AllowNoIcons=yes

OutputDir=.
OutputBaseFilename=PersonalFlipPad-1.0.32.0
; SetupIconFile=app.ico
Compression=lzma
SolidCompression=yes
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

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

; Add to "Open with" list for all files
Root: HKCR; Subkey: "*\OpenWithProgids\MyCrownJewelApp.Pfpad"; ValueType: string; ValueData: ""; Flags: uninsdeletekey

; Store installation information
Root: HKCU; Subkey: "Software\Microsoft\PersonalFlipPad"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\PersonalFlipPad"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\PersonalFlipPad"; ValueType: string; ValueName: "Version"; ValueData: "1.0.32.0"; Flags: uninsdeletekey

[UninstallDelete]
Type: filesandordirs; Name: "{app}"