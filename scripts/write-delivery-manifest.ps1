param([Parameter(Mandatory=$true)][string]$BuildRoot,[ValidateSet('Unsigned','Signed')][string]$SigningMode,
    [ValidateSet('Debug','Release')][string]$Configuration)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'release-support.ps1')
$release=Get-ReleaseVersion
$dist=Join-Path $BuildRoot 'dist'
$portable=Get-Content -LiteralPath (Join-Path $dist 'SteamWatch-win-x64.manifest.json') -Raw | ConvertFrom-Json
$setup=Join-Path $dist 'SteamWatchSetup-win-x64.exe'
$helper=Join-Path $BuildRoot 'helper/SteamWatch.InstallationHelper.exe'
if([Diagnostics.FileVersionInfo]::GetVersionInfo($helper).FileVersion.Trim() -ne $release.FileVersion){throw 'Installation helper version mismatch.'}
$binaries=@($setup,$helper)
if($SigningMode -eq 'Signed'){
    $uninstallers=@(Get-ChildItem -LiteralPath (Join-Path $BuildRoot 'signed-uninstaller') -Filter '*.exe' -File)
    if(-not $uninstallers.Count){throw 'Signed uninstaller evidence missing.'}
    $binaries+=@($uninstallers.FullName)
}
$binaryRecords=@(foreach($file in $binaries){
    $signature=Get-AuthenticodeSignature -LiteralPath $file
    if($SigningMode -eq 'Signed' -and ($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate)){throw 'Owned delivery binary signature/timestamp missing.'}
    if($SigningMode -eq 'Unsigned' -and $signature.Status -ne 'NotSigned'){throw 'Unsigned delivery binary unexpectedly signed.'}
    @{name=[IO.Path]::GetFileName($file);sha256=(Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant();signature=[string]$signature.Status;timestampVerified=($null -ne $signature.TimeStamperCertificate)}
})
$payload=Join-Path $BuildRoot 'artifacts/installer-payload/SteamWatch-win-x64.zip'
$sourceRevision=(& git -C (Split-Path -Parent $PSScriptRoot) rev-parse HEAD | Out-String).Trim()
$dirty=-not [string]::IsNullOrWhiteSpace((& git -C (Split-Path -Parent $PSScriptRoot) status --porcelain | Out-String))
$files=@(foreach($name in @('SteamWatch-win-x64.zip','SteamWatchSetup-win-x64.exe','SteamWatch-win-x64.manifest.json')){
    $path=Join-Path $dist $name
    $hash=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()
    if($name.EndsWith('.exe')){Set-Content -LiteralPath "$path.sha256" -Value "$hash  $name" -Encoding ASCII}
    @{name=$name;bytes=(Get-Item -LiteralPath $path).Length;sha256=$hash}
})
[ordered]@{schemaVersion=1;version=$release.Version;fileVersion=$release.FileVersion;configuration=$Configuration;platform='win-x64'
    channel='preview';releaseReady=$false;signingMode=$SigningMode;builtAt=[DateTimeOffset]::Now.ToString('o');source=@{revision=$sourceRevision;dirty=$dirty}
    files=$files;binaries=$binaryRecords;installerPayloadSha256=(Get-FileHash -LiteralPath $payload).Hash.ToLowerInvariant()
    selfContained=$portable.selfContained;helperSelfContained=@{dotnetPublish=$true;singleFile=$true;nativeLibrariesEmbedded=$true;verification='Explicit self-contained publish options; clean-system execution remains separate'}
    uninstaller=@{signed=($SigningMode -eq 'Signed');verification=if($SigningMode -eq 'Signed'){'Inno signed-uninstaller cache verified; deployed instance still requires acceptance'}else{'Explicit SignedUninstaller=no; verify deployed instance during installation acceptance'}}
    pending=@('UI and accessibility acceptance','real game/sleep/overnight acceptance','clean Windows installation/upgrade/uninstall','remote CI','release approval')
    restrictions=if($SigningMode -eq 'Unsigned'){@('Unsigned preview; not a signed public release','No SmartScreen guarantee')}else{@('Signing does not replace acceptance or guarantee SmartScreen reputation')}} |
    ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $dist 'release-manifest.json') -Encoding UTF8
$manifest=Join-Path $dist 'release-manifest.json'
Set-Content -LiteralPath "$manifest.sha256" -Value (((Get-FileHash -LiteralPath $manifest).Hash.ToLowerInvariant())+'  release-manifest.json') -Encoding ASCII
