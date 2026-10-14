; DELIVERY-004: fixed-sandbox complete-payload experiment, never a production identity.
#ifndef Sandbox
  #error Sandbox is required
#endif
#ifndef HoldMillis
  #define HoldMillis 0
#endif
[Setup]
AppId=SWV-{#Identity}
AppName=SteamWatch complete-payload validation
AppVersion=1.2.0
DefaultDirName={#Sandbox}\installers\{#BuildId}
UsePreviousAppDir=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=no
RestartApplications=no
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir={#Output}
OutputBaseFilename=transactional-{#BuildId}
WizardStyle=modern
Compression=lzma2/fast
UninstallDisplayIcon={#Sandbox}\app\SteamWatch.exe
[Files]
Source: "{#Payload}"; DestName: "payload.zip"; Flags: dontcopy nocompression
Source: "{#Helper}\SteamWatch.InstallationProbe.exe"; DestDir: "{app}\tools"; Flags: ignoreversion
Source: "{#Helper}\SteamWatch.InstallationProbe.dll"; DestDir: "{app}\tools"; Flags: ignoreversion
Source: "{#Helper}\SteamWatch.InstallationProbe.deps.json"; DestDir: "{app}\tools"; Flags: ignoreversion
Source: "{#Helper}\SteamWatch.InstallationProbe.runtimeconfig.json"; DestDir: "{app}\tools"; Flags: ignoreversion
Source: "{#Configuration}"; DestDir: "{app}\tools"; DestName: "bundle-config.json"; Flags: ignoreversion
[Code]
var
  Prepared, Committed, FinalizeFailed: Boolean;
  LifecycleMutex: THandle;

function NativeCreateMutex(Attributes: NativeUInt; InitialOwner: BOOL; Name: String): THandle;
  external 'CreateMutexW@kernel32.dll stdcall';
function NativeCloseHandle(Handle: THandle): BOOL;
  external 'CloseHandle@kernel32.dll stdcall';
procedure NativeExitProcess(ExitCode: UINT);
  external 'ExitProcess@kernel32.dll stdcall';

function AcquireLifecycle: Boolean;
var
  LastError: DWORD;
begin
  { Same directory means same mutex, including different build or AppId values. }
  LifecycleMutex := NativeCreateMutex(0, False,
    'Global\SteamWatch.Validation.' + GetSHA256OfString(Lowercase('{#Sandbox}')));
  LastError := DLLGetLastError;
  Result := (LifecycleMutex <> 0) and (LastError <> 183);
  if not Result then begin
    if LifecycleMutex <> 0 then NativeCloseHandle(LifecycleMutex);
    LifecycleMutex := 0;
    Log('Another install/uninstall owns the lifecycle mutex; no payload operation performed.');
  end;
end;

procedure ReleaseLifecycle;
begin
  if LifecycleMutex <> 0 then NativeCloseHandle(LifecycleMutex);
  LifecycleMutex := 0;
end;

function InitializeSetup: Boolean;
begin
  Result := AcquireLifecycle;
  if Result then begin
    #if Int(HoldMillis) > 0
      SaveStringToFile('{#Sandbox}\lifecycle-held.txt', 'setup', False);
      Sleep({#HoldMillis});
    #endif
  end;
end;

function SafeDirectory: Boolean;
begin
  Result := CompareText(RemoveBackslashUnlessRoot(ExpandFileName(ExpandConstant('{app}'))),
    RemoveBackslashUnlessRoot(ExpandFileName('{#Sandbox}\installers\{#BuildId}'))) = 0;
end;

function RunHelper(Mode: String; Temporary: Boolean): Integer;
var
  Tools, Args: String;
begin
  Result := 99;
  if Temporary then Tools := ExpandConstant('{tmp}') else Tools := ExpandConstant('{app}\tools');
  Args := 'bundle "{#ArtifactRoot}" "{#Sandbox}" ' + Mode + ' "' + Tools + '\bundle-config.json"';
  if Mode = 'prepare' then Args := Args + ' "' + ExpandConstant('{tmp}\payload.zip') + '"';
  if not Exec(Tools + '\SteamWatch.InstallationProbe.exe', Args, '', SW_HIDE, ewWaitUntilTerminated, Result) then Result := 99;
  Log(Format('Bundle operation %s returned %d', [Mode, Result]));
  { The fixture self-exited at a durable seam. Skip Inno's cleanup as well. }
  if Result = 73 then NativeExitProcess(73);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not SafeDirectory then begin Result := 'Validation target directory was changed; no payload replaced.'; Exit; end;
  if Prepared then Exit;
  ExtractTemporaryFile('SteamWatch.InstallationProbe.exe');
  ExtractTemporaryFile('SteamWatch.InstallationProbe.dll');
  ExtractTemporaryFile('SteamWatch.InstallationProbe.deps.json');
  ExtractTemporaryFile('SteamWatch.InstallationProbe.runtimeconfig.json');
  ExtractTemporaryFile('bundle-config.json');
  ExtractTemporaryFile('payload.zip');
  if RunHelper('prepare', True) <> 0 then begin Result := 'Normal exit or full-payload preparation failed; previous program preserved.'; Exit; end;
  Prepared := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    if RunHelper('finalize', True) = 0 then Committed := True
    else begin
      FinalizeFailed := True;
      RunHelper('rollback', True);
      WizardForm.FinishedLabel.Caption := 'Installation did not commit. Inspect the validation logs and recovery journal.';
      RaiseException('Directory/registration transaction failed; inspect recovery journal.');
    end;
  end;
end;

function GetCustomSetupExitCode: Integer;
begin
  Result := 0;
  if FinalizeFailed then Result := 20;
end;

procedure DeinitializeSetup;
begin
  try
    if Prepared and not Committed then RunHelper('rollback', True);
  finally
    ReleaseLifecycle;
  end;
end;

function InitializeUninstall: Boolean;
begin
  { Prototype is intentionally limited to silent sandbox verification. }
  Result := False;
  if not UninstallSilent or not SafeDirectory then Exit;
  if not AcquireLifecycle then Exit;
  if RunHelper('active', False) <> 0 then Exit;
  Result := RunHelper('uninstall', False) = 0;
end;

procedure DeinitializeUninstall;
begin
  ReleaseLifecycle;
end;
