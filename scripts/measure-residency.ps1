param(
 [Parameter(Mandatory=$true)][string]$PublishDirectory,
 [Parameter(Mandatory=$true)][string]$ProbeExecutable,
 [string]$OutputRoot,
 [ValidateRange(60,3600)][int]$StageSeconds=300
)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class ResidencyNative {
 [StructLayout(LayoutKind.Sequential)] public struct IoCounters {
  public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes;
 }
 [DllImport("kernel32.dll", SetLastError=true)] public static extern bool GetProcessIoCounters(IntPtr h,out IoCounters c);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int command);
}
"@
$PublishDirectory=[IO.Path]::GetFullPath($PublishDirectory)
$ProbeExecutable=[IO.Path]::GetFullPath($ProbeExecutable)
if(-not $OutputRoot) { $OutputRoot=Join-Path (Split-Path -Parent $PSScriptRoot) ('artifacts\residency-'+[guid]::NewGuid().ToString('N')) }
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
if(Test-Path -LiteralPath $OutputRoot) { throw 'Measurement output must be new.' }
if(-not (Test-Path -LiteralPath (Join-Path $PublishDirectory 'portable.marker'))) { throw 'Portable directory required.' }
if(Test-Path -LiteralPath (Join-Path $PublishDirectory 'data')) { throw 'Published directory must not contain user data.' }
if(-not (Test-Path -LiteralPath $ProbeExecutable)) { throw 'Probe executable missing.' }
$startup=Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if($startup.PSObject.Properties['SteamWatch']) { throw 'Requires a user without SteamWatch autorun.' }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$measurementDirectory=Join-Path $OutputRoot 'isolated-portable'
Copy-Item -LiteralPath $PublishDirectory -Destination $measurementDirectory -Recurse
$data=Join-Path $measurementDirectory 'data'
New-Item -ItemType Directory -Path $data | Out-Null
$historyDays=1825
$days=@(for($day=0;$day -lt $historyDays;$day++) {
 $ticks=@{'730'=600000000}
 for($game=1;$game -lt 20;$game++) { $ticks[[string](10000+$game)]=[long]($game*600000000) }
 @{date=(Get-Date).Date.AddDays(-$day).ToString('yyyy-MM-dd');measuredTicks=$ticks;estimatedMinutes=@{}}
})
@{schemaVersion=2;days=$days;steamBaselines=@{};gameNames=@{'730'='Synthetic residency fixture'};estimatedEntries=@()} |
 ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $data 'playtime-ledger.json') -Encoding UTF8
@{closeWindowAction=0;startWithWindows=$false;playReminderSounds=$false;forceCloseCountdownSeconds=60;firstReminderThresholdPercent=70;secondReminderThresholdPercent=85;finalReminderThresholdPercent=95;requireAuthenticationForSensitiveActions=$false} |
 ConvertTo-Json | Set-Content -LiteralPath (Join-Path $data 'settings.json') -Encoding UTF8
