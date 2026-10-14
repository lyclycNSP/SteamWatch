param([string]$Compiler, [string]$OutputRoot)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
if (-not $Compiler) { $Compiler = & (Join-Path $PSScriptRoot 'prepare-inno-toolchain.ps1') }
if (-not $OutputRoot) { $OutputRoot = Join-Path $artifactRoot ('v2-inno-validation/exit-' + [Guid]::NewGuid().ToString('N')) }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (-not $OutputRoot.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must stay inside repository artifacts.' }
if (Test-Path -LiteralPath $OutputRoot) { throw 'Use a new output directory.' }
New-Item -ItemType Directory $OutputRoot | Out-Null
$publish = Join-Path $OutputRoot 'probe'
dotnet publish (Join-Path $repoRoot 'tools/SteamWatch.InstallationProbe/SteamWatch.InstallationProbe.csproj') -c Release -r win-x64 --self-contained false -o $publish *> (Join-Path $OutputRoot 'build.log')
if ($LASTEXITCODE -ne 0) { throw 'Probe build failed; see retained log.' }
$probe = Join-Path $publish 'SteamWatch.InstallationProbe.exe'
$results = [Collections.Generic.List[object]]::new()
foreach ($mode in @('accept', 'decline', 'fail', 'timeout', 'prepared-stays')) {
    $sandbox = Join-Path $OutputRoot $mode
    $payload = Join-Path $sandbox 'payload'
    $program = Join-Path $sandbox 'program'
    New-Item -ItemType Directory -Force $payload, $program | Out-Null
    Set-Content -LiteralPath (Join-Path $payload 'application.txt') -Value 'new' -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $program 'application.txt') -Value 'old' -Encoding ASCII
    $identity = [Guid]::NewGuid().ToString('N')
    $registryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SteamWatch-Validation-${identity}_is1"
    & $Compiler /Qp "/DSandbox=$sandbox" "/DPayload=$payload" "/DOutput=$sandbox" "/DIdentity=$identity" "/DCoordinator=$probe" "/DArtifactRoot=$artifactRoot" (Join-Path $repoRoot 'installer/validation/baseline.iss') *> (Join-Path $sandbox 'compile.log')
    if ($LASTEXITCODE -ne 0) { throw 'Exit fixture compilation failed.' }
    $server = Start-Process -FilePath $probe -ArgumentList @('serve', ('"' + $artifactRoot + '"'), ('"' + $sandbox + '"'), $mode) -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $sandbox 'fixture-error.log')
    # Retain the native process handle before exit (Windows PowerShell 5.1).
    $null = $server.Handle
    try {
        $deadline = [Diagnostics.Stopwatch]::StartNew()
        while (-not (Test-Path -LiteralPath (Join-Path $sandbox 'target.json'))) {
            if ($server.HasExited -or $deadline.Elapsed.TotalSeconds -gt 10) { throw 'Fixture did not become ready.' }
            Start-Sleep -Milliseconds 100
        }
        $elapsed = [Diagnostics.Stopwatch]::StartNew()
        $setup = Start-Process -FilePath (Join-Path $sandbox 'baseline-1.exe') -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG="' + (Join-Path $sandbox 'setup.log') + '"')) -WindowStyle Hidden -PassThru -Wait
        $elapsed.Stop()
        $content = (Get-Content -LiteralPath (Join-Path $program 'application.txt') -Raw).Trim()
        $actualExitBeforeSetupReturned = $server.HasExited
        $registered = Test-Path -LiteralPath $registryPath
        $passed = if ($mode -eq 'accept') { $setup.ExitCode -eq 0 -and $content -eq 'new' -and $actualExitBeforeSetupReturned -and $registered }
            else { $setup.ExitCode -eq 7 -and $content -eq 'old' -and -not $registered -and -not $actualExitBeforeSetupReturned }
        $results.Add([ordered]@{ mode = $mode; setupExitCode = $setup.ExitCode; payload = $content
            actualExitBeforeSetupReturned = $actualExitBeforeSetupReturned; registered = $registered
            elapsedSeconds = $elapsed.Elapsed.TotalSeconds; passed = $passed })
    }
    finally {
        # Fixture lifetime is bounded; allow natural exit even for rejection/timeout cases.
        if (-not $server.WaitForExit(20000)) { throw 'Fixture lifetime exceeded; no process was killed.' }
        $fixtureExitCode = $server.ExitCode
        $uninstaller = Join-Path $program 'unins000.exe'
        if (Test-Path -LiteralPath $uninstaller) {
            $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -WindowStyle Hidden -PassThru -Wait
            if ($uninstall.ExitCode -ne 0 -or (Test-Path -LiteralPath $registryPath)) { throw 'Isolated registration cleanup failed.' }
        }
        $results.ToArray() | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputRoot 'evidence.json') -Encoding UTF8
        $server.Dispose()
        if ($null -eq $fixtureExitCode -or $fixtureExitCode -ne 0) { throw 'Controlled fixture failed or exit code unavailable; inspect fixture-error.log.' }
    }
}
if (@($results | Where-Object { -not $_.passed }).Count -gt 0) { throw 'Exit integration failed; inspect evidence.' }
Write-Output (Join-Path $OutputRoot 'evidence.json')
