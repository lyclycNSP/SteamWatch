param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("x64")]
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$installerProjectPath = Join-Path $repoRoot "src\SteamWatch.Installer\SteamWatch.Installer.csproj"
$installerPublishDir = Join-Path $repoRoot "artifacts\installer\win-x64"
$distDir = Join-Path $repoRoot "dist"
$setupPath = Join-Path $distDir "SteamWatchSetup-win-x64.exe"

& (Join-Path $PSScriptRoot "publish-portable.ps1") -Configuration $Configuration -Platform $Platform

if (Test-Path $installerPublishDir) {
    Remove-Item -LiteralPath $installerPublishDir -Recurse -Force
}

dotnet publish $installerProjectPath `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=false `
    -p:EnableCompressionInSingleFile=true `
    -p:RequireInstallerPayload=true `
    -o $installerPublishDir

Copy-Item -LiteralPath (Join-Path $installerPublishDir "SteamWatchSetup.exe") -Destination $setupPath -Force

Write-Host "Installer: $setupPath"
