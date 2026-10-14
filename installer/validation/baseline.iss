; WU-DELIVERY-004 experiment only. Never use this identity for production.
#ifndef Sandbox
  #error Sandbox is required
#endif
#ifndef Payload
  #error Payload is required
#endif
#ifndef Output
  #error Output is required
#endif
#ifndef Identity
  #error Identity is required
#endif
#ifndef Revision
  #define Revision "1"
#endif
[Setup]
AppId=SteamWatch-Validation-{#Identity}
AppName=SteamWatch isolated installation experiment
AppVersion={#Revision}
DefaultDirName={#Sandbox}\program
UsePreviousAppDir=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=no
RestartApplications=no
RestartIfNeededByRun=no
DisableDirPage=yes
DisableProgramGroupPage=yes
CreateAppDir=yes
OutputDir={#Output}
OutputBaseFilename=baseline-{#Revision}
Compression=lzma2
WizardStyle=modern
UninstallDisplayName=SteamWatch isolated experiment {#Identity}
[Files]
Source: "{#Payload}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Code]
function SafeDirectory: Boolean;
begin
  Result := CompareText(RemoveBackslashUnlessRoot(ExpandFileName(ExpandConstant('{app}'))),
    RemoveBackslashUnlessRoot(ExpandFileName('{#Sandbox}\program'))) = 0;
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
#ifdef Coordinator
var
  ExitCode: Integer;
#endif
begin
  Result := '';
  if not SafeDirectory then Result := 'Validation package refused a directory outside its fixed sandbox.';
#ifdef Coordinator
  if Result <> '' then Exit;
  if not Exec('{#Coordinator}', 'coordinate "{#ArtifactRoot}" "{#Sandbox}"', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    Result := 'Could not start the isolated exit coordinator.'
  else if ExitCode <> 0 then
    Result := Format('Normal exit was not confirmed (%d); payload was not replaced.', [ExitCode]);
#endif
end;
function InitializeUninstall: Boolean;
begin
  Result := SafeDirectory;
end;
#ifdef FailPostCopyCallback
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    RaiseException('Injected post-copy callback failure (validation only).');
end;
#endif
