; Compile after Meeting2Text.iss. Pass /DInstallerBytes=<size> on the ISCC command line.
#define OfflineName "Meeting2Text-Setup-x64.exe"
#define OfflineHash GetSHA256OfFile(SourcePath + "\..\dist\" + OfflineName)
[Setup]
AppName=Meeting2Text
AppVersion=1.2.0
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
CreateAppDir=no
Uninstallable=no
DisableWelcomePage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
OutputDir=..\dist
OutputBaseFilename=Meeting2Text-WebSetup-x64
[Languages]
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
[Files]
Source: "https://github.com/akiooooo/meeting-2-txt-mp3/releases/download/v1.2.0/{#OfflineName}"; DestDir: "{tmp}"; DestName: "{#OfflineName}"; ExternalSize: {#InstallerBytes}; Hash: "{#OfflineHash}"; Flags: external download ignoreversion
[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  Code: Integer;
  Args: String;
begin
  if CurStep = ssPostInstall then begin
    Args := '/NORESTART';
    if WizardSilent then Args := Args + ' /VERYSILENT';
    if not Exec(ExpandConstant('{tmp}\{#OfflineName}'), Args, '', SW_SHOW, ewWaitUntilTerminated, Code) or (Code <> 0) then
      RaiseException(Format('Не удалось завершить установку (код %d).', [Code]));
  end;
end;