Set-Content -LiteralPath (Join-Path $data 'first-run-guide-shown.txt') -Value 'isolated residency measurement' -Encoding UTF8
function Find-Id($root,$id) {
 $root.FindFirst([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::AutomationIdProperty,$id)))
}
function Sample($process) {
 $process.Refresh()
 if($process.HasExited) { throw 'Measurement process exited unexpectedly.' }
 $io=New-Object ResidencyNative+IoCounters
 if(-not [ResidencyNative]::GetProcessIoCounters($process.Handle,[ref]$io)) { throw 'Cannot sample process I/O counters.' }
 @{cpuMs=$process.TotalProcessorTime.TotalMilliseconds;workingSetBytes=$process.WorkingSet64;privateBytes=$process.PrivateMemorySize64;handles=$process.HandleCount;threads=$process.Threads.Count;readOperations=$io.ReadOperations;writeOperations=$io.WriteOperations;readBytes=$io.ReadBytes;writeBytes=$io.WriteBytes}
}
$appProcess=$null
$phases=New-Object Collections.Generic.List[object]
$normalExit=$false
try {
 $entry=Start-Process -FilePath (Join-Path $measurementDirectory 'SteamWatch.exe') -WorkingDirectory $OutputRoot -WindowStyle Hidden -PassThru
 if(-not $entry.WaitForExit(10000) -or $entry.ExitCode -ne 0) { throw 'Portable launcher failed.' }
 $ready=[Diagnostics.Stopwatch]::StartNew()
 $root=$null
 while($ready.Elapsed.TotalSeconds -lt 45) {
  $appProcess=Get-Process SteamWatch -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $measurementDirectory 'app\SteamWatch.exe') } | Select-Object -First 1
  if($appProcess -and $appProcess.MainWindowHandle -ne 0) {
   $root=[Windows.Automation.AutomationElement]::FromHandle($appProcess.MainWindowHandle)
   $subtitle=Find-Id $root 'PageSubtitle'
   $refresh=Find-Id $root 'RefreshButton'
   if($subtitle -and $subtitle.Current.Name -match '^\u6E38\u620F \d+ \u4E2A' -and $refresh -and $refresh.Current.IsEnabled) { break }
  }
  Start-Sleep -Milliseconds 100
 }
 if(-not $root -or $ready.Elapsed.TotalSeconds -ge 45) { throw 'Application was not ready.' }
 $window=$appProcess.MainWindowHandle
 $catalogSubtitle=(Find-Id $root 'PageSubtitle').Current.Name
 $ledgerPath=Join-Path $data 'playtime-ledger.json'
 Start-Sleep -Seconds 10
 foreach($phase in @('foreground','tray')) {
  if($phase -eq 'foreground') { [ResidencyNative]::ShowWindow($window,9) | Out-Null }
  else { $appProcess.CloseMainWindow() | Out-Null }
  $expectedVisible=$phase -eq 'foreground'
  $transition=[Diagnostics.Stopwatch]::StartNew()
  while([ResidencyNative]::IsWindowVisible($window) -ne $expectedVisible -and $transition.Elapsed.TotalSeconds -lt 10) { Start-Sleep -Milliseconds 100 }
  if([ResidencyNative]::IsWindowVisible($window) -ne $expectedVisible) { throw 'Window visibility transition failed.' }
  $baseline=Sample $appProcess
  $stamp=(Get-Item -LiteralPath $ledgerPath).LastWriteTimeUtc.Ticks
  $replacements=0
  $samples=New-Object Collections.Generic.List[object]
  $clock=[Diagnostics.Stopwatch]::StartNew()
  while($clock.Elapsed.TotalSeconds -lt $StageSeconds) {
   Start-Sleep -Seconds 5
   if([ResidencyNative]::IsWindowVisible($window) -ne $expectedVisible) { throw 'Window visibility changed during phase.' }
   $sample=Sample $appProcess
   $sample.seconds=$clock.Elapsed.TotalSeconds
   $sample.visible=$expectedVisible
   $currentStamp=(Get-Item -LiteralPath $ledgerPath).LastWriteTimeUtc.Ticks
   if($currentStamp -ne $stamp) { $replacements++; $stamp=$currentStamp }
   $samples.Add($sample)
   @{phase=$phase;sample=$sample} | ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $OutputRoot 'samples.jsonl') -Encoding UTF8
  }
  $last=$samples[$samples.Count-1]
  $phases.Add(@{phase=$phase;seconds=$clock.Elapsed.TotalSeconds;baseline=$baseline;samples=$samples.ToArray();cpuMs=$last.cpuMs-$baseline.cpuMs;cpuPercentOfMachine=100*($last.cpuMs-$baseline.cpuMs)/($last.seconds*1000*[Environment]::ProcessorCount);observedLedgerReplacements=$replacements})
  Write-Output "Measured $phase for $($clock.Elapsed.TotalSeconds) seconds."
 }
 & $ProbeExecutable upgrade-client $data
 if($LASTEXITCODE -ne 0 -or -not $appProcess.WaitForExit(15000) -or $appProcess.ExitCode -ne 0) { throw 'Normal upgrade exit failed.' }
 $normalExit=$true
 $ledger=Get-Content -LiteralPath $ledgerPath -Raw | ConvertFrom-Json
 if($ledger.days.Count -ne $historyDays) { throw 'Idle ledger gained or lost dates.' }
 foreach($day in $ledger.days) {
  if(@($day.measuredTicks.PSObject.Properties).Count -ne 20) { throw 'Idle ledger game count changed.' }
  foreach($game in $day.measuredTicks.PSObject.Properties) {
   $expected=if($game.Name -eq '730') { [long]600000000 } else { [long]([int]$game.Name-10000)*600000000 }
   if($game.Value -ne $expected) { throw 'Idle durations changed; this is not an idle measurement.' }
  }
 }
 @{kind='native-foreground-and-tray-bounded-residency';at=(Get-Date -Format o);stageSeconds=$StageSeconds;historyDays=$historyDays;gamesPerDay=20;catalogSubtitle=$catalogSubtitle;logicalProcessors=[Environment]::ProcessorCount;os=[Environment]::OSVersion.VersionString;publishDllSha256=(Get-FileHash -LiteralPath (Join-Path $PublishDirectory 'app\SteamWatch.dll') -Algorithm SHA256).Hash;normalExit=$normalExit;ledgerMeasuredTicksUnchanged=$true;phases=$phases.ToArray();limits=@('single machine/run, five minutes per phase by default, not overnight stability','foreground then tray order, no randomization or OS-cache clearing','synthetic history with actual locally installed Steam catalog','process I/O counters include all process I/O, not only ledger storage','five-second sampling cannot count every file replacement','tray state verified by normal close and hidden window; tray icon menu not clicked','active-session workload is a separate service-level measurement')} |
  ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'residency.json') -Encoding UTF8
 Write-Output "PASS native residency: $OutputRoot"
} catch {
 @{error=$_.Exception.Message;normalExit=$normalExit;completedPhases=$phases.ToArray()} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'failure.json') -Encoding UTF8
 throw
} finally {
 if($appProcess -and -not $appProcess.HasExited) {
  & $ProbeExecutable upgrade-client $data
  if(-not $appProcess.WaitForExit(15000)) { Write-Warning "Isolated app still running: $($appProcess.Id); use normal exit." }
 }
}
