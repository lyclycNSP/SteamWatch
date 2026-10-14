param([Parameter(Mandatory=$true)][string]$PackageDirectory,[Parameter(Mandatory=$true)][string]$OutputPath,
    [ValidateSet('Unsigned','Signed')][string]$SigningMode='Unsigned',[string]$Platform='x64')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'release-support.ps1')
$release=Get-ReleaseVersion
$PackageDirectory=[IO.Path]::GetFullPath($PackageDirectory)
$app=Join-Path $PackageDirectory 'app'
foreach($notice in @('SteamWatch-LICENSE.txt','THIRD-PARTY-NOTICES.md','license-index.json')){
    if(-not(Test-Path -LiteralPath (Join-Path $app "licenses/$notice") -PathType Leaf)){throw "Package license information missing: $notice"}
}
foreach($required in @('coreclr.dll','hostfxr.dll','hostpolicy.dll','System.Private.CoreLib.dll','Microsoft.UI.Xaml.dll','Microsoft.WindowsAppRuntime.dll','SteamWatch.exe')) {
    if(-not (Test-Path -LiteralPath (Join-Path $app $required) -PathType Leaf)){throw "Self-contained dependency missing: $required"}
}
$runtime=Get-Content -LiteralPath (Join-Path $app 'SteamWatch.runtimeconfig.json') -Raw | ConvertFrom-Json
if($runtime.runtimeOptions.framework -or $runtime.runtimeOptions.frameworks -or
    -not @($runtime.runtimeOptions.includedFrameworks | Where-Object name -eq 'Microsoft.NETCore.App').Count){throw 'Application runtimeconfig is not self-contained.'}
if(-not (Test-Path -LiteralPath (Join-Path $PackageDirectory 'portable.marker'))){throw 'Portable marker missing.'}
$owned=@('SteamWatch.exe','app/SteamWatch.exe','app/SteamWatch.dll','app/SteamWatch.Core.dll','app/SteamWatch.Infrastructure.dll')
$inventory=@(Get-ChildItem -LiteralPath $PackageDirectory -Recurse -File | Sort-Object FullName | ForEach-Object {
    $relative=$_.FullName.Substring($PackageDirectory.Length+1).Replace('\','/')
    if($relative -match '(^|/)(data|logs|exports)(/|$)|RuntimeProbe|InstallationProbe|UiAcceptanceHost|ui-acceptance\.host|(^|/)(settings|playtime[^/]*|limit-rules)\.json$|\.(pfx|p12|key|pem|user|suo)$|(^|/)\.env(?:\.|$)') {
        throw "Runtime data, probe or credential material in package: $relative"
    }
    $item=[ordered]@{path=$relative;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}
    if($relative -in $owned){
        $version=[Diagnostics.FileVersionInfo]::GetVersionInfo($_.FullName).FileVersion
        if($version -ne $release.FileVersion){throw "File version mismatch: $relative ($version)"}
        $signature=Get-AuthenticodeSignature -LiteralPath $_.FullName
        if($SigningMode -eq 'Signed' -and ($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate)){throw "Unsigned/untimestamped owned file: $relative"}
        if($SigningMode -eq 'Unsigned' -and $signature.Status -ne 'NotSigned'){throw "Unexpected signature in unsigned package: $relative"}
        $item.fileVersion=$version
        $item.signature=[string]$signature.Status
        $item.timestampVerified=($null -ne $signature.TimeStamperCertificate)
        $item.signerThumbprint=if($signature.SignerCertificate){$signature.SignerCertificate.Thumbprint}else{$null}
    }
    $item
})
foreach($name in $owned){if(-not @($inventory | Where-Object path -eq $name).Count){throw "Owned binary missing: $name"}}
$productVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $app 'SteamWatch.dll')).ProductVersion.Split('+')[0]
if($productVersion -cne $release.Version){throw 'Managed product version differs from centralized version.'}
[ordered]@{schemaVersion=1;version=$release.Version;fileVersion=$release.FileVersion;platform=$Platform;signingMode=$SigningMode
    releaseReady=$false;selfContained=@{dotnet=$true;windowsAppSdk=$true;frameworks=$runtime.runtimeOptions.includedFrameworks;verification='Runtimeconfig and required packaged files; clean-system execution remains separate'}
    files=$inventory} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
