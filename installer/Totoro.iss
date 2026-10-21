; Totoro's installer (Inno Setup 6). It installs the app as published (self-contained: .NET and the Windows App SDK's
; runtime come with it) for the current user, without admin rights.
;
; Build it with build.ps1, which publishes the app first; or by hand:
;   iscc /DAppVersion=2.0.0 /DPublishDir=<the publish folder> Totoro.iss

#ifndef AppVersion
  #define AppVersion "2.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\src\Totoro\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"
#endif
#ifndef OutputDir
  #define OutputDir "output"
#endif

#define AppName "Totoro"
#define AppExe "Totoro.exe"

[Setup]
; Identifies the app to Windows (Apps & features) across versions: never change it.
AppId={{6F1B8C2E-4A57-4D0B-9C3E-2A8F7E5D1B94}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=athulrajts
AppPublisherURL=https://github.com/insomniachi/Totoro
AppSupportURL=https://github.com/insomniachi/Totoro/issues
AppUpdatesURL=https://github.com/insomniachi/Totoro/releases
VersionInfoVersion={#AppVersion}

; For the current user, in %LocalAppData%\Programs: no admin rights needed, and the app can write next to itself
; (libvlc keeps its plugin cache there). The user's settings and data are elsewhere (%LocalAppData%\Totoro), and stay
; when it's uninstalled.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=yes

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

SetupIconFile=..\src\Totoro\Assets\Totoro.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
LicenseFile=..\LICENSE
WizardStyle=modern

OutputDir={#OutputDir}
OutputBaseFilename=Totoro-{#AppVersion}-setup-x64
Compression=lzma2/max
SolidCompression=yes

; Updating while it runs: close it first.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; libvlc loads every plugin in its folder: ones an earlier version had and this one leaves out would be left behind
; otherwise, and loaded.
Type: filesandordirs; Name: "{app}\libvlc"
; rqbit, which came with earlier versions: it's downloaded into the app's data now.
Type: filesandordirs; Name: "{app}\Tools\rqbit"
Type: dirifempty; Name: "{app}\Tools"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
; Installed silently, it's an update from the app (UpdateService), which closed for it: start it again.
Filename: "{app}\{#AppExe}"; Flags: nowait runasoriginaluser; Check: WizardSilent

[UninstallDelete]
; What the app wrote next to itself: libvlc's plugin cache.
Type: files; Name: "{app}\libvlc\win-x64\plugins\plugins.dat"
