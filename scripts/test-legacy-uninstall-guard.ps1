param([string]$OutputRoot)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
if (-not $OutputRoot) { $OutputRoot = Join-Path $artifactRoot ('legacy-uninstall-guard-' + [Guid]::NewGuid().ToString('N')) }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (-not $OutputRoot.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $OutputRoot)) { throw 'Use a new directory below repository artifacts.' }
New-Item -ItemType Directory $OutputRoot | Out-Null
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$archive = Join-Path $OutputRoot ('SteamWatch.previous-' + [Guid]::NewGuid().ToString('N'))
$profile = Join-Path $OutputRoot 'profile'
$current = Join-Path $profile 'Programs/SteamWatch'
$data = Join-Path $profile 'SteamWatch'
New-Item -ItemType Directory $archive, $current, $data | Out-Null
$script = Join-Path $archive 'Uninstall.ps1'
Copy-Item -LiteralPath (Join-Path $repoRoot 'src/SteamWatch.Installer/Uninstall.ps1') -Destination $script
Set-Content -LiteralPath (Join-Path $current 'SteamWatch.exe') -Value 'current synthetic application; never launched' -Encoding ASCII
Set-Content -LiteralPath (Join-Path $data 'records.txt') -Value 'independent data must survive' -Encoding ASCII
$hashes = @{}
foreach ($file in @(Get-ChildItem -LiteralPath $OutputRoot -File -Recurse)) { $hashes[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName).Hash }
$start = New-Object Diagnostics.ProcessStartInfo
$start.FileName = Join-Path $PSHOME 'powershell.exe'
$start.Arguments = '-NoProfile -File "' + $script + '"'
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$start.RedirectStandardError = $true
$start.RedirectStandardOutput = $true
# Only this child receives the test profile. The source guard rejects the archive
# before migration, any confirmation, registry writes or shell-folder access.
$start.EnvironmentVariables['LOCALAPPDATA'] = $profile
if ([IO.Path]::GetFullPath($archive) -ieq [IO.Path]::GetFullPath($current)) { throw 'Archive must differ from the expected active directory.' }
$child = [Diagnostics.Process]::Start($start)
$dialog = $null
try {
    $condition = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ProcessIdProperty, $child.Id)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while (-not $dialog) {
        if ($child.HasExited -or $watch.Elapsed.TotalSeconds -gt 20) { throw 'Archived script did not present its expected directory-guard error.' }
        $dialog = [Windows.Automation.AutomationElement]::RootElement.FindFirst([Windows.Automation.TreeScope]::Children, $condition)
        if (-not $dialog) { Start-Sleep -Milliseconds 100 }
    }
    do {
        $elements = $dialog.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)
        $texts = @(for ($index=0; $index -lt $elements.Count; $index++) { $elements[$index].Current.Name })
        if ($texts -contains 'Uninstall directory validation failed.') { break }
        Start-Sleep -Milliseconds 100
    } while (-not $child.HasExited -and $watch.Elapsed.TotalSeconds -lt 20)
    $controls = @(for ($index=0; $index -lt $elements.Count; $index++) { $item=$elements[$index].Current; [ordered]@{ name=$item.Name; type=$item.ControlType.ProgrammaticName; automationId=$item.AutomationId } })
    [ordered]@{ dialog=$dialog.Current.Name; texts=$texts; controls=$controls } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputRoot 'observed-dialog.json') -Encoding UTF8
    if (-not ($texts -contains 'Uninstall directory validation failed.')) { throw 'Archived script displayed an unexpected message.' }
    # This Windows PowerShell host exposes native dialog controls as Pane and
    # provides no button Invoke pattern. Close only the verified error window.
    $dialog.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern).Close()
    if (-not $child.WaitForExit(10000) -or $child.ExitCode -ne 1) { throw 'Archived script did not reject with exit code 1.' }
    foreach ($path in $hashes.Keys) { if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path).Hash -cne $hashes[$path]) { throw 'Directory-guard test changed its retained files.' } }
    [ordered]@{ observedAt=[DateTimeOffset]::Now.ToString('o'); source='src/SteamWatch.Installer/Uninstall.ps1'; originalScriptSha256=(Get-FileHash -LiteralPath $script).Hash.ToLowerInvariant(); childExitCode=$child.ExitCode; observedDialogTexts=$texts; acknowledgedBy='UIAutomation WindowPattern.Close on the verified error window'; retainedFileCount=$hashes.Count; retainedFileHashesMatched=$true; scope='Unmodified archived legacy script executed with child-only LOCALAPPDATA; rejected before confirmation or writes. Not active legacy uninstall acceptance.' } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputRoot 'evidence.json') -Encoding UTF8
    Write-Output (Join-Path $OutputRoot 'evidence.json')
}
finally {
    if (-not $child.HasExited -and $dialog) {
        $windowPattern = $null
        if ($dialog.TryGetCurrentPattern([Windows.Automation.WindowPattern]::Pattern, [ref]$windowPattern)) { $windowPattern.Close() }
        $null = $child.WaitForExit(5000)
    }
    if ($child.HasExited) {
        $child.StandardError.ReadToEnd() | Set-Content -LiteralPath (Join-Path $OutputRoot 'stderr.txt') -Encoding UTF8
        $child.StandardOutput.ReadToEnd() | Set-Content -LiteralPath (Join-Path $OutputRoot 'stdout.txt') -Encoding UTF8
    }
    $child.Dispose()
}
