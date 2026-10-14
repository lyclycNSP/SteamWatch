param([Parameter(Mandatory=$true)][string]$Directory)
$ErrorActionPreference='Stop'
$Directory=[IO.Path]::GetFullPath($Directory)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$manifestPath=Join-Path $Directory 'release-manifest.json'
$expected=(Get-Content -LiteralPath "$manifestPath.sha256" -Raw).Trim().Split(' ')[0]
if((Get-FileHash -LiteralPath $manifestPath).Hash -ine $expected){throw 'Delivery manifest checksum mismatch.'}
$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if($manifest.schemaVersion -ne 1 -or $manifest.signingMode -notin @('Unsigned','Signed') -or $manifest.releaseReady -ne $false){throw 'Unsupported or prematurely approved delivery manifest.'}
$expectedFiles=@('SteamWatch-win-x64.zip','SteamWatchSetup-win-x64.exe','SteamWatch-win-x64.manifest.json')
if($manifest.files.Count -ne $expectedFiles.Count){throw 'Unexpected delivery file set.'}
foreach($name in $expectedFiles){
    $record=@($manifest.files | Where-Object name -ceq $name)
    if($record.Count -ne 1){throw 'Duplicate/missing delivery file.'}
    $path=Join-Path $Directory $name
    if((Get-FileHash -LiteralPath $path).Hash -ine $record[0].sha256 -or (Get-Item -LiteralPath $path).Length -ne $record[0].bytes){throw "Delivery file mismatch: $name"}
    if(-not $name.EndsWith('.json')){
        $checksum=(Get-Content -LiteralPath "$path.sha256" -Raw).Trim()
        if($checksum -ine ($record[0].sha256+'  '+$name)){throw "Checksum file mismatch: $name"}
    }
}
$package=Get-Content -LiteralPath (Join-Path $Directory 'SteamWatch-win-x64.manifest.json') -Raw | ConvertFrom-Json
if($package.version -cne $manifest.version -or $package.fileVersion -cne $manifest.fileVersion -or $package.signingMode -cne $manifest.signingMode){throw 'Package/release identity mismatch.'}
$expectedEntries=@{}
$owned=@('SteamWatch.exe','app/SteamWatch.exe','app/SteamWatch.dll','app/SteamWatch.Core.dll','app/SteamWatch.Infrastructure.dll')
foreach($item in $package.files){
    if($item.path -match '(^|/)(data|logs|exports)(/|$)|RuntimeProbe|InstallationProbe|\.(pfx|p12|key|pem)$' -or $item.path -match '(^|/)\.\.?(/|$)|[:\\]' -or $item.path.StartsWith('/')){throw 'Unsafe/forbidden package inventory entry.'}
    if($expectedEntries.ContainsKey($item.path)){throw 'Duplicate package inventory entry.'}
    $expectedEntries[$item.path]=$item
}
foreach($name in $owned){if(-not $expectedEntries.ContainsKey($name)){throw 'Owned binary missing from package inventory.'}}
$signatureRoot=$null
if($manifest.signingMode -eq 'Signed'){
    $signatureRoot=Join-Path ([IO.Path]::GetTempPath()) ('SteamWatch-Verify-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory $signatureRoot | Out-Null
}
$zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $Directory 'SteamWatch-win-x64.zip'))
$seen=@{}
try{
    foreach($entry in $zip.Entries){
        if($entry.FullName.EndsWith('/')){continue}
        $prefix='SteamWatch-win-x64/'
        if(-not $entry.FullName.StartsWith($prefix)){throw 'Unexpected portable ZIP root.'}
        $name=$entry.FullName.Substring($prefix.Length)
        if(-not $expectedEntries.ContainsKey($name) -or $seen.ContainsKey($name)){throw 'Unlisted or duplicate ZIP file.'}
        $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
        try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant()}finally{$stream.Dispose();$sha.Dispose()}
        if($hash -cne $expectedEntries[$name].sha256 -or $entry.Length -ne $expectedEntries[$name].bytes){throw "ZIP inventory mismatch: $name"}
        if($signatureRoot -and $name -in $owned){
            $target=Join-Path $signatureRoot ([Guid]::NewGuid().ToString('N')+[IO.Path]::GetExtension($name))
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$target)
            $signature=Get-AuthenticodeSignature -LiteralPath $target
            if($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate -or
                $signature.SignerCertificate.Thumbprint -ne $expectedEntries[$name].signerThumbprint){throw "ZIP binary signature/timestamp verification failed: $name"}
        }
        $seen[$name]=$true
    }
    if($seen.Count -ne $expectedEntries.Count){throw 'ZIP is missing manifest files.'}
}finally{$zip.Dispose()}
$setup=Join-Path $Directory 'SteamWatchSetup-win-x64.exe'
$version=[Diagnostics.FileVersionInfo]::GetVersionInfo($setup)
if($version.FileVersion.Trim() -ne $manifest.fileVersion -or $version.ProductVersion.Trim() -cne $manifest.version){throw 'Installer version mismatch.'}
$signature=Get-AuthenticodeSignature -LiteralPath $setup
if($manifest.signingMode -eq 'Signed' -and ($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate)){throw 'Installer signature/timestamp verification failed.'}
if($manifest.signingMode -eq 'Unsigned' -and $signature.Status -ne 'NotSigned'){throw 'Unexpected installer signature status.'}
Write-Host "Verified $($manifest.version): $($seen.Count) portable files; $($manifest.signingMode); preview, acceptance pending."
