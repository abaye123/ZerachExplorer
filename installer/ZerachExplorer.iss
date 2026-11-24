; Built by scripts\build-release.ps1, which passes AppVersion, SourceDir and, when signing, Signed.

#define MyAppName "ZerachExplorer"
#define MyAppPublisher "abaye"
#define MyAppURL "https://www.abaye.co/"
#define MyAppExeName "ZerachExplorer.exe"

[Setup]
; Identifies the product for upgrades from earlier versions. Never change it.
AppId={{8A6SB7C1-B88F-464C-81DA-1C234716CFD3}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=license.he.txt
InfoAfterFile=after-install.he.txt
OutputDir=..\dist
OutputBaseFilename=ZerachExplorer_setup
SetupIconFile=..\ZerachExplorer\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#ifdef Signed
SignTool=release
SignedUninstaller=yes
#endif

[Languages]
Name: "hebrew"; MessagesFile: "compiler:Languages\Hebrew.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; Left behind by version 1.4.0, which was built as LiteExplorer.dll.
Type: files; Name: "{app}\LiteExplorer.dll"
Type: files; Name: "{app}\LiteExplorer.pdb"
Type: files; Name: "{app}\LiteExplorer.deps.json"
Type: files; Name: "{app}\LiteExplorer.runtimeconfig.json"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
