param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [ValidateSet('x64')][string]$Platform = 'x64',
    [string]$OutputRoot,
    [string]$Compiler,
    [ValidateSet('Unsigned','Signed')][string]$SigningMode = 'Unsigned'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'release-support.ps1')
$signing=Get-ReleaseSigning $SigningMode
$release=Get-ReleaseVersion
$repoRoot=Split-Path -Parent $PSScriptRoot
if(-not $OutputRoot){$OutputRoot=$repoRoot}
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
$buildLock=Enter-ReleaseBuild $OutputRoot
try {
$id=[Guid]::NewGuid().ToString('N')
# Publish everything privately. A compiler/signing/validation failure must not
# expose a new ZIP next to an old installer or stale checksums.
$work=Join-Path $OutputRoot "artifacts/installer/build-$id"
New-Item -ItemType Directory -Force $work | Out-Null
& (Join-Path $PSScriptRoot 'publish-portable.ps1') -Configuration $Configuration -Platform $Platform -OutputRoot $work -SigningMode $SigningMode
$dist=Join-Path $work 'dist'
$payload=Join-Path $work 'artifacts/installer-payload/SteamWatch-win-x64.zip'
$helperDirectory=Join-Path $work 'helper'
if(-not $Compiler){$Compiler=& (Join-Path $PSScriptRoot 'prepare-inno-toolchain.ps1')}
dotnet publish (Join-Path $repoRoot 'src/SteamWatch.Installer/SteamWatch.Installer.csproj') `
    -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true `
    -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None -p:DebugSymbols=false -o $helperDirectory
if($LASTEXITCODE -ne 0){throw 'Installer helper build failed. Previous delivery preserved.'}
$helper=Join-Path $helperDirectory 'SteamWatch.InstallationHelper.exe'
Sign-ReleaseFile $helper $signing
$payloadHash=(Get-FileHash -LiteralPath $payload).Hash.ToLowerInvariant()
$arguments=@('/Qp',"/DAppVersion=$($release.Version)","/DFileVersion=$($release.FileVersion)","/DBuildId=$id","/DOutput=$dist",
    "/DPayload=$payload","/DPayloadHash=$payloadHash","/DHelper=$helper",
    "/DIcon=$(Join-Path $repoRoot 'src/SteamWatch.App/Assets/AppIcon.ico')","/DGuide=$(Join-Path $repoRoot 'docs/manual-zh.txt')")
if($SigningMode -eq 'Signed'){
    $callback=Join-Path $PSScriptRoot 'sign-release-file.ps1'
    $hostExe=(Get-Command pwsh -ErrorAction Stop).Source
    if(($callback+$hostExe) -match '["%&|<>^!\r\n]'){throw 'Signing callback path contains unsupported command characters.'}
    $signedUninstaller=Join-Path $work 'signed-uninstaller'
    $arguments+=@('/DReleaseSigning=1',"/DSignedUninstallerOutput=$signedUninstaller",
        ('/SSteamWatchRelease=$q'+$hostExe+'$q -NoProfile -ExecutionPolicy Bypass -File $q'+$callback+'$q -Path $f'))
}
& $Compiler @arguments (Join-Path $repoRoot 'installer/SteamWatch.iss')
if($LASTEXITCODE -ne 0){throw 'Inno compilation/signing failed. Previous delivery preserved.'}
$setup=Join-Path $dist 'SteamWatchSetup-win-x64.exe'
if(-not (Test-Path -LiteralPath $setup)){throw 'Installer entry point missing.'}
$signature=Get-AuthenticodeSignature -LiteralPath $setup
if($SigningMode -eq 'Signed' -and ($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate)){throw 'Signed installer verification failed.'}
if($SigningMode -eq 'Unsigned' -and $signature.Status -ne 'NotSigned'){throw 'Unexpected installer signature.'}
$setupVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($setup)
if($setupVersion.FileVersion.Trim() -ne $release.FileVersion -or $setupVersion.ProductVersion.Trim() -ne $release.Version){throw 'Installer version mismatch.'}
& (Join-Path $PSScriptRoot 'write-delivery-manifest.ps1') -BuildRoot $work -SigningMode $SigningMode -Configuration $Configuration
& (Join-Path $PSScriptRoot 'verify-delivery.ps1') -Directory $dist
$names=@('SteamWatch-win-x64.zip','SteamWatch-win-x64.zip.sha256','SteamWatch-win-x64.manifest.json',
    'SteamWatchSetup-win-x64.exe','SteamWatchSetup-win-x64.exe.sha256','release-manifest.json','release-manifest.json.sha256')
Publish-ReleaseFiles $dist (Join-Path $OutputRoot 'dist') $names
Write-Host "Delivery: $(Join-Path $OutputRoot 'dist') ($($release.Version), $SigningMode)"
} finally { $buildLock.Dispose() }
