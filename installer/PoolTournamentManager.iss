; Inno Setup script for Pool Tournament Manager.
; Built by build-installer.ps1, which publishes the app to ..\publish\win-x64 first, then
; compiles this script. Do not run ISCC directly against a stale/missing publish folder.

#define MyAppName "Pool Tournament Manager"
#define MyAppVersion "0.32.0"
#define MyAppPublisher "Pool Tournament Manager"
#define MyAppExeName "PoolTournamentManager.App.exe"
#define MyPublishDir "..\publish\win-x64"

[Setup]
AppId={{94323793-6D72-4045-AA0B-4D1B90B00CB8}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\src\PoolTournamentManager.App\Assets\AppIcon.ico
Compression=lzma2
SolidCompression=yes
OutputDir=output
OutputBaseFilename=PoolTournamentManager-Setup-v{#MyAppVersion}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
