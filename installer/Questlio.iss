#ifndef AppVersion
  #define AppVersion "0.30.3"
#endif
#ifndef PublishDir
  #define PublishDir "..\dist\app"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

[Setup]
AppId={{7359EC34-9C25-42CF-9D20-1A78CA0E3F33}
AppName=Questlio
AppVersion={#AppVersion}
AppPublisher=StokeleyDev
AppPublisherURL=https://github.com/StokeleyDev/Questlio
AppSupportURL=https://github.com/StokeleyDev/Questlio/issues
AppUpdatesURL=https://github.com/StokeleyDev/Questlio/releases
DefaultDirName={localappdata}\Programs\Questlio
DefaultGroupName=Questlio
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
AppMutex=Local\Questlio.Desktop.v1
SetupMutex=Local\Questlio.Setup
OutputDir={#OutputDir}
OutputBaseFilename=Questlio-{#AppVersion}-Setup
SetupIconFile=..\desktop\assets\questlio.ico
UninstallDisplayIcon={app}\Questlio.exe
UninstallDisplayName=Questlio
Uninstallable=yes
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Questlio"; Filename: "{app}\Questlio.exe"; WorkingDir: "{app}"
Name: "{group}\Uninstall Questlio"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Questlio"; Filename: "{app}\Questlio.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Questlio.exe"; Description: "{cm:LaunchProgram,Questlio}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  StartupValue: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Questlio', StartupValue) then
      if CompareText(StartupValue, '"' + ExpandConstant('{app}\Questlio.exe') + '" --background') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Questlio');
  { Library, backups and preferences under LocalAppData\Questlio are preserved. }
end;
