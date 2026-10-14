param(
 [Parameter(Mandatory=$true)][string]$PublishDirectory,
 [string]$OutputRoot,
 [ValidateRange(10,120)][int]$IdleSeconds=30
)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$PublishDirectory=[IO.Path]::GetFullPath($PublishDirectory)
if(-not $OutputRoot) { $OutputRoot=Join-Path (Split-Path -Parent $PSScriptRoot) ('artifacts\desktop-performance-'+[guid]::NewGuid().ToString('N')) }
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
if(Test-Path -LiteralPath $OutputRoot) { throw 'Measurement output must be a new directory.' }
if(-not (Test-Path -LiteralPath (Join-Path $PublishDirectory 'portable.marker'))) { throw 'Portable publish directory required.' }
if(Test-Path -LiteralPath (Join-Path $PublishDirectory 'data')) { throw 'Published directory contains runtime data.' }
$startupKey=Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if($startupKey.PSObject.Properties['SteamWatch']) { throw 'Requires an isolated user without SteamWatch autorun.' }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
function Find-Id($root,$id) {
 $root.FindFirst([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::AutomationIdProperty,$id)))
}
function Sample($process) {
 $process.Refresh()
 @{cpuMs=$process.TotalProcessorTime.TotalMilliseconds;workingSetBytes=$process.WorkingSet64;privateBytes=$process.PrivateMemorySize64}
}
$results=New-Object Collections.Generic.List[object]
foreach($historyDays in @(1,1825)) {
 $profile=Join-Path $OutputRoot ('days-'+$historyDays)
 Copy-Item -LiteralPath $PublishDirectory -Destination $profile -Recurse
 $data=Join-Path $profile 'data'
 New-Item -ItemType Directory -Path $data | Out-Null
 @{closeWindowAction=1;startWithWindows=$false;playReminderSounds=$false;forceCloseCountdownSeconds=60;firstReminderThresholdPercent=70;secondReminderThresholdPercent=85;finalReminderThresholdPercent=95;requireAuthenticationForSensitiveActions=$false} |
  ConvertTo-Json | Set-Content -LiteralPath (Join-Path $data 'settings.json') -Encoding UTF8
 $days=@(for($day=0;$day -lt $historyDays;$day++) {
  $ticks=@{'730'=600000000}
  for($game=1;$game -lt 20;$game++) { $ticks[[string](10000+$game)]=[long]($game*600000000) }
  @{date=(Get-Date).Date.AddDays(-$day).ToString('yyyy-MM-dd');measuredTicks=$ticks;estimatedMinutes=@{}}
 })
 @{schemaVersion=2;days=$days;steamBaselines=@{};gameNames=@{'730'='Synthetic game'};estimatedEntries=@()} |
  ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $data 'playtime-ledger.json') -Encoding UTF8
 Set-Content -LiteralPath (Join-Path $data 'first-run-guide-shown.txt') -Value 'isolated performance fixture' -Encoding UTF8
 for($launch=1;$launch -le 3;$launch++) {
  $appProcess=$null
  try {
   $started=[Diagnostics.Stopwatch]::StartNew()
   $entry=Start-Process -FilePath (Join-Path $profile 'SteamWatch.exe') -WorkingDirectory $OutputRoot -WindowStyle Hidden -PassThru
   if(-not $entry.WaitForExit(10000) -or $entry.ExitCode -ne 0) { throw 'Portable launcher failed.' }
   $payload=Join-Path $profile 'app\SteamWatch.exe'
   $root=$null
   while($started.Elapsed.TotalSeconds -lt 45) {
    $appProcess=Get-Process -Name SteamWatch -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $payload } | Select-Object -First 1
    if($appProcess) {
     $appProcess.Refresh()
     if($appProcess.MainWindowHandle -ne 0) {
      $root=[Windows.Automation.AutomationElement]::FromHandle($appProcess.MainWindowHandle)
      $subtitle=Find-Id $root 'PageSubtitle'
      $refresh=Find-Id $root 'RefreshButton'
      if($subtitle -and $subtitle.Current.Name -match '^游戏 \d+ 个' -and $refresh -and $refresh.Current.IsEnabled) { break }
     }
    }
    Start-Sleep -Milliseconds 50
   }
   if(-not $root -or $started.Elapsed.TotalSeconds -ge 45) { throw 'Application did not become ready.' }
   $readyMs=$started.Elapsed.TotalMilliseconds
   $initial=Sample $appProcess
   $refreshTimes=New-Object Collections.Generic.List[double]
   $idleSamples=New-Object Collections.Generic.List[object]
   $idle=$null
   if($launch -eq 3) {
    $before=Sample $appProcess
    $clock=[Diagnostics.Stopwatch]::StartNew()
    while($clock.Elapsed.TotalSeconds -lt $IdleSeconds) {
     Start-Sleep -Seconds 5
     $idleSamples.Add((Sample $appProcess))
    }
    $after=Sample $appProcess
    $idle=@{seconds=$clock.Elapsed.TotalSeconds;cpuMs=$after.cpuMs-$before.cpuMs;cpuPercentOfMachine=100*($after.cpuMs-$before.cpuMs)/($clock.Elapsed.TotalMilliseconds*[Environment]::ProcessorCount);samples=$idleSamples.ToArray()}
    for($attempt=0;$attempt -lt 3;$attempt++) {
     $refresh=Find-Id $root 'RefreshButton'
     $ledgerFile=Join-Path $data 'playtime-ledger.json'
     $stamp=(Get-Item -LiteralPath $ledgerFile).LastWriteTimeUtc.Ticks
     $deadline=[Diagnostics.Stopwatch]::StartNew()
      $refresh.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
      while($deadline.Elapsed.TotalSeconds -lt 20) {
       if((Get-Item -LiteralPath $ledgerFile).LastWriteTimeUtc.Ticks -ne $stamp -and $refresh.Current.IsEnabled) { break }
       Start-Sleep -Milliseconds 20
      }
      if($deadline.Elapsed.TotalSeconds -ge 20) { throw 'Refresh save/ready signal not observed; no timing result.' }
      $feedback=Find-Id $root 'StatusFeedback'
      if($feedback -and $feedback.Current.Name -match '失败') { throw 'Refresh failed.' }
      $refreshTimes.Add($deadline.Elapsed.TotalMilliseconds)
    }
   }
   $results.Add(@{historyDays=$historyDays;gamesPerDay=20;launch=$launch;readyMs=$readyMs;initial=$initial;idle=$idle;refreshMs=$refreshTimes.ToArray()})
   $appProcess.CloseMainWindow() | Out-Null
   if(-not $appProcess.WaitForExit(15000)) { throw 'Measurement app did not exit normally.' }
  } finally {
   if($appProcess -and -not $appProcess.HasExited) { $appProcess.CloseMainWindow() | Out-Null; $null=$appProcess.WaitForExit(15000) }
  }
 }
 $measuredLedger=Get-Content -LiteralPath (Join-Path $data 'playtime-ledger.json') -Raw | ConvertFrom-Json
 if($measuredLedger.days.Count -ne $historyDays) { throw 'Unexpected ledger days; active-game measurement is not allowed.' }
 foreach($day in $measuredLedger.days) {
  $total=($day.measuredTicks.PSObject.Properties | Measure-Object -Property Value -Sum).Sum
  if($total -ne 114600000000) { throw 'Measured durations changed; do not report this as an idle fixture.' }
 }
}
@{kind='native-process-start-idle-and-refresh-measurement';at=(Get-Date -Format o);logicalProcessors=[Environment]::ProcessorCount;publishDllSha256=(Get-FileHash -LiteralPath (Join-Path $PublishDirectory 'app\SteamWatch.dll') -Algorithm SHA256).Hash;results=$results.ToArray();limits=@('synthetic ledgers, actual locally installed Steam catalog','new process launches, OS file cache not cleared','short foreground idle windows, excludes tray/long-run/fullscreen','refresh uses ledger replacement plus button readiness, sampled at 20ms with UIA overhead','requires current refresh implementation to persist the ledger','not clean-machine or real-game acceptance')} |
 ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'performance.json') -Encoding UTF8
Write-Output "PASS: 6 launches, 2 idle windows, 6 refreshes. Evidence: $OutputRoot"
