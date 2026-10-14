param([string]$Compiler, [string]$OutputRoot)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $Compiler) { $Compiler = & (Join-Path $PSScriptRoot 'prepare-inno-toolchain.ps1') }
if (-not $OutputRoot) { $OutputRoot = Join-Path $repoRoot ('artifacts/v2-inno-validation/baseline-' + [Guid]::NewGuid().ToString('N')) }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not $OutputRoot.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must stay inside repository artifacts.' }
if (Test-Path -LiteralPath $OutputRoot) { throw 'Use a new output directory; existing evidence is preserved.' }
New-Item -ItemType Directory $OutputRoot | Out-Null
$identity = [Guid]::NewGuid().ToString('N')
$registryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SteamWatch-Validation-${identity}_is1"
$program = Join-Path $OutputRoot 'program'
$data = Join-Path $OutputRoot 'data'
New-Item -ItemType Directory $data | Out-Null
Set-Content -LiteralPath (Join-Path $data 'ledger.txt') -Value 'preserve-user-data' -Encoding ASCII
$dataHash = (Get-FileHash -LiteralPath (Join-Path $data 'ledger.txt')).Hash
foreach ($revision in 1..3) {
    $payload = Join-Path $OutputRoot "payload-$revision"
    New-Item -ItemType Directory $payload | Out-Null
    Set-Content -LiteralPath (Join-Path $payload 'application.txt') -Value "revision-$revision" -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $payload 'dependency.txt') -Value "dependency-$revision" -Encoding ASCII
    $arguments = @('/Qp', "/DSandbox=$OutputRoot", "/DPayload=$payload", "/DOutput=$OutputRoot", "/DIdentity=$identity", "/DRevision=$revision")
    if ($revision -eq 3) { $arguments += '/DFailPostCopyCallback' }
    & $Compiler @arguments (Join-Path $repoRoot 'installer/validation/baseline.iss') *> (Join-Path $OutputRoot "compile-$revision.log")
    if ($LASTEXITCODE -ne 0) { throw "Compilation $revision failed; see retained log." }
}
function Run-Setup([int]$Revision, [string]$Label, [string[]]$Extra = @()) {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG="' + (Join-Path $OutputRoot "$Label.log") + '"')) + $Extra
    $process = Start-Process -FilePath (Join-Path $OutputRoot "baseline-$Revision.exe") -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    return $process.ExitCode
}
function Snapshot([string]$Label, [int]$ExitCode) {
    $files = @{}
    if (Test-Path -LiteralPath $program) {
        Get-ChildItem -LiteralPath $program -File -Recurse | ForEach-Object { $files[$_.FullName.Substring($program.Length + 1)] = (Get-FileHash -LiteralPath $_.FullName).Hash }
    }
    [ordered]@{ label = $Label; exitCode = $ExitCode; files = $files
        application = if (Test-Path -LiteralPath (Join-Path $program 'application.txt')) { (Get-Content -LiteralPath (Join-Path $program 'application.txt') -Raw).Trim() } else { $null }
        uninstallEntry = Test-Path -LiteralPath $registryPath
        displayVersion = if (Test-Path -LiteralPath $registryPath) { (Get-ItemProperty -LiteralPath $registryPath).DisplayVersion } else { $null }
        dataPreserved = (Get-FileHash -LiteralPath (Join-Path $data 'ledger.txt')).Hash -eq $dataHash }
}
$observations = [Collections.Generic.List[object]]::new()
try {
    $code = Run-Setup 1 'fresh'
    $observations.Add((Snapshot 'fresh' $code))
    if ($code -ne 0 -or $observations[0].application -ne 'revision-1') { throw 'Fresh installation failed.' }
    $code = Run-Setup 2 'upgrade'
    $observations.Add((Snapshot 'upgrade' $code))
    if ($code -ne 0 -or $observations[1].application -ne 'revision-2') { throw 'Upgrade failed.' }
    $code = Run-Setup 3 'post-copy-callback-failure'
    $observations.Add((Snapshot 'post-copy-callback-failure' $code))
    # Restore a known baseline normally, without deleting the failure evidence.
    $code = Run-Setup 2 'restore-baseline'
    if ($code -ne 0) { throw 'Baseline restoration failed.' }
    $locked = [IO.File]::Open((Join-Path $program 'application.txt'), 'Open', 'Read', 'Read')
    try { $code = Run-Setup 1 'file-in-use' }
    finally { $locked.Dispose() }
    $observations.Add((Snapshot 'file-in-use' $code))
    # application.txt sorts before dependency.txt: fail only after a successful replacement.
    $code = Run-Setup 2 'restore-before-partial-copy'
    if ($code -ne 0) { throw 'Baseline restoration failed.' }
    $locked = [IO.File]::Open((Join-Path $program 'dependency.txt'), 'Open', 'Read', 'Read')
    try { $code = Run-Setup 1 'second-file-in-use' }
    finally { $locked.Dispose() }
    $observations.Add((Snapshot 'second-file-in-use' $code))
    $outside = Join-Path $OutputRoot 'refused-target'
    $code = Run-Setup 1 'directory-guard' @('/DIR="' + $outside + '"')
    $observations.Add([ordered]@{ label = 'directory-guard'; exitCode = $code; refused = -not (Test-Path -LiteralPath $outside) })
}
finally {
    $uninstaller = Join-Path $program 'unins000.exe'
    if (Test-Path -LiteralPath $uninstaller) {
        $process = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG="' + (Join-Path $OutputRoot 'uninstall.log') + '"')) -WindowStyle Hidden -PassThru -Wait
        $observations.Add((Snapshot 'uninstall' $process.ExitCode))
    }
    [ordered]@{ observedAt = [DateTimeOffset]::Now.ToString('o'); identity = $identity; registryPath = $registryPath
        sandbox = $OutputRoot; compiler = $Compiler; fixture = 'Two text payload files and an independent synthetic ledger; no SteamWatch application launched'
        observations = @($observations.ToArray()); registryClean = -not (Test-Path -LiteralPath $registryPath)
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputRoot 'evidence.json') -Encoding UTF8
}
if ($observations.Count -ne 7) { throw 'Incomplete experiment; inspect retained evidence.' }
foreach ($item in $observations) {
    if ($item.Contains('dataPreserved') -and -not $item.dataPreserved) { throw 'User-data preservation failed.' }
}
if ($observations[5].exitCode -eq 0 -or -not $observations[5].refused) { throw 'Sandbox guard failed.' }
if ($observations[6].exitCode -ne 0 -or $observations[6].uninstallEntry -or $observations[6].files.Count -ne 0) { throw 'Isolated uninstall cleanup failed.' }
Write-Output (Join-Path $OutputRoot 'evidence.json')
