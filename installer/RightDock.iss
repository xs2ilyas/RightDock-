; RightDock installer definition for Inno Setup 6.
; Build with: tools\publish.ps1 -Installer
; The published build is framework-dependent and requires the .NET 5 Desktop Runtime.

#define MyAppName "Right Dock"
#define MyAppVersion "1.9.0"
#define MyAppPublisher "RightDock"
#define MyAppExeName "RightDock.exe"

[Setup]
AppId={{7D9E3B2A-5C64-4D1B-9F0E-2A8B6C4D1E73}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\RightDock
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=RightDock-{#MyAppVersion}-setup
SetupIconFile=..\Resources\RightDock.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
AppMutex=Local\RightDock.SingleInstance
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
VersionInfoVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent