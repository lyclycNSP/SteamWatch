param([string]$OutputRoot)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputRoot) { $OutputRoot = Join-Path $repoRoot 'artifacts/v2-inno-validation' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not $OutputRoot.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Toolchain must stay inside repository artifacts.'
}
$version = '7.1.0'
$name = "innosetup-$version-x64.exe"
$url = "https://github.com/jrsoftware/issrc/releases/download/is-7_1_0/$name"
$expectedHash = '0362a383ed217d4c4239b5933866dd96d3eb2102737da92f80f6057a4b40df2f'
$downloads = Join-Path $OutputRoot 'downloads'
$toolchain = Join-Path $OutputRoot "toolchain/inno-$version-x64"
New-Item -ItemType Directory -Force $downloads | Out-Null
$setup = Join-Path $downloads $name
if (-not (Test-Path -LiteralPath $setup)) { Invoke-WebRequest $url -OutFile $setup }
$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
if ($hash -ne $expectedHash) { throw 'Official setup SHA256 mismatch; nothing executed.' }
$signature = Get-AuthenticodeSignature -LiteralPath $setup
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'CN=Pyrsys B\.V\.,') {
    throw 'Official setup Authenticode verification failed; nothing executed.'
}
$compiler = Join-Path $toolchain 'ISCC.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $arguments = @('/PORTABLE=1', '/CURRENTUSER', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS',
        ('/DIR="' + $toolchain + '"'), ('/LOG="' + (Join-Path $OutputRoot 'toolchain-setup.log') + '"'))
    $process = Start-Process -FilePath $setup -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Portable toolchain extraction failed: $($process.ExitCode)" }
}
$compilerSignature = Get-AuthenticodeSignature -LiteralPath $compiler
if ($compilerSignature.Status -ne 'Valid' -or $compilerSignature.SignerCertificate.Subject -notmatch 'CN=Pyrsys B\.V\.,') { throw 'Compiler signature invalid.' }
$dependencies = @(Get-ChildItem -LiteralPath $toolchain -Filter '*.dll' | ForEach-Object {
    $dllSignature = Get-AuthenticodeSignature -LiteralPath $_.FullName
    if ($dllSignature.Status -ne 'Valid' -or $dllSignature.SignerCertificate.Subject -notmatch 'CN=Pyrsys B\.V\.,') {
        throw "Compiler dependency signature invalid: $($_.Name)"
    }
    [ordered]@{ name = $_.Name; sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant(); signatureStatus = [string]$dllSignature.Status }
})
foreach ($required in @('ISCmplr.dll', 'ISPP.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $toolchain $required))) { throw "Compiler dependency missing: $required" }
}
$reportedVersion = (& $compiler --version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $reportedVersion -notmatch ('(?m)^' + [regex]::Escape($version) + '$')) { throw "Unexpected compiler version: $reportedVersion" }
$license = Join-Path $toolchain 'license.txt'
if (-not (Test-Path -LiteralPath $license)) { throw 'Toolchain license missing.' }
[ordered]@{
    checkedAt = [DateTimeOffset]::Now.ToString('o'); version = $version; architecture = 'x64'
    source = $url; sha256 = $hash; signatureStatus = [string]$signature.Status
    signer = $signature.SignerCertificate.Subject; signerThumbprint = $signature.SignerCertificate.Thumbprint
    compiler = $compiler; reportedVersion = $reportedVersion; compilerSha256 = (Get-FileHash -LiteralPath $compiler).Hash.ToLowerInvariant()
    compilerSignatureStatus = [string]$compilerSignature.Status; dependencies = $dependencies
    license = $license; licenseSha256 = (Get-FileHash -LiteralPath $license).Hash.ToLowerInvariant()
    mode = 'Official /PORTABLE=1 /CURRENTUSER; no global installation or PATH changes'
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputRoot 'toolchain.json') -Encoding UTF8
Write-Output $compiler
