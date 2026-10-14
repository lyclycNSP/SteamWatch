param([Parameter(Mandatory=$true)][string]$ApplicationDirectory)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$ApplicationDirectory=[IO.Path]::GetFullPath($ApplicationDirectory)
$destination=Join-Path $ApplicationDirectory 'licenses'
if(Test-Path -LiteralPath $destination){throw 'License destination must be new.'}
$deps=Get-Content -LiteralPath (Join-Path $ApplicationDirectory 'SteamWatch.deps.json') -Raw | ConvertFrom-Json
$packageRoot=if($env:NUGET_PACKAGES){[IO.Path]::GetFullPath($env:NUGET_PACKAGES)}else{
    Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'
}
New-Item -ItemType Directory -Path $destination | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $destination 'SteamWatch-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $repoRoot 'THIRD-PARTY-NOTICES.md') -Destination $destination
$packages=@($deps.libraries.PSObject.Properties | Where-Object {$_.Value.type -in @('package','runtimepack')} | Sort-Object Name)
$entries=@(foreach($package in $packages){
    $identity=$package.Name -replace '^runtimepack\.',''
    if($identity -notmatch '^([A-Za-z0-9_.-]+)/([A-Za-z0-9_.+-]+)$'){throw "Invalid package identity: $identity"}
    $id=$Matches[1]; $version=$Matches[2]
    if($id -in @('.','..') -or $version -in @('.','..')){throw "Invalid package path: $identity"}
    $relative="$($id.ToLowerInvariant())/$($version.ToLowerInvariant())"
    $source=Join-Path $packageRoot $relative
    if(-not([IO.Path]::GetFullPath($source).StartsWith([IO.Path]::GetFullPath($packageRoot).TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase))){throw 'Package license source escaped the NuGet cache.'}
    if(-not(Test-Path -LiteralPath $source -PathType Container)){throw "Restored package not found for license collection: $identity"}
    $output=Join-Path $destination "nuget/$relative"
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $files=@(Get-ChildItem -LiteralPath $source -File | Where-Object {
        $_.Name -match '(?i)^(LICENSE|NOTICE|THIRD[-_.]?PARTY[-_.]?NOTICES?)(\.[A-Za-z]+)?$'
    } | Sort-Object Name | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $output
        @{file="nuget/$relative/$($_.Name)";sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}
    })
    $nuspec=Get-ChildItem -LiteralPath $source -Filter '*.nuspec' -File | Select-Object -First 1
    $license=$null; $licenseUrl=$null; $copyright=$null
    if($nuspec){
        [xml]$xml=Get-Content -LiteralPath $nuspec.FullName -Raw
        $licenseNode=$xml.SelectSingleNode('//*[local-name()="metadata"]/*[local-name()="license"]')
        $urlNode=$xml.SelectSingleNode('//*[local-name()="metadata"]/*[local-name()="licenseUrl"]')
        $copyrightNode=$xml.SelectSingleNode('//*[local-name()="metadata"]/*[local-name()="copyright"]')
        if($licenseNode){$license=@{type=$licenseNode.GetAttribute('type');value=$licenseNode.InnerText}}
        if($urlNode){$licenseUrl=$urlNode.InnerText}
        if($copyrightNode){$copyright=$copyrightNode.InnerText}
    }
    if($files.Count -eq 0 -and -not $license -and -not $licenseUrl){throw "No license information for: $identity"}
    @{package=$id;version=$version;license=$license;licenseUrl=$licenseUrl;copyright=$copyright;files=$files}
})
@{schemaVersion=1;projectLicense='MIT';packages=$entries} | ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath (Join-Path $destination 'license-index.json') -Encoding UTF8
Write-Output "Collected license information for $($entries.Count) restored dependencies."
