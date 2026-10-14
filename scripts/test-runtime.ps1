param([string]$OutputRoot, [ValidateSet('timing','active-exit-recovery','residency')][string]$Mode = 'timing', [ValidateRange(60,3600)][int]$DurationSeconds = 300)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputRoot) { $OutputRoot = Join-Path $repoRoot ('artifacts\runtime-' + [guid]::NewGuid().ToString('N')) }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) { throw 'Probe output must be a new directory.' }
$publish = Join-Path $OutputRoot 'publish'
dotnet publish (Join-Path $repoRoot 'tools\SteamWatch.RuntimeProbe\SteamWatch.RuntimeProbe.csproj') `
    -c Release -r win-x64 --self-contained false -o $publish
if ($LASTEXITCODE -ne 0) { throw "Probe build failed: $LASTEXITCODE" }
$fixtureRoot = Join-Path $OutputRoot 'fixture'
Copy-Item -LiteralPath $publish -Destination $fixtureRoot -Recurse
$probe = Join-Path $publish 'SteamWatch.RuntimeProbe.exe'
Copy-Item -LiteralPath $probe -Destination (Join-Path $fixtureRoot 'steam.exe')
$games = Join-Path $fixtureRoot 'steamapps\common'
New-Item -ItemType Directory -Path $games -Force | Out-Null
$game = Join-Path $games 'Fixture'
Copy-Item -LiteralPath $publish -Destination $game -Recurse
Copy-Item -LiteralPath $probe -Destination (Join-Path $game 'game.exe')
if($Mode -eq 'residency') { & $probe $Mode $fixtureRoot (Join-Path $OutputRoot 'evidence') $DurationSeconds }
else { & $probe $Mode $fixtureRoot (Join-Path $OutputRoot 'evidence') }
if ($LASTEXITCODE -ne 0) { throw "Runtime probe failed: $LASTEXITCODE" }
Write-Output "Evidence: $OutputRoot"
