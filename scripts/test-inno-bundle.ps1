param([Parameter(Mandatory=$true)][string]$PayloadZip, [string]$OutputRoot, [string]$Compiler,
    [ValidateSet('fresh','legacy-enabled','after-switch','after-shortcut','exit-decline','exit-accept','repeat-upgrade','repeat-failure','lifecycle-busy','payload-locked','native-launch','crash-prepared','crash-old-renamed','crash-new-renamed','crash-registered','crash-committed')][string[]]$Scenarios = @('fresh','legacy-enabled','after-switch','after-shortcut','exit-decline','exit-accept','repeat-upgrade','repeat-failure','lifecycle-busy','payload-locked','native-launch','crash-prepared','crash-old-renamed','crash-new-renamed','crash-registered','crash-committed'))
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$PayloadZip = (Resolve-Path -LiteralPath $PayloadZip).Path
if (-not $OutputRoot) { $OutputRoot = Join-Path $artifactRoot ('v2-inno-bundle-' + [Guid]::NewGuid().ToString('N')) }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (-not $OutputRoot.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use repository artifacts for the experiment.' }
if (Test-Path -LiteralPath $OutputRoot) { throw 'Use a new output directory.' }
New-Item -ItemType Directory $OutputRoot | Out-Null
if (-not $Compiler) { $Compiler = & (Join-Path $PSScriptRoot 'prepare-inno-toolchain.ps1') }
$helper = Join-Path $OutputRoot 'helper'
dotnet publish (Join-Path $repoRoot 'tools/SteamWatch.InstallationProbe/SteamWatch.InstallationProbe.csproj') -c Release -r win-x64 --self-contained false -o $helper *> (Join-Path $OutputRoot 'build.log')
if ($LASTEXITCODE -ne 0) { throw 'Helper build failed.' }
$payloadHash = (Get-FileHash -LiteralPath $PayloadZip).Hash.ToLowerInvariant()
Add-Type -AssemblyName System.IO.Compression.FileSystem
$payloadFiles = @{}
$zip = [IO.Compression.ZipFile]::OpenRead($PayloadZip)
try {
    foreach ($entry in $zip.Entries) {
        $name = $entry.FullName.Replace('\','/')
        if ($name.EndsWith('/')) { continue }
        if (-not $name.StartsWith('SteamWatch-win-x64/')) { throw 'Expected a flat installer payload ZIP.' }
        $name = $name.Substring('SteamWatch-win-x64/'.Length)
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $payloadFiles[$name] = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant() }
        finally { $stream.Dispose(); $sha.Dispose() }
    }
}
finally { $zip.Dispose() }
$payloadFiles | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputRoot 'payload-manifest.json') -Encoding UTF8
function Tree-Hashes([string]$Directory) {
    $result = @{}
    if (Test-Path -LiteralPath $Directory) {
        Get-ChildItem -LiteralPath $Directory -File -Recurse | ForEach-Object { $result[$_.FullName.Substring($Directory.Length + 1).Replace('\','/')] = (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant() }
    }
    return $result
}
function Same-Hashes($First, $Second) {
    if ($First.Count -ne $Second.Count) { return $false }
    foreach ($name in $First.Keys) { if ($First[$name] -cne $Second[$name]) { return $false } }
    return $true
}
function Write-Link([string]$Path, [string]$Target, [string]$Arguments = '') {
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($Path)
    try { $link.TargetPath = $Target; $link.Arguments = $Arguments; $link.Save() }
    finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($link) | Out-Null; [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null }
}
function Link-Target([string]$Path) {
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($Path)
    try { return $link.TargetPath }
    finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($link) | Out-Null; [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null }
}
function Main-StartupFingerprint {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
    try {
        $value = if ($key) { $key.GetValue('SteamWatch', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) } else { $null }
        if ($null -eq $value) { return 'absent' }
        $sha = [Security.Cryptography.SHA256]::Create()
        try { return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes([string]$value))) }
        finally { $sha.Dispose() }
    }
    finally { if ($key) { $key.Dispose() } }
}
$startupBefore = Main-StartupFingerprint
$results = [Collections.Generic.List[object]]::new()
foreach ($scenario in $Scenarios) {
    $sandbox = Join-Path $OutputRoot $scenario
    $program = Join-Path $sandbox 'app'
    $links = Join-Path $sandbox 'links'
    $identity = [Guid]::NewGuid().ToString('N')
    $buildId = [Guid]::NewGuid().ToString('N')
    $legacyPath = "Software\Microsoft\Windows\CurrentVersion\Uninstall\SteamWatch-Validation-${identity}-legacy"
    $newPath = "Software\Microsoft\Windows\CurrentVersion\Uninstall\SWV-${identity}_is1"
    $startupPath = "Software\SteamWatch\Validation\${identity}\Run"
    $server = $null
    $payloadLock = $null
    $native = $null
    New-Item -ItemType Directory -Force $sandbox, $links, (Join-Path $sandbox 'data') | Out-Null
    Set-Content -LiteralPath (Join-Path $sandbox 'portable.marker') -Value 'Isolated complete-payload experiment' -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $sandbox 'data/shared.txt') -Value 'independent-wins' -Encoding ASCII
    $dataBefore = Tree-Hashes (Join-Path $sandbox 'data')
    try {
        if ($scenario -ne 'fresh') {
            New-Item -ItemType Directory -Force (Join-Path $program 'data') | Out-Null
            Set-Content -LiteralPath (Join-Path $program 'SteamWatch.exe') -Value 'old synthetic executable; never launched' -Encoding ASCII
            Copy-Item -LiteralPath (Join-Path $repoRoot 'src/SteamWatch.Installer/Uninstall.ps1') -Destination (Join-Path $program 'Uninstall.ps1')
            Set-Content -LiteralPath (Join-Path $program 'data/shared.txt') -Value 'legacy-must-not-overwrite' -Encoding ASCII
            Set-Content -LiteralPath (Join-Path $program 'data/legacy-only.txt') -Value 'keep-legacy' -Encoding ASCII
            $legacy = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($legacyPath)
            try { $legacy.SetValue('DisplayVersion','1.2.0'); $legacy.SetValue('InstallLocation',$program); $legacy.SetValue('UninstallString',('powershell -NoProfile -File "' + (Join-Path $program 'Uninstall.ps1') + '"')) }
            finally { $legacy.Dispose() }
            foreach ($name in @('desktop.lnk','start-menu.lnk','uninstall.lnk','guide.lnk')) { Write-Link (Join-Path $links $name) (Join-Path $program 'SteamWatch.exe') '--old-fixture' }
            $startup = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($startupPath)
            try { $startup.SetValue('SteamWatch','"old-fixture-path.exe"') } finally { $startup.Dispose() }
        }
        $before = Tree-Hashes $program
        $linksBefore = Tree-Hashes $links
        $configuration = Join-Path $sandbox 'bundle-config.json'
        $failureMode = if ($scenario -in @('after-switch','after-shortcut') -or $scenario.StartsWith('crash-')) { $scenario } else { 'none' }
        [ordered]@{ ArtifactRoot=$artifactRoot; Sandbox=$sandbox; Identity=$identity; BuildId=$buildId; PayloadSha256=$payloadHash; FailureMode=$failureMode } |
            ConvertTo-Json | Set-Content -LiteralPath $configuration -Encoding UTF8
        & $Compiler /Qp "/DSandbox=$sandbox" "/DArtifactRoot=$artifactRoot" "/DIdentity=$identity" "/DBuildId=$buildId" "/DOutput=$sandbox" "/DPayload=$PayloadZip" "/DHelper=$helper" "/DConfiguration=$configuration" (Join-Path $repoRoot 'installer/validation/transactional.iss') *> (Join-Path $sandbox 'compile.log')
        if ($LASTEXITCODE -ne 0) { throw 'Complete-payload installer compilation failed.' }
        if ($scenario -in @('exit-decline','exit-accept')) {
            $fixtureTools = Join-Path $sandbox 'fixture-tools'
            New-Item -ItemType Directory $fixtureTools | Out-Null
            Get-ChildItem -LiteralPath $helper -File | Where-Object Extension -in @('.exe','.dll','.json') | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $fixtureTools }
            $mode = if ($scenario -eq 'exit-accept') { 'accept' } else { 'decline' }
            $server = Start-Process -FilePath (Join-Path $fixtureTools 'SteamWatch.InstallationProbe.exe') -ArgumentList @('serve',('"'+$artifactRoot+'"'),('"'+$sandbox+'"'),$mode) -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $sandbox 'server-error.log')
            $null = $server.Handle
            $watch = [Diagnostics.Stopwatch]::StartNew()
            while (-not (Test-Path -LiteralPath (Join-Path $sandbox 'target.json'))) { if ($server.HasExited -or $watch.Elapsed.TotalSeconds -gt 10) { throw 'Exit fixture did not start.' }; Start-Sleep -Milliseconds 100 }
        }
        if ($scenario -eq 'payload-locked') { $payloadLock = [IO.File]::Open((Join-Path $program 'SteamWatch.exe'), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None) }
        $setup = Start-Process -FilePath (Join-Path $sandbox "transactional-$buildId.exe") -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $sandbox 'setup.log')+'"')) -WindowStyle Hidden -PassThru -Wait
        if ($payloadLock) { $payloadLock.Dispose(); $payloadLock = $null }
        $expectedCode = if ($scenario.StartsWith('crash-')) { 73 } elseif ($scenario -in @('after-switch','after-shortcut','payload-locked')) { 20 } elseif ($scenario -eq 'exit-decline') { 7 } else { 0 }
        if ($setup.ExitCode -ne $expectedCode) { throw "Scenario $scenario exited $($setup.ExitCode), expected $expectedCode." }
        $interruption = $null
        if ($scenario.StartsWith('crash-')) {
            $journalPath = Join-Path $sandbox 'app.upgrade.json'
            $crashedJournal = Get-Content -LiteralPath $journalPath -Raw | ConvertFrom-Json
            $crashedBuild = $buildId
            $crashedFiles = Tree-Hashes $program
            $crashedLinks = Tree-Hashes $links
            $buildId = [Guid]::NewGuid().ToString('N')
            $retryConfig = Join-Path $sandbox 'retry-config.json'
            [ordered]@{ ArtifactRoot=$artifactRoot; Sandbox=$sandbox; Identity=$identity; BuildId=$buildId; PayloadSha256=$payloadHash; FailureMode='none' } | ConvertTo-Json | Set-Content -LiteralPath $retryConfig -Encoding UTF8
            & $Compiler /Qp "/DSandbox=$sandbox" "/DArtifactRoot=$artifactRoot" "/DIdentity=$identity" "/DBuildId=$buildId" "/DOutput=$sandbox" "/DPayload=$PayloadZip" "/DHelper=$helper" "/DConfiguration=$retryConfig" (Join-Path $repoRoot 'installer/validation/transactional.iss') *> (Join-Path $sandbox 'retry-compile.log')
            if ($LASTEXITCODE -ne 0) { throw 'Recovery installer compilation failed.' }
            $retry = Start-Process -FilePath (Join-Path $sandbox "transactional-$buildId.exe") -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $sandbox 'retry-setup.log')+'"')) -WindowStyle Hidden -PassThru -Wait
            if ($retry.ExitCode -ne 0) { throw 'Installer restart did not recover and install.' }
            $observed = Get-Content -LiteralPath (Join-Path $sandbox 'recovery-observed.json') -Raw | ConvertFrom-Json
            $observedProgram = @{}; foreach ($property in $observed.ProgramFiles.PSObject.Properties) { $observedProgram[$property.Name]=$property.Value }
            $observedLinks = @{}; foreach ($property in $observed.Links.PSObject.Properties) { $observedLinks[$property.Name]=$property.Value }
            if ($scenario -eq 'crash-committed') {
                if ($observed.ResolvedPhase -ne 'Committed' -or -not (Same-Hashes $crashedFiles $observedProgram) -or -not (Same-Hashes $crashedLinks $observedLinks)) { throw 'Committed installation was downgraded on restart.' }
            }
            else {
                if ($observed.ResolvedPhase -ne 'RolledBack' -or -not (Same-Hashes $before $observedProgram) -or -not (Same-Hashes $linksBefore $observedLinks) -or $observed.Startup -cne '"old-fixture-path.exe"') { throw 'Automatic recovery did not restore the previous state before preparing retry.' }
                $restoredKeys = $observed.Registration | ConvertFrom-Json
                if (-not $restoredKeys[0].Exists -or $restoredKeys[1].Exists) { throw 'Automatic recovery did not restore old registration.' }
            }
            $interruption = [ordered]@{ setupAndHelperSelfExitCode=73; durablePhaseAtInterruption=$crashedJournal.Phase; resolvedPhaseOnRestart=$observed.ResolvedPhase; retrySetupExitCode=$retry.ExitCode; previousStateVerifiedBeforeRetry=$true; crashedBuild=$crashedBuild }
            $expectedCode = 0
        }
        if ($server) { if (-not $server.WaitForExit(20000) -or $server.ExitCode -ne 0) { throw 'Exit fixture failed to end naturally.' }; Move-Item -LiteralPath (Join-Path $sandbox 'target.json') -Destination (Join-Path $sandbox 'target.finished.json') }
        $success = $expectedCode -eq 0
        $allFilesMatch = $true
        if ($success) {
            foreach ($name in $payloadFiles.Keys) { if ((Get-FileHash -LiteralPath (Join-Path $program $name)).Hash.ToLowerInvariant() -ne $payloadFiles[$name]) { $allFilesMatch=$false } }
            if (-not $allFilesMatch) { throw 'Installed complete payload differs from ZIP.' }
            if ((Link-Target (Join-Path $links 'desktop.lnk')) -ine (Join-Path $program 'SteamWatch.exe')) { throw 'Application shortcut target incorrect.' }
            if ((Link-Target (Join-Path $links 'start-menu.lnk')) -ine (Join-Path $program 'SteamWatch.exe')) { throw 'Start menu shortcut target incorrect.' }
            $guideName = [string]([char]0x64CD) + [char]0x4F5C + [char]0x6307 + [char]0x5357 + '.txt'
            if ((Link-Target (Join-Path $links 'guide.lnk')) -ine (Join-Path $program $guideName)) { throw 'Guide shortcut target incorrect.' }
            if ((Link-Target (Join-Path $links 'uninstall.lnk')) -ine (Join-Path $sandbox "installers/$buildId/unins000.exe")) { throw 'Uninstall shortcut target incorrect.' }
        }
        elseif (-not (Same-Hashes $before (Tree-Hashes $program)) -or -not (Same-Hashes $linksBefore (Tree-Hashes $links))) { throw 'Failed install changed old payload or shortcut bytes.' }
        $legacy = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($legacyPath)
        $next = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($newPath)
        $startup = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($startupPath)
        try {
            $legacyExists = $null -ne $legacy; $newExists = $null -ne $next
            $startupValue = if ($startup) { $startup.GetValue('SteamWatch') } else { $null }
            if ($success) {
                if ($legacyExists -or -not $newExists) { throw 'Uninstall identity migration failed.' }
                if ($scenario -eq 'fresh' -and $null -ne $startupValue) { throw 'Fresh install enabled startup.' }
                if ($scenario -ne 'fresh' -and $startupValue -cne ('"'+(Join-Path $program 'SteamWatch.exe')+'"')) { throw 'Enabled startup preference was not retained.' }
            }
            elseif (-not $legacyExists -or $newExists -or $startupValue -cne '"old-fixture-path.exe"') { throw 'Failure did not restore registration/startup snapshot.' }
        }
        finally { foreach ($key in @($legacy,$next,$startup)) { if ($key) { $key.Dispose() } } }
        $dataAfter = Tree-Hashes (Join-Path $sandbox 'data')
        foreach ($name in $dataBefore.Keys) { if ($dataBefore[$name] -cne $dataAfter[$name]) { throw 'Independent data was overwritten.' } }
        if ($scenario -notin @('fresh','exit-decline')) {
            if ($dataAfter['legacy-only.txt'] -cne $before['data/legacy-only.txt']) { throw 'Legacy-only data was not copied.' }
            if ($success) {
                $backups = @(Get-ChildItem -LiteralPath $sandbox -Directory -Filter 'SteamWatch.previous-*')
                $originalBackups = @($backups | Where-Object { Same-Hashes $before (Tree-Hashes $_.FullName) })
                if ($originalBackups.Count -ne 1) { throw 'Original installation and data were not retained in exactly one backup.' }
            }
        }
        $sequenceResult = $null
        $nativeResult = $null
        if ($scenario -eq 'native-launch') {
            Add-Type -AssemblyName UIAutomationClient
            Add-Type -AssemblyName UIAutomationTypes
            Add-Type -AssemblyName System.Drawing
            $native = Start-Process -FilePath (Join-Path $program 'SteamWatch.exe') -WindowStyle Hidden -PassThru
            $null = $native.Handle
            $condition = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ProcessIdProperty, $native.Id)
            $watch = [Diagnostics.Stopwatch]::StartNew()
            $window = $null
            $ready = $false
            $refreshName = [string]([char]0x5237) + [char]0x65B0
            while (-not $ready -or -not (Test-Path -LiteralPath (Join-Path $sandbox 'data/playtime-ledger.json'))) {
                if ($native.HasExited -or $watch.Elapsed.TotalSeconds -gt 45) { throw 'Installed sandbox WinUI payload did not initialize.' }
                $window = [Windows.Automation.AutomationElement]::RootElement.FindFirst([Windows.Automation.TreeScope]::Children, $condition)
                if ($window) {
                    $elements = $window.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)
                    for ($index=0; $index -lt $elements.Count; $index++) {
                        $state=$elements[$index].Current
                        if ($state.Name -eq $refreshName -and $state.ControlType.Id -eq 50000 -and $state.IsEnabled) { $ready=$true; break }
                    }
                }
                Start-Sleep -Milliseconds 100
            }
            if (Test-Path -LiteralPath (Join-Path $program 'data')) { throw 'Native payload used application-local data instead of the independent root.' }
            $rectangle = $window.Current.BoundingRectangle
            $bitmap = New-Object Drawing.Bitmap([int]$rectangle.Width, [int]$rectangle.Height)
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            try { $graphics.CopyFromScreen([int]$rectangle.X,[int]$rectangle.Y,0,0,$bitmap.Size); $bitmap.Save((Join-Path $sandbox 'native-window.png')) }
            finally { $graphics.Dispose(); $bitmap.Dispose() }
            $nativeResult = [ordered]@{ processId=$native.Id; executable=(Join-Path $program 'SteamWatch.exe'); windowTitle=$window.Current.Name; windowClass=$window.Current.ClassName; refreshEnabledAfterInitialization=$ready; independentLedgerCreated=$true; scope='Full WinUI payload in portable-isolated validation layout, not formal installed mode' }
        }
        if ($scenario -in @('repeat-upgrade','repeat-failure','lifecycle-busy','native-launch')) {
            $priorBuildId = $buildId
            $priorUninstaller = Join-Path $sandbox "installers/$priorBuildId/unins000.exe"
            $installedBefore = Tree-Hashes $program
            $installedLinksBefore = Tree-Hashes $links
            $buildId = [Guid]::NewGuid().ToString('N')
            $repeatConfiguration = Join-Path $sandbox 'repeat-config.json'
            $repeatFailure = if ($scenario -eq 'repeat-failure') { 'after-shortcut' } else { 'none' }
            [ordered]@{ ArtifactRoot=$artifactRoot; Sandbox=$sandbox; Identity=$identity; BuildId=$buildId; PayloadSha256=$payloadHash; FailureMode=$repeatFailure } |
                ConvertTo-Json | Set-Content -LiteralPath $repeatConfiguration -Encoding UTF8
            $holdMillis = if ($scenario -eq 'lifecycle-busy') { 12000 } else { 0 }
            & $Compiler /Qp "/DSandbox=$sandbox" "/DArtifactRoot=$artifactRoot" "/DIdentity=$identity" "/DBuildId=$buildId" "/DOutput=$sandbox" "/DPayload=$PayloadZip" "/DHelper=$helper" "/DConfiguration=$repeatConfiguration" "/DHoldMillis=$holdMillis" (Join-Path $repoRoot 'installer/validation/transactional.iss') *> (Join-Path $sandbox 'repeat-compile.log')
            if ($LASTEXITCODE -ne 0) { throw 'Repeat installer compilation failed.' }
            $repeatSetup = Join-Path $sandbox "transactional-$buildId.exe"
            $repeatProcess = Start-Process -FilePath $repeatSetup -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $sandbox 'repeat-setup.log')+'"')) -WindowStyle Hidden -PassThru
            $null = $repeatProcess.Handle
            $busySetupCode = $null; $busyUninstallCode = $null
            try {
                if ($scenario -eq 'lifecycle-busy') {
                    $watch = [Diagnostics.Stopwatch]::StartNew()
                    while (-not (Test-Path -LiteralPath (Join-Path $sandbox 'lifecycle-held.txt'))) {
                        if ($repeatProcess.HasExited -or $watch.Elapsed.TotalSeconds -gt 10) { throw 'Lifecycle holder did not initialize.' }
                        Start-Sleep -Milliseconds 100
                    }
                    $contender = Start-Process -FilePath $repeatSetup -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $sandbox 'busy-setup.log')+'"')) -WindowStyle Hidden -PassThru -Wait
                    $busySetupCode = $contender.ExitCode
                    $contender = Start-Process -FilePath $priorUninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $sandbox 'busy-uninstall.log')+'"')) -WindowStyle Hidden -PassThru -Wait
                    $busyUninstallCode = $contender.ExitCode
                    if ($busySetupCode -eq 0 -or $busyUninstallCode -eq 0 -or $repeatProcess.HasExited) { throw 'Lifecycle collision did not fail before mutation.' }
                    if (-not (Same-Hashes $installedBefore (Tree-Hashes $program)) -or -not (Same-Hashes $installedLinksBefore (Tree-Hashes $links))) { throw 'Lifecycle contender modified the active installation.' }
                    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($newPath)
                    try { if ($key.GetValue('UninstallString') -cne ('"'+$priorUninstaller+'"')) { throw 'Lifecycle contender changed registration ownership.' } } finally { $key.Dispose() }
                    if (-not (Same-Hashes $dataAfter (Tree-Hashes (Join-Path $sandbox 'data')))) { throw 'Lifecycle contender changed independent data.' }
                }
                if (-not $repeatProcess.WaitForExit(120000)) { throw 'Repeat install did not end naturally; no process killed.' }
                $repeatCode = $repeatProcess.ExitCode
            }
            finally { $repeatProcess.Dispose() }
            $expectedRepeatCode = if ($scenario -eq 'repeat-failure') { 20 } else { 0 }
            if ($repeatCode -ne $expectedRepeatCode) { throw "Repeat install exited $repeatCode, expected $expectedRepeatCode." }
            $newUninstaller = Join-Path $sandbox "installers/$buildId/unins000.exe"
            $inactive = if ($scenario -eq 'repeat-failure') { $newUninstaller } else { $priorUninstaller }
            $active = if ($scenario -eq 'repeat-failure') { $priorUninstaller } else { $newUninstaller }
            foreach ($name in $payloadFiles.Keys) { if ((Get-FileHash -LiteralPath (Join-Path $program $name)).Hash.ToLowerInvariant() -cne $payloadFiles[$name]) { throw 'Repeated installation differs from complete payload.' } }
            if ($scenario -eq 'repeat-failure' -and -not (Same-Hashes $installedBefore (Tree-Hashes $program))) { throw 'Repeat failure did not restore the previous transaction identity.' }
            if ($scenario -eq 'repeat-failure' -and -not (Same-Hashes $installedLinksBefore (Tree-Hashes $links))) { throw 'Repeat failure changed active shortcut bytes.' }
            $afterRepeat = Tree-Hashes $program
            $linksAfterRepeat = Tree-Hashes $links
            if ((Link-Target (Join-Path $links 'uninstall.lnk')) -ine $active) { throw 'Repeat uninstall shortcut points to an inactive build.' }
            $inactiveProcess = Start-Process -FilePath $inactive -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $sandbox 'inactive-uninstall.log')+'"')) -WindowStyle Hidden -PassThru -Wait
            if ($inactiveProcess.ExitCode -eq 0 -or -not (Same-Hashes $afterRepeat (Tree-Hashes $program)) -or -not (Same-Hashes $linksAfterRepeat (Tree-Hashes $links))) { throw 'Inactive repeat uninstaller modified current payload or shortcuts.' }
            $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($newPath)
            try { if ($key.GetValue('UninstallString') -cne ('"'+$active+'"')) { throw 'Repeat did not retain the correct active uninstall entry.' } } finally { $key.Dispose() }
            $sequenceResult = [ordered]@{ repeatSetupExitCode=$repeatCode; inactiveUninstallExitCode=$inactiveProcess.ExitCode; activeUninstaller=$active; busySetupExitCode=$busySetupCode; busyUninstallExitCode=$busyUninstallCode; payloadPreserved=$true; registrationOwnerVerified=$true }
            if ($scenario -eq 'repeat-failure') { $buildId = $priorBuildId }
            if ($native) {
                if (-not $native.WaitForExit(10000) -or $native.ExitCode -ne 0) { throw 'Actual WinUI process did not exit normally before upgrade.' }
                $nativeResult['normalExitBeforeUpgrade'] = $true
                $nativeResult['applicationExitCode'] = $native.ExitCode
                $dataAfter = Tree-Hashes (Join-Path $sandbox 'data')
                foreach ($name in $dataBefore.Keys) { if ($dataAfter[$name] -cne $dataBefore[$name]) { throw 'Native upgrade overwrote independent fixture data.' } }
            }
        }
        $uninstaller = Join-Path $sandbox "installers/$buildId/unins000.exe"
        $uninstallCode = $null
        if (Test-Path -LiteralPath $uninstaller) {
            $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $sandbox 'uninstall.log')+'"')) -WindowStyle Hidden -PassThru -Wait
            $uninstallCode = $uninstall.ExitCode
            if ($success) {
                if ($uninstallCode -ne 0 -or (Test-Path -LiteralPath $program) -or (Tree-Hashes $links).Count -ne 0) { throw 'Active uninstall did not remove program/shortcuts.' }
                if (-not (Same-Hashes $dataAfter (Tree-Hashes (Join-Path $sandbox 'data')))) { throw 'Uninstall modified retained data.' }
                $remainingRegistration = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($newPath)
                try { if ($remainingRegistration) { throw 'Uninstall left its active registration.' } } finally { if ($remainingRegistration) { $remainingRegistration.Dispose() } }
                $remainingStartup = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($startupPath)
                try { if ($remainingStartup -and $null -ne $remainingStartup.GetValue('SteamWatch')) { throw 'Uninstall left its mirrored startup value.' } } finally { if ($remainingStartup) { $remainingStartup.Dispose() } }
            }
            else {
                if ($uninstallCode -eq 0 -or -not (Same-Hashes $before (Tree-Hashes $program))) { throw 'Inactive uninstaller was allowed to affect old installation.' }
            }
        }
        $results.Add([ordered]@{ scenario=$scenario; setupExitCode=$setup.ExitCode; payloadFileCount=$payloadFiles.Count; payloadMatched=if($success){$allFilesMatch}else{$null}
            oldPayloadAndLinksRestored=if(-not $success){$true}else{$null}; legacyEntryPresentAfterSetup=$legacyExists; newEntryPresentAfterSetup=$newExists
            startupPreferencePreserved=$true; startupScope='Isolated HKCU Run mirror; real startup untouched'; independentDataPreserved=$true
            legacyDataCopiedAndSourceRetained=if($scenario -eq 'fresh'){$null}elseif($success){$true}else{$null}; shortcutTargetsVerified=if($success){4}else{0}
            uninstallExitCode=$uninstallCode; uninstallWasActive=$success; sequence=$sequenceResult; interruption=$interruption; native=$nativeResult; passed=$true })
    }
    finally {
        if ($payloadLock) { $payloadLock.Dispose() }
        if ($native) {
            if (-not $native.HasExited) { & (Join-Path $helper 'SteamWatch.InstallationProbe.exe') bundle $artifactRoot $sandbox exit $configuration; if (-not $native.WaitForExit(15000)) { throw 'Native app still running; no process killed.' } }
            $native.Dispose()
        }
        if ($server) { if (-not $server.WaitForExit(20000)) { throw 'Fixture exceeded lifetime; no process killed.' }; $server.Dispose() }
        foreach ($path in @($legacyPath,$newPath,$startupPath)) { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKey($path,$false) }
        [ordered]@{ observedAt=[DateTimeOffset]::Now.ToString('o'); payload=$PayloadZip; payloadSha256=$payloadHash; scenarios=$results.ToArray(); realStartupUnchanged=(Main-StartupFingerprint) -ceq $startupBefore
            scope='Full WinUI payload with isolated app/marker/data layout; native launch only when selected; real sandbox LNK/HKCU, mirrored startup, silent-only prototype uninstall; process self-exit is not reboot/power loss' } |
            ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $OutputRoot 'evidence.json') -Encoding UTF8
    }
}
if ((Main-StartupFingerprint) -cne $startupBefore) { throw 'Real SteamWatch startup entry changed.' }
Write-Output (Join-Path $OutputRoot 'evidence.json')
