# Shared by build scripts and the Inno signing callback. No PFX/password input.
function Enter-ReleaseBuild([string]$Root) {
    $directory=Join-Path $Root 'artifacts'
    New-Item -ItemType Directory -Force $directory | Out-Null
    $path=Join-Path $directory 'release-build.lock'
    if((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Release lock cannot be a reparse point.'}
    return [IO.File]::Open($path,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
}

function Get-ReleaseVersion {
    [xml]$properties = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../Directory.Build.props') -Raw
    $version = [string]$properties.Project.PropertyGroup.Version
    $fileVersion = [string]$properties.Project.PropertyGroup.FileVersion
    if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$' -or $fileVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Invalid centralized release version.' }
    return [pscustomobject]@{ Version=$version; FileVersion=$fileVersion }
}

function Get-ReleaseSigning([ValidateSet('Unsigned','Signed')][string]$Mode) {
    if ($Mode -eq 'Unsigned') { return [pscustomobject]@{ Mode=$Mode; Certificate=$null; Tool=$null; Timestamp=$null; Machine=$false } }
    $thumbprint = $env:STEAMWATCH_SIGN_CERT_SHA1
    if ($thumbprint -notmatch '^[0-9A-Fa-f]{40}$') { throw 'Signed mode requires STEAMWATCH_SIGN_CERT_SHA1 (40 hex characters). No unsigned fallback.' }
    $timestamp = $null
    if (-not [Uri]::TryCreate($env:STEAMWATCH_TIMESTAMP_URL,[UriKind]::Absolute,[ref]$timestamp) -or
        $timestamp.Scheme -notin @('http','https') -or $timestamp.UserInfo -or $timestamp.Query -or $timestamp.Fragment) {
        throw 'Signed mode requires an HTTP(S) RFC3161 STEAMWATCH_TIMESTAMP_URL without credentials/query/fragment.'
    }
    $location = $env:STEAMWATCH_SIGN_STORE_LOCATION
    if (-not $location) { $location='CurrentUser' }
    if ($location -notin @('CurrentUser','LocalMachine')) { throw 'Signing store must be CurrentUser or LocalMachine.' }
    $certificate = Get-ChildItem -Path "Cert:\$location\My" -CodeSigningCert | Where-Object Thumbprint -eq $thumbprint | Select-Object -First 1
    if (-not $certificate -or -not $certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date)) {
        throw 'Requested code-signing certificate/private key is unavailable or expired. No unsigned fallback.'
    }
    $tool = $env:STEAMWATCH_SIGNTOOL_PATH
    if (-not $tool) {
        $sdk = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
        $tool = Get-ChildItem -LiteralPath $sdk -Directory | Sort-Object Name -Descending | ForEach-Object {
            $candidate=Join-Path $_.FullName 'x64/signtool.exe'
            if (Test-Path -LiteralPath $candidate) { $candidate }
        } | Select-Object -First 1
    }
    if (-not $tool -or -not (Test-Path -LiteralPath $tool -PathType Leaf)) { throw 'Signed mode requires Windows SDK signtool.exe.' }
    return [pscustomobject]@{ Mode=$Mode; Certificate=$certificate.Thumbprint; Tool=[IO.Path]::GetFullPath($tool); Timestamp=$timestamp.AbsoluteUri; Machine=($location -eq 'LocalMachine') }
}

function Sign-ReleaseFile([string]$Path,$Signing) {
    if ($Signing.Mode -eq 'Unsigned') { return }
    $arguments=@('sign','/sha1',$Signing.Certificate,'/s','My','/fd','SHA256','/tr',$Signing.Timestamp,'/td','SHA256','/d','SteamWatch')
    if ($Signing.Machine) { $arguments += '/sm' }
    $arguments += [IO.Path]::GetFullPath($Path)
    & $Signing.Tool @arguments | Write-Host
    if ($LASTEXITCODE -ne 0) { throw "Authenticode signing failed: $LASTEXITCODE. No unsigned fallback." }
    & $Signing.Tool verify /pa /all /tw $Path | Write-Host
    if ($LASTEXITCODE -ne 0) { throw 'SignTool signature/timestamp verification failed.' }
    $signature=Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $Signing.Certificate -or -not $signature.TimeStamperCertificate) {
        throw 'Expected trusted signer and timestamp were not verified.'
    }
}

function Publish-ReleaseFiles([string]$Source,[string]$Destination,[string[]]$Names) {
    # All files have been built and verified before any public artifact changes.
    # Keep old bytes and compensate partial commits; never delete previous artifacts.
    New-Item -ItemType Directory -Force $Destination | Out-Null
    $id=[Guid]::NewGuid().ToString('N')
    $changed=[Collections.Generic.List[object]]::new()
    foreach ($name in $Names) {
        if ([IO.Path]::GetFileName($name) -cne $name -or -not (Test-Path -LiteralPath (Join-Path $Source $name) -PathType Leaf)) { throw 'Invalid or missing staged release file.' }
        $target=Join-Path $Destination $name
        if ((Test-Path -LiteralPath $target) -and ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Release destination cannot be a reparse point.' }
    }
    try {
        foreach ($name in $Names) {
            $target=Join-Path $Destination $name
            $backup="$target.previous-$id"
            $exists=Test-Path -LiteralPath $target
            if ($exists) { [IO.File]::Replace((Join-Path $Source $name),$target,$backup) }
            else { [IO.File]::Move((Join-Path $Source $name),$target) }
            $changed.Add([pscustomobject]@{Name=$name;Existed=$exists;Backup=$backup})
        }
    } catch {
        for ($index=$changed.Count-1;$index -ge 0;$index--) {
            $item=$changed[$index]; $target=Join-Path $Destination $item.Name
            if ($item.Existed) { [IO.File]::Replace($item.Backup,$target,"$target.failed-$id") }
            else { [IO.File]::Move($target,(Join-Path $Source $item.Name)) }
        }
        throw
    }
}
