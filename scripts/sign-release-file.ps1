param([Parameter(Mandatory=$true)][string]$Path)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'release-support.ps1')
Sign-ReleaseFile $Path (Get-ReleaseSigning 'Signed')
