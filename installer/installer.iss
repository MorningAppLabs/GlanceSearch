[Setup]
; Basic Application Information
AppName=GlanceSearch
AppVerName=GlanceSearch 1.0.0
AppVersion=1.0.0
AppPublisher=Morning App Labs
AppSupportURL=https://github.com/MorningAppLabs/GlanceSearch
AppUpdatesURL=https://github.com/MorningAppLabs/GlanceSearch/releases

; Install Directory
DefaultDirName={autopf}\GlanceSearch
DefaultGroupName=GlanceSearch
DisableProgramGroupPage=yes

; Icon and Output
SetupIconFile=..\src\GlanceSearch.App\Assets\app.ico
UninstallDisplayIcon={app}\GlanceSearch.exe
OutputDir=..\build
OutputBaseFilename=GlanceSearchSetup-1.0.0

; Compression
Compression=lzma2/max
SolidCompression=yes

; Require Admin Privileges for Program Files install
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start GlanceSearch automatically when Windows starts"; GroupDescription: "Startup Options:"

[Files]
; IMPORTANT: Make sure to publish the project first using:
; dotnet publish src\GlanceSearch.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
Source: "..\src\GlanceSearch.App\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu
Name: "{group}\GlanceSearch"; Filename: "{app}\GlanceSearch.exe"
; Desktop
Name: "{autodesktop}\GlanceSearch"; Filename: "{app}\GlanceSearch.exe"; Tasks: desktopicon
; Startup
Name: "{userstartup}\GlanceSearch"; Filename: "{app}\GlanceSearch.exe"; Tasks: startupicon

[Run]
Filename: "{app}\GlanceSearch.exe"; Description: "{cm:LaunchProgram,GlanceSearch}"; Flags: nowait postinstall skipifsilent
