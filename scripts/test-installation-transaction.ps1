param([string]$OutputRoot)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
if (-not $OutputRoot) { $OutputRoot = Join-Path $artifactRoot ('v2-installation-transaction-' + [Guid]::NewGuid().ToString('N')) }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (-not $OutputRoot.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must stay inside repository artifacts.' }
if (Test-Path -LiteralPath $OutputRoot) { throw 'Use a new output directory.' }
New-Item -ItemType Directory $OutputRoot | Out-Null
$publish = Join-Path $OutputRoot 'probe'
dotnet publish (Join-Path $repoRoot 'tools/SteamWatch.InstallationProbe/SteamWatch.InstallationProbe.csproj') -c Release -r win-x64 --self-contained false -o $publish *> (Join-Path $OutputRoot 'build.log')
if ($LASTEXITCODE -ne 0) { throw 'Probe build failed; see retained log.' }
$probe = Join-Path $publish 'SteamWatch.InstallationProbe.exe'
$results = [Collections.Generic.List[object]]::new()
function Run-Probe([string]$Sandbox, [string]$Mode, [int]$Expected) {
    $process = Start-Process -FilePath $probe -ArgumentList @('transaction', ('"' + $artifactRoot + '"'), ('"' + $Sandbox + '"'), $Mode) -WindowStyle Hidden -PassThru -Wait -RedirectStandardError (Join-Path $OutputRoot ((Split-Path $Sandbox -Leaf) + '-' + $Mode + '.log'))
    if ($process.ExitCode -ne $Expected) { throw "Fixture $Mode exited $($process.ExitCode), expected $Expected." }
}
function Set-FixtureAcl([string]$RelativePath, [string]$Sddl) {
    $rights = [Security.AccessControl.RegistryRights]::ReadPermissions -bor [Security.AccessControl.RegistryRights]::ChangePermissions
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($RelativePath, [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree, $rights)
    if (-not $key) { throw 'Controlled ACL key missing.' }
    try {
        $acl = New-Object Security.AccessControl.RegistrySecurity
        $acl.SetSecurityDescriptorSddlForm($Sddl, [Security.AccessControl.AccessControlSections]::Access)
        $key.SetAccessControl($acl)
    }
    finally { $key.Dispose() }
}
foreach ($mode in @('prepared', 'old-renamed', 'new-renamed', 'registered', 'registration-failure', 'registry-denied', 'normal')) {
    $sandbox = Join-Path $OutputRoot $mode
    $aclPath = $null
    $originalAcl = $null
    $relativeAclPath = $null
    try {
        Run-Probe $sandbox 'initialize' 0
        $identity = (Get-Content -LiteralPath (Join-Path $sandbox 'identity.txt') -Raw).Trim()
        if ($identity -notmatch '^[0-9a-f]{32}$') { throw 'Invalid fixture registry identity.' }
        if ($mode -eq 'registry-denied') {
            $aclPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SteamWatch-Validation-${identity}-new_is1"
            # Windows PowerShell 5.1 Get-Acl -LiteralPath fails for Registry provider keys.
            # This fixed prefix plus validated hex GUID contains no wildcard characters.
            $originalAcl = Get-Acl -Path $aclPath
            $deniedAcl = Get-Acl -Path $aclPath
            $relativeAclPath = "Software\Microsoft\Windows\CurrentVersion\Uninstall\SteamWatch-Validation-${identity}-new_is1"
            $originalAcl = $originalAcl.GetSecurityDescriptorSddlForm([Security.AccessControl.AccessControlSections]::Access)
            Set-Content -LiteralPath (Join-Path $sandbox 'registry-original-acl.sddl') -Value $originalAcl -Encoding ASCII
            $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
            $rule = New-Object Security.AccessControl.RegistryAccessRule($sid, [Security.AccessControl.RegistryRights]::SetValue, [Security.AccessControl.AccessControlType]::Deny)
            $deniedAcl.AddAccessRule($rule)
            Set-FixtureAcl $relativeAclPath ($deniedAcl.GetSecurityDescriptorSddlForm([Security.AccessControl.AccessControlSections]::Access))
        }
        $expected = if ($mode -in @('prepared','old-renamed','new-renamed','registered')) { 73 }
            elseif ($mode -in @('registration-failure','registry-denied')) { 12 } else { 0 }
        Run-Probe $sandbox $mode $expected
        if ($mode -ne 'normal') { Run-Probe $sandbox 'recover' 0; Run-Probe $sandbox 'recover' 0 }
        else { Run-Probe $sandbox 'recover' 12 }
        Run-Probe $sandbox 'observe' 0
        $application = (Get-Content -LiteralPath (Join-Path $sandbox 'program/application.txt') -Raw).Trim()
        $phase = (Get-Content -LiteralPath (Join-Path $sandbox 'phase.txt') -Raw).Trim()
        $before = Get-Content -LiteralPath (Join-Path $sandbox 'registration-before.json') -Raw
        $after = Get-Content -LiteralPath (Join-Path $sandbox 'registration-after.json') -Raw
        $snapshotRestored = $before -ceq $after
        $stageName = (Get-Content -LiteralPath (Join-Path $sandbox 'stage.txt') -Raw).Trim()
        $journal = Get-Content -LiteralPath (Join-Path $sandbox 'program.upgrade.json') -Raw | ConvertFrom-Json
        $backup = Join-Path $sandbox ('SteamWatch.previous-' + $journal.Id)
        $dataPreserved = (Get-Content -LiteralPath (Join-Path $sandbox 'independent-data.txt') -Raw) -ceq 'keep-independent'
        if ($mode -eq 'normal') {
            $legacyDataPreserved = (Get-Content -LiteralPath (Join-Path $backup 'legacy-data.txt') -Raw) -ceq 'keep-legacy'
            $registration = $after | ConvertFrom-Json
            $passed = $application -eq 'new' -and $phase -eq 'Committed' -and -not $registration[0].Exists -and $registration[1].Exists -and $legacyDataPreserved -and $dataPreserved
        }
        else {
            $legacyDataPreserved = (Get-Content -LiteralPath (Join-Path $sandbox 'program/legacy-data.txt') -Raw) -ceq 'keep-legacy'
            $passed = $application -eq 'old' -and $phase -eq 'RolledBack' -and $snapshotRestored -and
                (Get-Content -LiteralPath (Join-Path $sandbox "$stageName/application.txt") -Raw) -ceq 'new' -and $legacyDataPreserved -and $dataPreserved
        }
        $results.Add([ordered]@{ mode = $mode; processExitCode = $expected; phase = $phase; application = $application
            registrationSnapshotRestored = $snapshotRestored; independentDataPreserved = $dataPreserved
            legacyDataPreserved = $legacyDataPreserved; repeatedRecoveryChecked = $mode -ne 'normal'; passed = $passed })
        if (-not $passed) { throw "Transaction scenario failed: $mode" }
    }
    finally {
        try {
            if ($originalAcl) {
                Set-FixtureAcl $relativeAclPath $originalAcl
                $restoredAcl = (Get-Acl -Path $aclPath).GetSecurityDescriptorSddlForm([Security.AccessControl.AccessControlSections]::Access)
                $originalRaw = New-Object Security.AccessControl.RawSecurityDescriptor($originalAcl)
                $restoredRaw = New-Object Security.AccessControl.RawSecurityDescriptor($restoredAcl)
                $originalBytes = New-Object byte[] $originalRaw.DiscretionaryAcl.BinaryLength
                $restoredBytes = New-Object byte[] $restoredRaw.DiscretionaryAcl.BinaryLength
                $originalRaw.DiscretionaryAcl.GetBinaryForm($originalBytes, 0)
                $restoredRaw.DiscretionaryAcl.GetBinaryForm($restoredBytes, 0)
                # Windows may add the system-maintained SE_DACL_AUTO_INHERITED flag.
                # All ACE bytes and every other control flag must remain identical.
                $flagDifference = [int]$originalRaw.ControlFlags -bxor [int]$restoredRaw.ControlFlags
                $allowedDifference = [int][Security.AccessControl.ControlFlags]::DiscretionaryAclAutoInherited
                $aclRestored = [Convert]::ToBase64String($originalBytes) -ceq [Convert]::ToBase64String($restoredBytes) -and
                    ($flagDifference -band (-bnot $allowedDifference)) -eq 0
                [ordered]@{ original = $originalAcl; restored = $restoredAcl; accessRulesAndControlFlagsEqual = $aclRestored
                    descriptorsIdentical = $originalAcl -ceq $restoredAcl; automaticInheritanceFlagNormalized = $flagDifference -eq $allowedDifference } |
                    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $sandbox 'acl-restoration.json') -Encoding UTF8
                if (-not $aclRestored) { throw 'Test ACL access rules or protected/inheritance settings changed.' }
            }
            if (Test-Path -LiteralPath (Join-Path $sandbox 'identity.txt')) { Run-Probe $sandbox 'cleanup' 0 }
        }
        finally {
            [ordered]@{ observedAt = [DateTimeOffset]::Now.ToString('o'); scenarios = $results.ToArray()
                scope = 'Controlled text payloads, real isolated HKCU keys, self-exit process interruptions; not power-loss or full WinUI acceptance'
            } | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $OutputRoot 'evidence.json') -Encoding UTF8
        }
    }
}
Write-Output (Join-Path $OutputRoot 'evidence.json')
