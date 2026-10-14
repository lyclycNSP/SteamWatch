; Formal package. Overrides are compile-time only, used by the isolated integration harness.
#ifndef ProductId
  #define ProductId "SteamWatch"
#endif
#ifndef LocalRoot
  #define LocalRoot "{localappdata}"
#endif
[Setup]
AppId={#ProductId}
AppName=SteamWatch
AppVersion={#AppVersion}
#ifndef FileVersion
  #define FileVersion AppVersion
#endif
VersionInfoVersion={#FileVersion}
VersionInfoTextVersion={#FileVersion}
VersionInfoProductVersion={#FileVersion}
VersionInfoProductTextVersion={#AppVersion}
#ifdef ReleaseSigning
SignTool=SteamWatchRelease
SignedUninstaller=yes
SignedUninstallerDir={#SignedUninstallerOutput}
SignToolRunMinimized=yes
#else
SignedUninstaller=no
#endif
AppPublisher=SteamWatch
DefaultDirName={code:GetManagement}
UsePreviousAppDir=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=no
RestartApplications=no
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableWelcomePage=no
WizardStyle=modern
Compression=lzma2/fast
SolidCompression=yes
SetupIconFile={#Icon}
OutputDir={#Output}
OutputBaseFilename=SteamWatchSetup-win-x64
UninstallDisplayIcon={#LocalRoot}\Programs\SteamWatch\SteamWatch.exe
UninstallDisplayName=SteamWatch
InfoBeforeFile={#Guide}
[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
[Messages]
ConfirmUninstall=是否卸载 %1？%n%n将移除当前程序、快捷方式和开机启动项。时长记录、规则、认证配置及用于恢复的旧副本会保留。
[Files]
Source: "{#Payload}"; DestName: "payload.zip"; Flags: dontcopy nocompression
Source: "{#Helper}"; DestDir: "{app}"; DestName: "SteamWatch.InstallationHelper.exe"; Flags: ignoreversion
[Run]
Filename: "{#LocalRoot}\Programs\SteamWatch\SteamWatch.exe"; Description: "启动 SteamWatch"; Flags: nowait postinstall skipifsilent; Check: CanLaunch
[Code]
var
  Prepared, Committed, FinalizeFailed: Boolean;
  LifecycleMutex: THandle;
  Management: String;

function NativeCreateMutex(Attributes: NativeUInt; InitialOwner: BOOL; Name: String): THandle;
  external 'CreateMutexW@kernel32.dll stdcall';
function NativeCloseHandle(Handle: THandle): BOOL;
  external 'CloseHandle@kernel32.dll stdcall';

function AcquireLifecycle: Boolean;
var LastError: DWORD;
begin
  LifecycleMutex := NativeCreateMutex(0, False, 'Global\SteamWatch.Installation.' +
    GetSHA256OfString(Lowercase(ExpandConstant('{#LocalRoot}\Programs\SteamWatch'))));
  LastError := DLLGetLastError;
  Result := (LifecycleMutex <> 0) and (LastError <> 183);
  if not Result then begin
    if LifecycleMutex <> 0 then NativeCloseHandle(LifecycleMutex);
    LifecycleMutex := 0;
    SuppressibleMsgBox('另一个 SteamWatch 安装或卸载正在运行。请等待完成后重试。', mbError, MB_OK, IDOK);
  end;
end;

procedure ReleaseLifecycle;
begin
  if LifecycleMutex <> 0 then NativeCloseHandle(LifecycleMutex);
  LifecycleMutex := 0;
end;

function GetManagement(Param: String): String;
var IdentitySource, IdentityHash: String;
begin
  if Management = '' then begin
    Log('Generating independent installation management identity.');
    IdentitySource := ExpandConstant('{tmp}') + '{#BuildId}' + GetDateTimeString('yyyymmddhhnnsszzz', '-', ':');
    IdentityHash := GetSHA256OfString(IdentitySource);
    Management := ExpandConstant('{#LocalRoot}\Programs\SteamWatch.Installers\') +
      Copy(IdentityHash, 1, 32);
    Log('Independent installation management directory selected.');
  end;
  Result := Management;
end;

function InitializeSetup: Boolean;
begin
  Result := AcquireLifecycle;
end;

function CanLaunch: Boolean;
begin
  Result := Committed;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := '程序位置：' + ExpandConstant('{#LocalRoot}\Programs\SteamWatch') + NewLine +
    '数据位置：' + ExpandConstant('{#LocalRoot}\SteamWatch') + NewLine +
    '首次安装默认关闭开机启动；升级保留现有设置。卸载保留时长、规则和认证配置。';
end;

function RunHelper(Mode: String; Temporary: Boolean): Integer;
var Tools, Args: String;
begin
  Result := 99;
  if Temporary then Tools := ExpandConstant('{tmp}') else Tools := ExpandConstant('{app}');
  Args := Mode + ' "' + ExpandConstant('{app}') + '"';
  #ifdef ValidationRoot
    Args := '"{#ValidationRoot}" {#ValidationIdentity} ' + Args;
  #endif
  if Mode = 'prepare' then Args := Args + ' "' + ExpandConstant('{tmp}\payload.zip') + '" {#PayloadHash}';
  Log('SteamWatch helper arguments: ' + Args);
  if not Exec(Tools + '\SteamWatch.InstallationHelper.exe', Args, '', SW_HIDE, ewWaitUntilTerminated, Result) then Result := 99;
  Log(Format('SteamWatch operation %s returned %d', [Mode, Result]));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if CompareText(RemoveBackslashUnlessRoot(ExpandFileName(ExpandConstant('{app}'))), GetManagement('')) <> 0 then begin
    Result := 'SteamWatch 使用固定的当前用户安装位置，不能通过命令行更改目录。';
    Exit;
  end;
  if Prepared then Exit;
  ExtractTemporaryFile('SteamWatch.InstallationHelper.exe');
  ExtractTemporaryFile('payload.zip');
  if RunHelper('prepare', True) <> 0 then begin
    Result := '无法准备安装：退出被取消、数据保存失败，或仍在运行的旧版不支持升级退出。请正常退出 SteamWatch 后重试。诊断日志：{#LocalRoot}\Programs\SteamWatch.installation.log';
    Exit;
  end;
  Prepared := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    if RunHelper('finalize', True) = 0 then Committed := True
    else begin
      FinalizeFailed := True;
      RunHelper('rollback', True);
      WizardForm.FinishedLabel.Caption := '安装未完成。旧程序已恢复，或已保留恢复日志；请重新运行安装包。详情见 SteamWatch.installation.log。';
      RaiseException('SteamWatch 安装失败，本次更新没有成功提交。');
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
  Result := False;
  if not AcquireLifecycle then Exit;
  Result := RunHelper('active', False) = 0;
  if not Result then SuppressibleMsgBox('此卸载入口已被后续安装替代或已失效。请从 Windows 设置或 SteamWatch 设置使用当前卸载入口。', mbError, MB_OK, IDOK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  { Inno calls this after confirmation, before any file or registry removal.
    Exceptions here are fatal, preserving Inno metadata for a retry. }
  if CurUninstallStep = usAppMutexCheck then
    if RunHelper('uninstall', False) <> 0 then
      RaiseException('无法安全卸载 SteamWatch，卸载信息已保留。请正常退出应用后重试。详情见 SteamWatch.installation.log。');
end;

procedure DeinitializeUninstall;
begin
  ReleaseLifecycle;
end;
