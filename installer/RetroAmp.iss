; Inno Setup script for RetroAmp
#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\src\RetroAmp\bin\Release\net48"
#endif

[Setup]
AppId={{6B7C2D9E-3F41-4C2B-9A1E-52A7D3E0F1A4}
AppName=RetroAmp
AppVersion={#AppVersion}
AppVerName=RetroAmp {#AppVersion}
AppPublisher=NhảmStudio
AppPublisherURL=https://topvl.net
AppSupportURL=https://github.com/aiplus4m-cmd/Winamp
DefaultDirName={autopf}\RetroAmp
DefaultGroupName=RetroAmp
UninstallDisplayIcon={app}\RetroAmp.exe
OutputDir=..\dist
OutputBaseFilename=RetroAmp-{#AppVersion}-Setup
SetupIconFile=..\assets\retroamp.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=yes
MinVersion=6.1sp1

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "openwith"; Description: "Add RetroAmp to ""Open with"" for music && video files"; GroupDescription: "File types:"

[Files]
Source: "{#SourceDir}\RetroAmp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\RetroAmp.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\RetroAmp"; Filename: "{app}\RetroAmp.exe"
Name: "{group}\Uninstall RetroAmp"; Filename: "{uninstallexe}"
Name: "{autodesktop}\RetroAmp"; Filename: "{app}\RetroAmp.exe"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Classes\Applications\RetroAmp.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "RetroAmp"; Flags: uninsdeletekey; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\RetroAmp.exe\shell\open\command"; ValueType: string; ValueData: """{app}\RetroAmp.exe"" ""%1"""; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\RetroAmp.exe\SupportedTypes"; ValueType: string; ValueName: ".mp3"; ValueData: ""; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\RetroAmp.exe\SupportedTypes"; ValueType: string; ValueName: ".flac"; ValueData: ""; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\RetroAmp.exe\SupportedTypes"; ValueType: string; ValueName: ".m4a"; ValueData: ""; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\RetroAmp.exe\SupportedTypes"; ValueType: string; ValueName: ".wav"; ValueData: ""; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\RetroAmp.exe\SupportedTypes"; ValueType: string; ValueName: ".wma"; ValueData: ""; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\RetroAmp.exe\SupportedTypes"; ValueType: string; ValueName: ".ogg"; ValueData: ""; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\RetroAmp.exe\SupportedTypes"; ValueType: string; ValueName: ".aac"; ValueData: ""; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\RetroAmp.exe\SupportedTypes"; ValueType: string; ValueName: ".mp4"; ValueData: ""; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\RetroAmp.exe\SupportedTypes"; ValueType: string; ValueName: ".mkv"; ValueData: ""; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\RetroAmp.exe\SupportedTypes"; ValueType: string; ValueName: ".avi"; ValueData: ""; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\RetroAmp.exe\SupportedTypes"; ValueType: string; ValueName: ".m3u"; ValueData: ""; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\Applications\RetroAmp.exe\SupportedTypes"; ValueType: string; ValueName: ".m3u8"; ValueData: ""; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\.mp3\OpenWithProgids"; ValueType: string; ValueName: "Applications\RetroAmp.exe"; ValueData: ""; Flags: uninsdeletevalue; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\.flac\OpenWithProgids"; ValueType: string; ValueName: "Applications\RetroAmp.exe"; ValueData: ""; Flags: uninsdeletevalue; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\.m4a\OpenWithProgids"; ValueType: string; ValueName: "Applications\RetroAmp.exe"; ValueData: ""; Flags: uninsdeletevalue; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\.mp4\OpenWithProgids"; ValueType: string; ValueName: "Applications\RetroAmp.exe"; ValueData: ""; Flags: uninsdeletevalue; Tasks: openwith
Root: HKA; Subkey: "Software\Classes\.mkv\OpenWithProgids"; ValueType: string; ValueName: "Applications\RetroAmp.exe"; ValueData: ""; Flags: uninsdeletevalue; Tasks: openwith

[Run]
Filename: "{app}\RetroAmp.exe"; Description: "{cm:LaunchProgram,RetroAmp}"; Flags: nowait postinstall skipifsilent
