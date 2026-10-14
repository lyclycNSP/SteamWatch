param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [ValidateSet('x64', 'x86', 'ARM64')][string]$Platform = 'x64',
    [string]$OutputRoot,
    [ValidateSet('Unsigned','Signed')][string]$SigningMode = 'Unsigned'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'release-support.ps1')
$signing = Get-ReleaseSigning $SigningMode
$releaseVersion = Get-ReleaseVersion
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputRoot) { $OutputRoot = $repoRoot }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$buildLock=Enter-ReleaseBuild $OutputRoot
try {
$runtime = @{ x64='win-x64'; x86='win-x86'; ARM64='win-arm64' }[$Platform]
$publishRoot = Join-Path $OutputRoot 'artifacts\publish'
$distDir = Join-Path $OutputRoot 'dist'
$transactionId = [Guid]::NewGuid().ToString('N')
$stagingRoot = Join-Path $publishRoot "staging-$transactionId"
$stagingDirectory = Join-Path $stagingRoot "SteamWatch-$runtime"
$rawDirectory = Join-Path $stagingRoot "raw\SteamWatch-$runtime"
$launcherDirectory = Join-Path $stagingRoot 'launcher'
$payloadDirectory = Join-Path $OutputRoot 'artifacts\installer-payload'
$payloadZip = Join-Path $payloadDirectory "SteamWatch-$runtime.zip"
$stagingPayloadZip = Join-Path $stagingRoot 'installer-payload.zip'
$publishDirectory = Join-Path $publishRoot "SteamWatch-$runtime"
$previousDirectory = Join-Path $publishRoot "SteamWatch-$runtime.previous-$transactionId"
$stagingZip = Join-Path $stagingRoot "SteamWatch-$runtime.zip"
$zipPath = Join-Path $distDir "SteamWatch-$runtime.zip"
New-Item -ItemType Directory -Force -Path $stagingDirectory,$rawDirectory,$distDir,$payloadDirectory | Out-Null
$guide = Join-Path $repoRoot 'docs\manual-zh.txt'
if (-not (Test-Path -LiteralPath $guide)) { throw "Missing guide: $guide" }
dotnet publish (Join-Path $repoRoot 'src\SteamWatch.App\SteamWatch.App.csproj') `
    -c $Configuration "-p:Platform=$Platform" -r $runtime --self-contained true `
    -p:WindowsAppSDKSelfContained=true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $rawDirectory
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE. Previous packages were preserved." }
if (-not (Test-Path -LiteralPath (Join-Path $rawDirectory 'SteamWatch.exe'))) { throw 'Published entry point missing.' }
$guideName = [string]::Concat([char]0x64CD,[char]0x4F5C,[char]0x6307,[char]0x5357,'.txt')
Copy-Item -LiteralPath $guide -Destination (Join-Path $rawDirectory $guideName)
& (Join-Path $PSScriptRoot 'copy-package-licenses.ps1') -ApplicationDirectory $rawDirectory
foreach ($name in @('SteamWatch.exe','SteamWatch.dll','SteamWatch.Core.dll','SteamWatch.Infrastructure.dll')) {
    Sign-ReleaseFile (Join-Path $rawDirectory $name) $signing
}
# The installer consumes the flat payload, never the portable launcher layout.
Compress-Archive -LiteralPath $rawDirectory -DestinationPath $stagingPayloadZip
Move-Item -LiteralPath $rawDirectory -Destination (Join-Path $stagingDirectory 'app')
Move-Item -LiteralPath (Join-Path $stagingDirectory "app\$guideName") -Destination (Join-Path $stagingDirectory $guideName)
& (Join-Path $PSScriptRoot 'build-launcher.ps1') -Platform $Platform -OutputDirectory $launcherDirectory
Copy-Item -LiteralPath (Join-Path $launcherDirectory 'SteamWatch.exe') -Destination (Join-Path $stagingDirectory 'SteamWatch.exe')
Sign-ReleaseFile (Join-Path $stagingDirectory 'SteamWatch.exe') $signing
Set-Content -LiteralPath (Join-Path $stagingDirectory 'portable.marker') -Value 'Portable mode: launch SteamWatch.exe; dependencies are in app; data is in data.' -Encoding UTF8
Compress-Archive -LiteralPath $stagingDirectory -DestinationPath $stagingZip
$portableManifestName = "SteamWatch-$runtime.manifest.json"
& (Join-Path $PSScriptRoot 'write-package-manifest.ps1') -PackageDirectory $stagingDirectory -OutputPath (Join-Path $stagingRoot $portableManifestName) -SigningMode $SigningMode -Platform $Platform
$hash = (Get-FileHash -LiteralPath $stagingZip -Algorithm SHA256).Hash
Set-Content -LiteralPath "$stagingZip.sha256" -Value "$hash  SteamWatch-$runtime.zip" -Encoding ASCII
$hadPrevious = Test-Path -LiteralPath $publishDirectory
$hadPayload = Test-Path -LiteralPath $payloadZip
$payloadBackup = "$payloadZip.previous-$transactionId"
$payloadCommitted = $false
if ($hadPrevious) { Move-Item -LiteralPath $publishDirectory -Destination $previousDirectory }
try {
    Move-Item -LiteralPath $stagingDirectory -Destination $publishDirectory
    if ($hadPayload) { [IO.File]::Replace($stagingPayloadZip,$payloadZip,$payloadBackup) }
    else { Move-Item -LiteralPath $stagingPayloadZip -Destination $payloadZip }
    $payloadCommitted = $true
    Publish-ReleaseFiles $stagingRoot $distDir @("SteamWatch-$runtime.zip","SteamWatch-$runtime.zip.sha256",$portableManifestName)
} catch {
    if ($payloadCommitted) {
        if ($hadPayload) { [IO.File]::Replace($payloadBackup,$payloadZip,"$payloadZip.failed-$transactionId") }
        else { Move-Item -LiteralPath $payloadZip -Destination $stagingPayloadZip }
    }
    if (Test-Path -LiteralPath $publishDirectory) { Move-Item -LiteralPath $publishDirectory -Destination $stagingDirectory }
    if ($hadPrevious) { Move-Item -LiteralPath $previousDirectory -Destination $publishDirectory }
    throw
}
Write-Host "Published: $publishDirectory"
Write-Host "Archive: $zipPath"
} finally { $buildLock.Dispose() }
