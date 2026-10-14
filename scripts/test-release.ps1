param([Parameter(Mandatory=$true)][string]$DeliveryDirectory,[Parameter(Mandatory=$true)][string]$OutputRoot)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'release-support.ps1')
$repo=Split-Path -Parent $PSScriptRoot
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
$allowed=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts'))+'\'
if(-not $OutputRoot.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $OutputRoot)){throw 'Use a new repository artifact directory.'}
New-Item -ItemType Directory $OutputRoot | Out-Null
$DeliveryDirectory=(Resolve-Path -LiteralPath $DeliveryDirectory).Path
$results=[Collections.Generic.List[object]]::new()
function Check([string]$Name,[scriptblock]$Action){& $Action; $results.Add(@{scenario=$Name;passed=$true})}
function Reject([scriptblock]$Action,[string]$ExpectedMessage='*'){
    $failed=$false
    try{& $Action}catch{if($_.Exception.Message -notlike $ExpectedMessage){throw};$failed=$true}
    if(-not $failed){throw 'Expected failure did not occur.'}
}
Check 'valid-delivery' { & (Join-Path $PSScriptRoot 'verify-delivery.ps1') -Directory $DeliveryDirectory }
Check 'missing-signing-identity' {
    $old=$env:STEAMWATCH_SIGN_CERT_SHA1
    try{$env:STEAMWATCH_SIGN_CERT_SHA1='';Reject {Get-ReleaseSigning 'Signed' | Out-Null} '*requires STEAMWATCH_SIGN_CERT_SHA1*'}finally{$env:STEAMWATCH_SIGN_CERT_SHA1=$old}
}
Check 'shared-output-build-lock' {
    $scope=Join-Path $OutputRoot 'concurrent'
    $lock=Enter-ReleaseBuild $scope
    try{Reject {$second=Enter-ReleaseBuild $scope; $second.Dispose()}}
    finally{$lock.Dispose()}
    $retry=Enter-ReleaseBuild $scope;$retry.Dispose()
}
Check 'missing-certificate-no-fallback' {
    $old=$env:STEAMWATCH_SIGN_CERT_SHA1;$url=$env:STEAMWATCH_TIMESTAMP_URL
    try{$env:STEAMWATCH_SIGN_CERT_SHA1='0'*40;$env:STEAMWATCH_TIMESTAMP_URL='https://timestamp.invalid';Reject {Get-ReleaseSigning 'Signed' | Out-Null} 'Requested code-signing certificate/private key*'}
    finally{$env:STEAMWATCH_SIGN_CERT_SHA1=$old;$env:STEAMWATCH_TIMESTAMP_URL=$url}
}
Check 'timestamp-credentials-rejected' {
    $old=$env:STEAMWATCH_SIGN_CERT_SHA1;$url=$env:STEAMWATCH_TIMESTAMP_URL
    try{$env:STEAMWATCH_SIGN_CERT_SHA1='0'*40;$env:STEAMWATCH_TIMESTAMP_URL='https://user:pass@timestamp.invalid';Reject {Get-ReleaseSigning 'Signed' | Out-Null} '*without credentials/query/fragment*'}
    finally{$env:STEAMWATCH_SIGN_CERT_SHA1=$old;$env:STEAMWATCH_TIMESTAMP_URL=$url}
}
foreach($fresh in @($false,$true)){
    Check ("partial-file-commit-rollback-fresh-$fresh") {
        $scope=Join-Path $OutputRoot ("rollback-$fresh");$source=Join-Path $scope 'source';$target=Join-Path $scope 'target'
        New-Item -ItemType Directory -Force $source,$target | Out-Null
        if(-not $fresh){Set-Content (Join-Path $target 'a.txt') 'old-a' -Encoding ASCII}
        Set-Content (Join-Path $target 'b.txt') 'old-b' -Encoding ASCII
        Set-Content (Join-Path $target 'user.txt') 'unrelated-preserved' -Encoding ASCII
        Set-Content (Join-Path $source 'a.txt') 'new-a' -Encoding ASCII
        Set-Content (Join-Path $source 'b.txt') 'new-b' -Encoding ASCII
        $before=@{};Get-ChildItem $target -File | ForEach-Object {$before[$_.Name]=(Get-FileHash $_.FullName).Hash}
        $lock=[IO.File]::Open((Join-Path $target 'b.txt'),'Open','Read','None')
        try{Reject {Publish-ReleaseFiles $source $target @('a.txt','b.txt')}}finally{$lock.Dispose()}
        foreach($name in $before.Keys){if((Get-FileHash (Join-Path $target $name)).Hash -cne $before[$name]){throw 'Previous artifact bytes were not restored.'}}
        if($fresh -and (Test-Path (Join-Path $target 'a.txt'))){throw 'Failed fresh commit exposed partial artifact.'}
    }
}
Check 'missing-staged-file-before-commit' {
    $source=Join-Path $OutputRoot 'missing/source';$target=Join-Path $OutputRoot 'missing/target'
    New-Item -ItemType Directory -Force $source,$target | Out-Null
    Set-Content (Join-Path $source 'a.txt') 'new' -Encoding ASCII
    Set-Content (Join-Path $target 'a.txt') 'old' -Encoding ASCII
    Reject {Publish-ReleaseFiles $source $target @('a.txt','missing.txt')}
    if((Get-Content (Join-Path $target 'a.txt') -Raw).Trim() -cne 'old'){throw 'Preflight failure exposed a new artifact.'}
}
$tamper=Join-Path $OutputRoot 'tamper'
New-Item -ItemType Directory $tamper | Out-Null
Get-ChildItem -LiteralPath $DeliveryDirectory -File | Where-Object {$_.Name -notmatch '\.(previous|failed)-'} | Copy-Item -Destination $tamper
Check 'manifest-checksum-rejection' {
    Add-Content (Join-Path $tamper 'release-manifest.json') ' ' -Encoding UTF8
    Reject {& (Join-Path $PSScriptRoot 'verify-delivery.ps1') -Directory $tamper} 'Delivery manifest checksum mismatch.'
    Copy-Item (Join-Path $DeliveryDirectory 'release-manifest.json') (Join-Path $tamper 'release-manifest.json') -Force
}
Check 'installer-tamper-rejection' {
    $file=[IO.File]::Open((Join-Path $tamper 'SteamWatchSetup-win-x64.exe'),'Open','Write')
    try{$file.Position=128;$file.WriteByte(0)}finally{$file.Dispose()}
    # Appending guarantees a mutation even if the original byte was already zero.
    $file=[IO.File]::Open((Join-Path $tamper 'SteamWatchSetup-win-x64.exe'),'Append','Write')
    try{$file.WriteByte(1)}finally{$file.Dispose()}
    Reject {& (Join-Path $PSScriptRoot 'verify-delivery.ps1') -Directory $tamper} 'Delivery file mismatch: SteamWatchSetup-win-x64.exe'
    Copy-Item (Join-Path $DeliveryDirectory 'SteamWatchSetup-win-x64.exe') (Join-Path $tamper 'SteamWatchSetup-win-x64.exe') -Force
}
Check 'package-version-mismatch-rejection' {
    $path=Join-Path $tamper 'SteamWatch-win-x64.manifest.json'
    $package=Get-Content $path -Raw | ConvertFrom-Json;$package.version='9.9.9'
    $package | ConvertTo-Json -Depth 8 | Set-Content $path -Encoding UTF8
    $path=Join-Path $tamper 'release-manifest.json'
    $manifest=Get-Content $path -Raw | ConvertFrom-Json
    $record=$manifest.files | Where-Object name -eq 'SteamWatch-win-x64.manifest.json'
    $packagePath=Join-Path $tamper $record.name
    $record.sha256=(Get-FileHash $packagePath).Hash.ToLowerInvariant();$record.bytes=(Get-Item $packagePath).Length
    $manifest | ConvertTo-Json -Depth 10 | Set-Content $path -Encoding UTF8
    Set-Content "$path.sha256" (((Get-FileHash $path).Hash.ToLowerInvariant())+'  release-manifest.json') -Encoding ASCII
    Reject {& (Join-Path $PSScriptRoot 'verify-delivery.ps1') -Directory $tamper} 'Package/release identity mismatch.'
}
$inspection=Join-Path $OutputRoot 'producer-input'
[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $DeliveryDirectory 'SteamWatch-win-x64.zip'),$inspection)
$packageRoot=Join-Path $inspection 'SteamWatch-win-x64'
$manifestOutput=Join-Path $OutputRoot 'rejected-package.json'
Check 'probe-contamination-rejected' {
    $probe=Join-Path $packageRoot 'RuntimeProbe.exe'
    Set-Content $probe 'synthetic marker, never executed' -Encoding ASCII
    Reject {& (Join-Path $PSScriptRoot 'write-package-manifest.ps1') -PackageDirectory $packageRoot -OutputPath $manifestOutput} 'Runtime data, probe or credential material in package:*'
    Move-Item -LiteralPath $probe -Destination (Join-Path $OutputRoot 'retained-RuntimeProbe.fixture')
}
Check 'runtime-data-contamination-rejected' {
    $data=Join-Path $packageRoot 'app/data'
    New-Item -ItemType Directory $data | Out-Null
    $settings=Join-Path $data 'settings.json'
    Set-Content $settings '{"fixture":true}' -Encoding ASCII
    Reject {& (Join-Path $PSScriptRoot 'write-package-manifest.ps1') -PackageDirectory $packageRoot -OutputPath $manifestOutput} 'Runtime data, probe or credential material in package:*'
    Move-Item -LiteralPath $settings -Destination (Join-Path $OutputRoot 'retained-settings.fixture')
}
Check 'missing-package-license-rejected' {
    $license=Join-Path $packageRoot 'app/licenses/SteamWatch-LICENSE.txt'
    $retained=Join-Path $OutputRoot 'retained-SteamWatch-LICENSE.txt'
    Move-Item -LiteralPath $license -Destination $retained
    try{Reject {& (Join-Path $PSScriptRoot 'write-package-manifest.ps1') -PackageDirectory $packageRoot -OutputPath $manifestOutput} 'Package license information missing: SteamWatch-LICENSE.txt'}
    finally{Move-Item -LiteralPath $retained -Destination $license}
}
Check 'missing-self-contained-runtime-rejected' {
    $runtime=Join-Path $packageRoot 'app/coreclr.dll'
    $retained=Join-Path $OutputRoot 'retained-coreclr.dll'
    Move-Item -LiteralPath $runtime -Destination $retained
    try{Reject {& (Join-Path $PSScriptRoot 'write-package-manifest.ps1') -PackageDirectory $packageRoot -OutputPath $manifestOutput} 'Self-contained dependency missing: coreclr.dll'}
    finally{Move-Item -LiteralPath $retained -Destination $runtime}
}
@{observedAt=[DateTimeOffset]::Now.ToString('o');scenarios=$results.ToArray();scope='Actual unsigned delivery verification, real file sharing violation/partial commit compensation, signing preflight; no certificate signing or timestamp service used'} |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputRoot 'evidence.json') -Encoding UTF8
