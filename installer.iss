#define AppName      "PureUpdate"
#define AppVersion   "1.7.0"
#define AppPublisher "Heiphaistos"
#define AppURL       "https://github.com/heiphaistos44-crypto/PureUpdate-"
#define AppExeName   "PureUpdate.exe"
#define PublishDir   "publish"

[Setup]
AppId={{A7F3C2D1-8B4E-4F6A-9C3D-1E2F5A7B8C9D}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
LicenseFile=
OutputDir=installer_output
OutputBaseFilename=PureUpdate_v{#AppVersion}_win-x64_Setup
SetupIconFile=Resources\PureUpdate.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=120
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
UninstallDisplayName={#AppName} {#AppVersion}
UninstallDisplayIcon={app}\{#AppExeName}
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} — Gestionnaire de mises à jour Windows

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
Source: "{#PublishDir}\{#AppExeName}";           DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";            Filename: "{app}\{#AppExeName}"; Comment: "Gestionnaire de mises à jour Windows"
Name: "{group}\Désinstaller {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";      Filename: "{app}\{#AppExeName}"; Comment: "Gestionnaire de mises à jour Windows"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\PureUpdate"
Type: filesandordirs; Name: "{app}\.logs"
Type: files; Name: "{app}\hidden_updates.json"
Type: dirifempty; Name: "{app}"
