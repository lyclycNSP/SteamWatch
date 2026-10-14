param([Parameter(Mandatory=$true)][string]$PayloadZip, [Parameter(Mandatory=$true)][string]$Probe,
    [Parameter(Mandatory=$true)][string]$Compiler, [Parameter(Mandatory=$true)][string]$OutputRoot,
    [ValidateSet('fresh','legacy-enabled','legacy-disabled','same-package','upgrade','payload-locked','exit-decline','save-failed','no-pipe','exit-accept','uninstall-cancel','uninstall-failure','registration-denied','install-cancel')][string[]]$Scenarios = @('fresh','legacy-enabled','legacy-disabled','same-package','upgrade','payload-locked','exit-decline','save-failed','no-pipe','exit-accept','uninstall-cancel','uninstall-failure','registration-denied','install-cancel'))
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'release-support.ps1')
$release=Get-ReleaseVersion
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (-not $OutputRoot.StartsWith($artifactRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $OutputRoot)) { throw 'Use a new repository artifacts directory.' }
New-Item -ItemType Directory $OutputRoot | Out-Null
$PayloadZip = (Resolve-Path -LiteralPath $PayloadZip).Path
$Probe = (Resolve-Path -LiteralPath $Probe).Path
$Compiler = (Resolve-Path -LiteralPath $Compiler).Path
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SteamWatchValidationButtons {
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    public static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    public static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
'@
$payloadHash = (Get-FileHash -LiteralPath $PayloadZip).Hash.ToLowerInvariant()
$manifest = @{}
$zip = [IO.Compression.ZipFile]::OpenRead($PayloadZip)
try {
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName.EndsWith('/')) { continue }
        $name = $entry.FullName.Replace('\','/').Substring('SteamWatch-win-x64/'.Length)
        $stream = $entry.Open(); $sha = [Security.Cryptography.SHA256]::Create()
        try { $manifest[$name] = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant() }
        finally { $stream.Dispose(); $sha.Dispose() }
    }
} finally { $zip.Dispose() }
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputRoot 'payload-manifest.json') -Encoding UTF8
function Tree([string]$Path) {
    $hashes = @{}
    if (Test-Path -LiteralPath $Path) {
        Get-ChildItem -LiteralPath $Path -File -Recurse | ForEach-Object { $hashes[$_.FullName.Substring($Path.Length+1)] = (Get-FileHash -LiteralPath $_.FullName).Hash }
    }
    return $hashes
}
function Same($A,$B) {
    if ($A.Count -ne $B.Count) { return $false }
    foreach ($name in $A.Keys) { if ($A[$name] -cne $B[$name]) { return $false } }
    return $true
}
function Key-Snapshot([string]$Path) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($Path)
    try {
        if (-not $key) { return 'absent' }
        return (@($key.GetValueNames() | Sort-Object | ForEach-Object { @{name=$_;kind=[string]$key.GetValueKind($_);value=$key.GetValue($_,$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)} }) | ConvertTo-Json -Compress -Depth 5)
    } finally { if ($key) { $key.Dispose() } }
}
function Link-Target([string]$Path) {
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($Path)
    try { return @($link.TargetPath,$link.Arguments) }
    finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($link) | Out-Null; [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null }
}
function Choose-Confirmation($Dialog,[ValidateSet('6','7')][string]$Id='7') {
    $button=$Dialog.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$Id))
    if(-not $button){throw 'Owned confirmation No button not found.'}
    $pattern=$null
    if($button.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern,[ref]$pattern)){$pattern.Invoke()}
    elseif($button.Current.NativeWindowHandle -ne 0){
        # PS5.1 may expose native controls without InvokePattern. BM_CLICK targets
        # only the verified child-owned No button; this is not a mouse gesture.
        $null=[SteamWatchValidationButtons]::SendMessage([IntPtr]$button.Current.NativeWindowHandle,0x00F5,[IntPtr]::Zero,[IntPtr]::Zero)
    } else {throw 'Owned No button has neither InvokePattern nor a native handle.'}
}
function Write-OldLink([string]$Path,[string]$Target) {
    New-Item -ItemType Directory -Force (Split-Path -Parent $Path) | Out-Null
    $shell=New-Object -ComObject WScript.Shell
    $link=$shell.CreateShortcut($Path)
    try { $link.TargetPath=$Target; $link.Arguments='--old-fixture'; $link.Save() }
    finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($link) | Out-Null; [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null }
}
function Restore-Acl([string]$Path,[string]$Sddl) {
    $rights=[Security.AccessControl.RegistryRights]::ReadPermissions -bor [Security.AccessControl.RegistryRights]::ChangePermissions
    $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($Path,[Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree,$rights)
    try { $acl=New-Object Security.AccessControl.RegistrySecurity; $acl.SetSecurityDescriptorSddlForm($Sddl,[Security.AccessControl.AccessControlSections]::Access); $key.SetAccessControl($acl) }
    finally { $key.Dispose() }
}
function Silent-Run([string]$Exe,[string]$Log) {
    $child = Start-Process -FilePath $Exe -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+$Log+'"')) -WindowStyle Hidden -PassThru
    try { if (-not $child.WaitForExit(120000)) { throw 'Installer exceeded lifetime; no process killed.' }; return $child.ExitCode }
    finally { $child.Dispose() }
}
function Compile([string]$Sandbox,[string]$Identity,[string]$Destination) {
    New-Item -ItemType Directory -Force $Destination | Out-Null
    & $Compiler /Qp "/DProductId=SWT-$Identity" "/DLocalRoot=$Sandbox" "/DValidationRoot=$Sandbox" "/DValidationIdentity=$Identity" `
        "/DAppVersion=$($release.Version)" "/DFileVersion=$($release.FileVersion)" ("/DBuildId="+[Guid]::NewGuid().ToString('N')) "/DOutput=$Destination" "/DPayload=$PayloadZip" "/DPayloadHash=$payloadHash" "/DHelper=$Probe" `
        "/DIcon=$(Join-Path $repoRoot 'src/SteamWatch.App/Assets/AppIcon.ico')" "/DGuide=$(Join-Path $repoRoot 'docs/manual-zh.txt')" `
        (Join-Path $repoRoot 'installer/SteamWatch.iss') *> (Join-Path $Destination 'compile.log')
    if ($LASTEXITCODE -ne 0) { throw 'Formal script compilation failed.' }
    return (Join-Path $Destination 'SteamWatchSetup-win-x64.exe')
}
$realPaths = @('Software\Microsoft\Windows\CurrentVersion\Run','Software\Microsoft\Windows\CurrentVersion\Uninstall\SteamWatch','Software\Microsoft\Windows\CurrentVersion\Uninstall\SteamWatch_is1')
$realBefore = @($realPaths | ForEach-Object { Key-Snapshot $_ })
$realLinks = @((Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'SteamWatch.lnk'),(Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs/SteamWatch'))
$realLinksBefore = @{}
foreach ($path in $realLinks) {
    $realLinksBefore[$path] = if (Test-Path -LiteralPath $path -PathType Leaf) { (Get-FileHash -LiteralPath $path).Hash } else { (Tree $path | ConvertTo-Json -Compress) }
}
$results = [Collections.Generic.List[object]]::new()
foreach ($scenario in $Scenarios) {
    $sandbox = Join-Path $OutputRoot $scenario
    $identity = [Guid]::NewGuid().ToString('N')
    $program = Join-Path $sandbox 'Programs/SteamWatch'
    $data = Join-Path $sandbox 'SteamWatch'
    $legacyPath = "Software\Microsoft\Windows\CurrentVersion\Uninstall\SWT-${identity}-old"
    $innoPath = "Software\Microsoft\Windows\CurrentVersion\Uninstall\SWT-${identity}_is1"
    $startupPath = "Software\SteamWatch\Validation\$identity\Run"
    $server = $null; $locked = $null; $originalAcl=$null; $dialog=$null; $interactiveChild=$null
    New-Item -ItemType Directory -Force $sandbox,$data | Out-Null
    Set-Content -LiteralPath (Join-Path $sandbox 'validation.scope') -Value $identity -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $data 'shared.txt') -Value 'independent-wins' -Encoding ASCII
    try {
        if ($scenario -ne 'fresh') {
            New-Item -ItemType Directory -Force (Join-Path $program 'data') | Out-Null
            Set-Content -LiteralPath (Join-Path $program 'SteamWatch.exe') -Value 'synthetic old version; not launched' -Encoding ASCII
            Copy-Item -LiteralPath (Join-Path $repoRoot 'src/SteamWatch.Installer/Uninstall.ps1') -Destination (Join-Path $program 'Uninstall.ps1')
            Write-OldLink (Join-Path $sandbox 'Desktop/SteamWatch.lnk') (Join-Path $program 'SteamWatch.exe')
            $uninstallLink=[string]::Concat([char]0x5378,[char]0x8F7D,' SteamWatch.lnk')
            $guideLink=[string]::Concat([char]0x64CD,[char]0x4F5C,[char]0x6307,[char]0x5357,'.lnk')
            foreach($name in @('SteamWatch.lnk',$uninstallLink,$guideLink)) { Write-OldLink (Join-Path $sandbox ('StartMenu/SteamWatch/'+$name)) (Join-Path $program 'SteamWatch.exe') }
            Set-Content -LiteralPath (Join-Path $program 'data/shared.txt') -Value 'must-not-overwrite' -Encoding ASCII
            Set-Content -LiteralPath (Join-Path $program 'data/legacy-only.txt') -Value 'retain-source' -Encoding ASCII
            $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($legacyPath)
            try { $key.SetValue('DisplayVersion','1.2.0'); $key.SetValue('InstallLocation',$program); $key.SetValue('UnknownBinary',[byte[]]@(0,128,255)); $key.SetValue('UninstallString',('powershell -File "'+(Join-Path $program 'Uninstall.ps1')+'"')) } finally { $key.Dispose() }
            if ($scenario -ne 'legacy-disabled') {
                $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($startupPath)
                try { $key.SetValue('SteamWatch','"old.exe"'); $key.SetValue('OtherApp','untouched') } finally { $key.Dispose() }
            }
        }
        if ($scenario -in @('exit-decline','exit-accept','save-failed','no-pipe')) {
            Copy-Item -LiteralPath $Probe -Destination (Join-Path $program 'SteamWatch.exe') -Force
            $mode = @{ 'exit-decline'='decline';'exit-accept'='accept';'save-failed'='save-failed';'no-pipe'='no-pipe' }[$scenario]
            $server = Start-Process -FilePath (Join-Path $program 'SteamWatch.exe') -ArgumentList @(('"'+$sandbox+'"'),$identity,'serve',$mode) -WindowStyle Hidden -PassThru
            $null = $server.Handle
            $watch = [Diagnostics.Stopwatch]::StartNew()
            while (-not (Test-Path -LiteralPath (Join-Path $sandbox 'server-ready.txt'))) { if ($server.HasExited -or $watch.Elapsed.TotalSeconds -gt 10) { throw 'Fixture server did not start.' }; Start-Sleep -Milliseconds 100 }
        }
        $before = Tree $program
        $linksBefore=@{Desktop=Tree (Join-Path $sandbox 'Desktop');StartMenu=Tree (Join-Path $sandbox 'StartMenu')}
        $legacyBefore = Key-Snapshot $legacyPath
        $startupBefore = Key-Snapshot $startupPath
        $setup = Compile $sandbox $identity (Join-Path $sandbox 'bundle')
        if ($scenario -eq 'install-cancel') {
            $interactiveChild=Start-Process -FilePath $setup -ArgumentList @('/NORESTART',('/LOG="'+(Join-Path $sandbox 'cancel-install.log')+'"')) -WindowStyle Hidden -PassThru
            $watch=[Diagnostics.Stopwatch]::StartNew()
            do {
                $descendants=@(Get-CimInstance Win32_Process -Filter ("ParentProcessId = "+$interactiveChild.Id))
                foreach($descendant in $descendants){
                    $condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,[int]$descendant.ProcessId)
                    $dialog=[Windows.Automation.AutomationElement]::RootElement.FindFirst([Windows.Automation.TreeScope]::Children,$condition)
                    if($dialog){break}
                }
                if(-not $dialog){Start-Sleep -Milliseconds 100}
            } while(-not $dialog -and -not $interactiveChild.HasExited -and $watch.Elapsed.TotalSeconds -lt 15)
            if(-not $dialog){throw 'Setup welcome not observed.'}
            $ownedPid=$dialog.Current.ProcessId
            $null=[SteamWatchValidationButtons]::PostMessage([IntPtr]$dialog.Current.NativeWindowHandle,0x0010,[IntPtr]::Zero,[IntPtr]::Zero)
            $confirmation=$null;$watch.Restart()
            do {
                $condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$ownedPid)
                $windows=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,$condition)
                for($i=0;$i -lt $windows.Count;$i++){
                    $yes=$windows[$i].FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'6'))
                    if($yes){$confirmation=$windows[$i];break}
                }
                if(-not $confirmation){Start-Sleep -Milliseconds 100}
            } while(-not $confirmation -and -not $interactiveChild.HasExited -and $watch.Elapsed.TotalSeconds -lt 10)
            if($confirmation){$dialog=$confirmation;Choose-Confirmation $confirmation '6'}
            if(-not $interactiveChild.WaitForExit(10000) -or $interactiveChild.ExitCode -eq 0){throw 'Setup cancellation did not finish.'}
            if(-not (Same $before (Tree $program)) -or (Key-Snapshot $legacyPath) -cne $legacyBefore -or (Key-Snapshot $startupPath) -cne $startupBefore -or (Key-Snapshot $innoPath) -cne 'absent'){throw 'Cancelled setup changed application/registration.'}
            $results.Add(@{scenario=$scenario;setupExitCode=$interactiveChild.ExitCode;cancelledBeforePreparation=$true;passed=$true})
            continue
        }
        if($scenario -eq 'registration-denied'){
            $acl=Get-Acl -Path ('HKCU:\'+$startupPath)
            $originalAcl=$acl.GetSecurityDescriptorSddlForm([Security.AccessControl.AccessControlSections]::Access)
            $originalAcl | Set-Content -LiteralPath (Join-Path $sandbox 'original-acl.sddl') -Encoding ASCII
            $sid=[Security.Principal.WindowsIdentity]::GetCurrent().User
            $rule=New-Object Security.AccessControl.RegistryAccessRule($sid,[Security.AccessControl.RegistryRights]::SetValue,[Security.AccessControl.AccessControlType]::Deny)
            $acl.AddAccessRule($rule)
            Restore-Acl $startupPath ($acl.GetSecurityDescriptorSddlForm([Security.AccessControl.AccessControlSections]::Access))
        }
        if ($scenario -eq 'payload-locked') { $locked = [IO.File]::Open((Join-Path $program 'SteamWatch.exe'), 'Open','Read','None') }
        $setupCode = Silent-Run $setup (Join-Path $sandbox 'setup.log')
        if($originalAcl){
            Restore-Acl $startupPath $originalAcl
            $restoredAcl=(Get-Acl -Path ('HKCU:\'+$startupPath)).GetSecurityDescriptorSddlForm([Security.AccessControl.AccessControlSections]::Access)
            $originalRaw=New-Object Security.AccessControl.RawSecurityDescriptor($originalAcl)
            $restoredRaw=New-Object Security.AccessControl.RawSecurityDescriptor($restoredAcl)
            $originalBytes=New-Object byte[] $originalRaw.DiscretionaryAcl.BinaryLength
            $restoredBytes=New-Object byte[] $restoredRaw.DiscretionaryAcl.BinaryLength
            $originalRaw.DiscretionaryAcl.GetBinaryForm($originalBytes,0)
            $restoredRaw.DiscretionaryAcl.GetBinaryForm($restoredBytes,0)
            $flagDifference=[int]$originalRaw.ControlFlags -bxor [int]$restoredRaw.ControlFlags
            $allowedDifference=[int][Security.AccessControl.ControlFlags]::DiscretionaryAclAutoInherited
            $aclRestored=[Convert]::ToBase64String($originalBytes) -ceq [Convert]::ToBase64String($restoredBytes) -and ($flagDifference -band (-bnot $allowedDifference)) -eq 0
            @{original=$originalAcl;restored=$restoredAcl;accessRulesAndControlFlagsEqual=$aclRestored} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $sandbox 'acl-restoration.json') -Encoding UTF8
            if(-not $aclRestored){throw 'Fixture ACL permissions were not restored.'}
            $originalAcl=$null
        }
        if ($locked) { $locked.Dispose(); $locked=$null }
        $expected = if ($scenario -in @('payload-locked','registration-denied')) { 20 } elseif ($scenario -in @('exit-decline','save-failed','no-pipe')) { 7 } else { 0 }
        if ($setupCode -ne $expected) { throw "$scenario returned $setupCode, expected $expected." }
        if ($expected -ne 0) {
            if (-not (Same $before (Tree $program)) -or (Key-Snapshot $legacyPath) -cne $legacyBefore -or (Key-Snapshot $startupPath) -cne $startupBefore -or (Key-Snapshot $innoPath) -cne 'absent') { throw 'Failed installation did not restore old program/registration/startup.' }
            if(-not (Same $linksBefore.Desktop (Tree (Join-Path $sandbox 'Desktop'))) -or -not (Same $linksBefore.StartMenu (Tree (Join-Path $sandbox 'StartMenu')))){throw 'Failed installation did not restore original shortcut bytes.'}
            if ($server -and $server.HasExited) { throw 'Declined/failed/no-pipe process was unexpectedly terminated.' }
            $results.Add(@{scenario=$scenario;setupExitCode=$setupCode;oldStatePreserved=$true;passed=$true})
            continue
        }
        foreach ($name in $manifest.Keys) { if ((Get-FileHash -LiteralPath (Join-Path $program $name)).Hash.ToLowerInvariant() -cne $manifest[$name]) { throw 'Full payload mismatch.' } }
        if (Test-Path -LiteralPath (Join-Path $program 'portable.marker')) { throw 'Installer retained portable marker.' }
        if (Test-Path -LiteralPath (Join-Path $program 'Uninstall.ps1')) { throw 'Installer retained destructive old script.' }
        if ((Key-Snapshot $legacyPath) -cne 'absent') { throw 'Legacy registration was not handed over.' }
        $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($innoPath)
        try { $uninstall = $key.GetValue('UninstallString').Trim('"') } finally { $key.Dispose() }
        $uninstallSignature=Get-AuthenticodeSignature -LiteralPath $uninstall
        if($uninstallSignature.Status -ne 'NotSigned' -or [Diagnostics.FileVersionInfo]::GetVersionInfo($uninstall).ProductVersion.Trim() -ne $release.Version){throw 'Deployed unsigned uninstaller signature/product version mismatch.'}
        $linkFiles = @((Get-ChildItem -LiteralPath (Join-Path $sandbox 'Desktop') -File).FullName) + @((Get-ChildItem -LiteralPath (Join-Path $sandbox 'StartMenu') -Recurse -File).FullName)
        if ($linkFiles.Count -ne 4) { throw 'Expected four actual shortcut files.' }
        $targets = @($linkFiles | ForEach-Object { $value=Link-Target $_; if ($value[1]) { throw 'Legacy shortcut arguments survived.' }; $value[0] })
        if (@($targets | Where-Object { $_ -ieq $uninstall }).Count -ne 1 -or @($targets | Where-Object { $_ -ieq (Join-Path $program 'SteamWatch.exe') }).Count -ne 2) { throw 'Shortcut targets differ from active program/uninstaller.' }
        $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($startupPath)
        try {
            $startup = if ($key) { $key.GetValue('SteamWatch') } else { $null }
            if ($scenario -in @('fresh','legacy-disabled')) { if ($null -ne $startup) { throw 'Installer enabled startup without preference.' } }
            elseif ($startup -cne ('"'+(Join-Path $program 'SteamWatch.exe')+'"') -or $key.GetValue('OtherApp') -cne 'untouched') { throw 'Startup preference or unrelated value changed.' }
        } finally { if ($key) { $key.Dispose() } }
        if ($scenario -ne 'fresh') {
            if ((Get-Content -LiteralPath (Join-Path $data 'shared.txt') -Raw).Trim() -cne 'independent-wins' -or -not (Test-Path -LiteralPath (Join-Path $data 'legacy-only.txt'))) { throw 'Legacy migration overwrote or omitted data.' }
            $backups = @(Get-ChildItem -LiteralPath (Join-Path $sandbox 'Programs') -Directory -Filter 'SteamWatch.previous-*')
            if (-not (Test-Path -LiteralPath (Join-Path $backups[0].FullName 'data/legacy-only.txt'))) { throw 'Legacy source was removed.' }
        }
        if ($server) { if (-not $server.WaitForExit(10000) -or $server.ExitCode -ne 0) { throw 'Accepted normal exit did not finish.' } }
        $repeatCode=$null; $staleCode=$null; $cancelled=$null; $uninstallFailure=$null
        if ($scenario -in @('same-package','upgrade')) {
            $prior = $uninstall
            $repeatSetup = if ($scenario -eq 'same-package') { $setup } else { Compile $sandbox $identity (Join-Path $sandbox 'bundle2') }
            $repeatCode = Silent-Run $repeatSetup (Join-Path $sandbox 'repeat.log')
            if ($repeatCode -ne 0) { throw 'Repeated installation failed.' }
            $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($innoPath)
            try { $uninstall=$key.GetValue('UninstallString').Trim('"') } finally { $key.Dispose() }
            if ($prior -ieq $uninstall) { throw 'Reinstall reused old management directory.' }
            $snapshot=Tree $program
            $staleCode=Silent-Run $prior (Join-Path $sandbox 'stale-uninstall.log')
            if ($staleCode -eq 0 -or -not (Same $snapshot (Tree $program))) { throw 'Stale uninstall changed the active application.' }
        }
        if ($scenario -eq 'uninstall-cancel') {
            $snapshot=Tree $program; $regBefore=Key-Snapshot $innoPath; $startupBefore=Key-Snapshot $startupPath
            $child=Start-Process -FilePath $uninstall -ArgumentList @('/NORESTART',('/LOG="'+(Join-Path $sandbox 'cancel-uninstall.log')+'"')) -WindowStyle Hidden -PassThru
            $interactiveChild=$child
            try {
                $watch=[Diagnostics.Stopwatch]::StartNew(); $dialog=$null
                do {
                    $windows=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.Condition]::TrueCondition)
                    for ($index=0;$index -lt $windows.Count;$index++) {
                        $candidate=$windows[$index]
                        if ($candidate.Current.Name -notmatch 'SteamWatch') { continue }
                        $process=Get-Process -Id $candidate.Current.ProcessId -ErrorAction SilentlyContinue
                        if (-not $process -or -not $process.Path.StartsWith($env:TEMP,[StringComparison]::OrdinalIgnoreCase)) { continue }
                        $ownership=Get-CimInstance Win32_Process -Filter ("ProcessId = "+$process.Id)
                        if ($ownership.ParentProcessId -ne $child.Id) { continue }
                        $controls=$candidate.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition)
                        $names=@(for($j=0;$j -lt $controls.Count;$j++){ $controls[$j].Current.Name })
                        if (@($names | Where-Object { $_ -match 'SteamWatch' -and ($_ -match 'remove|\u5378\u8F7D|\u79FB\u9664|\u5220\u9664') }).Count -gt 0) { $dialog=$candidate;break }
                    }
                    if (-not $dialog) { Start-Sleep -Milliseconds 100 }
                } while (-not $dialog -and -not $child.HasExited -and $watch.Elapsed.TotalSeconds -lt 15)
                if (-not $dialog) { throw 'Uninstall confirmation not observed.' }
                $names | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $sandbox 'confirmation.json') -Encoding UTF8
                if (-not (Same $snapshot (Tree $program)) -or (Key-Snapshot $innoPath) -cne $regBefore -or (Key-Snapshot $startupPath) -cne $startupBefore) { throw 'Uninstall changed state before confirmation.' }
                Choose-Confirmation $dialog
                if (-not $child.WaitForExit(10000) -or $child.ExitCode -eq 0) { throw 'Cancelled uninstall did not exit with cancellation.' }
                if (-not (Same $snapshot (Tree $program)) -or (Key-Snapshot $innoPath) -cne $regBefore -or (Key-Snapshot $startupPath) -cne $startupBefore) { throw 'Cancellation changed installation.' }
                $cancelled=$true
            } finally { if($child.HasExited){$interactiveChild=$null;$child.Dispose()} }
        }
        if ($scenario -eq 'uninstall-failure') {
            $snapshot=Tree $program; $regBefore=Key-Snapshot $innoPath; $startupBefore=Key-Snapshot $startupPath
            $locked=[IO.File]::Open((Join-Path $program 'SteamWatch.exe'),'Open','Read','None')
            $uninstallFailure=Silent-Run $uninstall (Join-Path $sandbox 'failed-uninstall.log')
            $locked.Dispose();$locked=$null
            if ($uninstallFailure -eq 0 -or -not (Same $snapshot (Tree $program)) -or (Key-Snapshot $innoPath) -cne $regBefore -or (Key-Snapshot $startupPath) -cne $startupBefore) { throw 'Failed uninstall did not retain active registration/program/startup.' }
        }
        $dataBefore=Tree $data
        $uninstallCode=Silent-Run $uninstall (Join-Path $sandbox 'uninstall.log')
        if ($uninstallCode -ne 0 -or (Test-Path -LiteralPath $program) -or (Key-Snapshot $innoPath) -cne 'absent' -or -not (Same $dataBefore (Tree $data))) { throw 'Active uninstall failed or modified retained data.' }
        foreach ($path in $linkFiles) { if (Test-Path -LiteralPath $path) { throw 'Uninstall left active shortcut.' } }
        $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($startupPath)
        try { if ($key -and $null -ne $key.GetValue('SteamWatch')) { throw 'Uninstall left startup value.' }; if ($key -and $key.GetValue('OtherApp') -cne 'untouched') { throw 'Uninstall affected another startup value.' } } finally { if ($key) { $key.Dispose() } }
        $results.Add(@{scenario=$scenario;setupExitCode=$setupCode;payloadFileCount=$manifest.Count;fullPayloadMatched=$true;shortcutTargetsVerified=4;dataAndLegacySourcePreserved=$true;startupPreferencePreserved=$true;repeatCode=$repeatCode;staleUninstallCode=$staleCode;interactiveCancellationPreserved=$cancelled;uninstallFailureCode=$uninstallFailure;uninstallCode=$uninstallCode;passed=$true})
    } finally {
        if($interactiveChild){
            if(-not $interactiveChild.HasExited -and $dialog){
                $no=$dialog.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'7'))
                if($no){Choose-Confirmation $dialog}
            }
            if(-not $interactiveChild.WaitForExit(10000)){throw 'Owned interactive window still open; no process killed.'}
            $interactiveChild.Dispose()
        }
        if($originalAcl){Restore-Acl $startupPath $originalAcl}
        if ($locked) { $locked.Dispose() }
        if ($server) { if (-not $server.WaitForExit(25000)) { throw 'Process fixture exceeded lifetime; no process killed.' }; $server.Dispose() }
        foreach ($path in @($legacyPath,$innoPath,$startupPath)) { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKey($path,$false) }
        [ordered]@{observedAt=[DateTimeOffset]::Now.ToString('o');formalScript='installer/SteamWatch.iss';engine='src/SteamWatch.Installer';payloadSha256=$payloadHash;scenarios=$results.ToArray();scope='Same formal Inno script and engine sources; compile-time isolated known-folder roots/registry/startup. Actual LNK/HKCU and current-user Inno, bounded named-pipe process fixtures. Not a clean-system or real-user-profile acceptance.'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputRoot 'evidence.json') -Encoding UTF8
    }
}
for ($index=0;$index -lt $realPaths.Count;$index++) { if ((Key-Snapshot $realPaths[$index]) -cne $realBefore[$index]) { throw 'Real product/startup registry changed.' } }
foreach ($path in $realLinks) {
    $value=if(Test-Path -LiteralPath $path -PathType Leaf){(Get-FileHash -LiteralPath $path).Hash}else{(Tree $path | ConvertTo-Json -Compress)}
    if($value -cne $realLinksBefore[$path]) { throw 'Real desktop/start-menu changed.' }
}
$evidence=Get-Content -LiteralPath (Join-Path $OutputRoot 'evidence.json') -Raw | ConvertFrom-Json
$evidence | Add-Member -NotePropertyName realProfileRegistryAndShortcutsUnchanged -NotePropertyValue $true
$evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputRoot 'evidence.json') -Encoding UTF8
Write-Output (Join-Path $OutputRoot 'evidence.json')
