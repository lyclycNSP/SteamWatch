param(
    [ValidateSet('x64','x86','ARM64')][string]$Platform = 'x64',
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Native launcher requires Visual Studio C++ Build Tools (vswhere missing).' }
$installation = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if ($LASTEXITCODE -ne 0 -or -not $installation) { throw 'Native launcher requires Visual Studio C++ Build Tools and Windows SDK.' }
$developerCommand = Join-Path $installation 'Common7\Tools\VsDevCmd.bat'
$source = Join-Path $repoRoot 'src\SteamWatch.Launcher\launcher.c'
$icon = Join-Path $repoRoot 'src\SteamWatch.App\Assets\AppIcon.ico'
[xml]$versionProperties = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
$version = [string]$versionProperties.Project.PropertyGroup.FileVersion
$productVersion = [string]$versionProperties.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Launcher FileVersion must contain four numeric components.' }
$resource = Join-Path $OutputDirectory 'launcher.rc'
$resourceBody = @"
#include <windows.h>
1 ICON "$($icon.Replace('\','\\'))"
1 VERSIONINFO
FILEVERSION $($version.Replace('.',','))
PRODUCTVERSION $($version.Replace('.',','))
FILETYPE VFT_APP
BEGIN
 BLOCK "StringFileInfo"
 BEGIN
  BLOCK "040904b0"
  BEGIN
   VALUE "FileDescription", "SteamWatch Portable Launcher\0"
   VALUE "FileVersion", "$version\0"
   VALUE "ProductName", "SteamWatch\0"
   VALUE "ProductVersion", "$productVersion\0"
  END
 END
 BLOCK "VarFileInfo"
 BEGIN
  VALUE "Translation", 0x409, 1200
 END
END
"@
Set-Content -LiteralPath $resource -Value $resourceBody -Encoding Unicode
# Keep paths out of a generated command string: cmd expands quoted environment variables.
# Quotes and line breaks are rejected because they would break those argument boundaries.
foreach ($path in @($OutputDirectory,$developerCommand,$source)) {
    if ($path -match '["\r\n%]') { throw 'Launcher build paths cannot contain quotes, percent signs or line breaks.' }
}
$env:STEAMWATCH_LAUNCHER_DEV = $developerCommand
$env:STEAMWATCH_LAUNCHER_SOURCE = $source
$buildScript = Join-Path $OutputDirectory 'build.cmd'
@"
@echo off
call "%STEAMWATCH_LAUNCHER_DEV%" -no_logo -arch=$Platform -host_arch=x64
if errorlevel 1 exit /b 1
rc /nologo /fo launcher.res launcher.rc
if errorlevel 1 exit /b 1
cl /nologo /W4 /WX /O2 /MT /utf-8 "%STEAMWATCH_LAUNCHER_SOURCE%" launcher.res /Fe:SteamWatch.exe /link /SUBSYSTEM:WINDOWS /DYNAMICBASE /NXCOMPAT user32.lib
if errorlevel 1 exit /b 1
dumpbin /nologo /dependents SteamWatch.exe > dependencies.txt
exit /b %errorlevel%
"@ | Set-Content -LiteralPath $buildScript -Encoding ASCII
Push-Location -LiteralPath $OutputDirectory
try {
    & $env:ComSpec /d /c build.cmd
    if ($LASTEXITCODE -ne 0) { throw "Native launcher build failed: $LASTEXITCODE" }
} finally {
    Pop-Location
    Remove-Item Env:STEAMWATCH_LAUNCHER_DEV,Env:STEAMWATCH_LAUNCHER_SOURCE
}
