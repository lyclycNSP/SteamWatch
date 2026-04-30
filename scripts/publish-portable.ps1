param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("x64", "x86", "ARM64")]
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\SteamWatch.App\SteamWatch.App.csproj"
$guideTemplatePath = Join-Path $repoRoot "docs\manual-zh.txt"
$runtime = switch ($Platform) {
    "x64" { "win-x64" }
    "x86" { "win-x86" }
    "ARM64" { "win-arm64" }
}

$publishRoot = Join-Path $repoRoot "artifacts\publish"
$publishDir = Join-Path $publishRoot "SteamWatch-$runtime"
$distDir = Join-Path $repoRoot "dist"
$zipPath = Join-Path $distDir "SteamWatch-$runtime.zip"
$guideName = [string]::Concat([char]0x64CD, [char]0x4F5C, [char]0x6307, [char]0x5357, ".txt")
$guidePath = Join-Path $publishRoot $guideName
$guideInPublishDirPath = Join-Path $publishDir $guideName

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path $guidePath) {
    Remove-Item -LiteralPath $guidePath -Force
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

if (-not (Test-Path $guideTemplatePath)) {
    throw "Guide template not found: $guideTemplatePath"
}

dotnet publish $projectPath `
    -c $Configuration `
    -p:Platform=$Platform `
    -r $runtime `
    --self-contained true `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -o $publishDir

Copy-Item -LiteralPath $guideTemplatePath -Destination $guidePath -Force
Copy-Item -LiteralPath $guideTemplatePath -Destination $guideInPublishDirPath -Force

Compress-Archive -Path $publishDir, $guidePath -DestinationPath $zipPath -Force

Write-Host "Published: $publishDir"
Write-Host "Guide:     $guidePath"
Write-Host "App guide: $guideInPublishDirPath"
Write-Host "Archive:   $zipPath"
