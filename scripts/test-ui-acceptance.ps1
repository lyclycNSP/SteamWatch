param(
    [Parameter(Mandatory=$true)][string]$HostPublishDirectory,
    [Parameter(Mandatory=$true)][string]$ProbeExecutable,
    [Parameter(Mandatory=$true)][string]$OutputRoot
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing,System.Windows.Forms
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class AcceptanceWindow {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h,int attribute,out Rect r,int size);
 [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int height,bool repaint);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr h);
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int command);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,IntPtr p);
 [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a,uint b,bool attach);
 public static bool Foreground(IntPtr h) {
  ShowWindow(h,9);
  uint a=GetCurrentThreadId(), b=GetWindowThreadProcessId(GetForegroundWindow(),IntPtr.Zero);
  bool attached=a!=b && AttachThreadInput(a,b,true);
  try { BringWindowToTop(h); SetForegroundWindow(h); } finally { if(attached) AttachThreadInput(a,b,false); }
  System.Threading.Thread.Sleep(100);
  return GetForegroundWindow()==h;
 }
}
'@
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
if(Test-Path -LiteralPath $OutputRoot){throw 'Acceptance output must be a new directory.'}
$hostExe=Join-Path $HostPublishDirectory 'SteamWatch.exe'
if(-not(Test-Path -LiteralPath $hostExe)){throw 'Acceptance host executable missing.'}
if([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $HostPublishDirectory 'SteamWatch.dll')).ProductVersion -notmatch '-uiacceptance(?:\+|$)'){
    throw 'Only an explicitly versioned acceptance host is allowed.'
}
$startup=Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if($startup.PSObject.Properties['SteamWatch']){throw 'Use an isolated user without an existing SteamWatch startup entry.'}
$app=Join-Path $OutputRoot 'portable/app'
$data=Join-Path $OutputRoot 'portable/data'
New-Item -ItemType Directory -Path $app,$data | Out-Null
Get-ChildItem -LiteralPath $HostPublishDirectory | Copy-Item -Destination $app -Recurse
Set-Content -LiteralPath (Join-Path $OutputRoot 'portable/portable.marker') -Value 'Acceptance host only'
Set-Content -LiteralPath (Join-Path $app 'ui-acceptance.host') -Value $data
@{closeWindowAction=1;startWithWindows=$false;playReminderSounds=$false;requireAuthenticationForSensitiveActions=$false} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $data 'settings.json')
Set-Content -LiteralPath (Join-Path $data 'first-run-guide-shown.txt') -Value 'Acceptance host only'
@{schemaVersion=1;days=@(@{date=(Get-Date -Format 'yyyy-MM-dd');measuredTicks=@{'730'=600000000};estimatedMinutes=@{'730'=15}});steamBaselines=@{};gameNames=@{'730'='验收历史记录 · Very Long Game Name 用于检查长文本换行与省略显示（合成数据）'}} |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $data 'playtime-ledger.json')
$key=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($data.ToUpperInvariant())))
$checks=[Collections.Generic.List[string]]::new()
$snapshots=[Collections.Generic.List[object]]::new()
$keyboard=[Collections.Generic.List[object]]::new()
$completed=$false
$process=$null
function Command([string]$command) {
    $pipe=[IO.Pipes.NamedPipeClientStream]::new('.',"SteamWatch-UI-Acceptance-$key",[IO.Pipes.PipeDirection]::InOut)
    try {
        $pipe.Connect(10000)
        $reader=[IO.StreamReader]::new($pipe,[Text.Encoding]::UTF8,$false,1024,$true)
        $writer=[IO.StreamWriter]::new($pipe,[Text.UTF8Encoding]::new($false),1024,$true)
        try {
            $writer.AutoFlush=$true; $writer.WriteLine($command)
            $line=$reader.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(12)).GetAwaiter().GetResult()
            $result=$line | ConvertFrom-Json
            if($result.error){throw $result.error}
            return $result
        } finally { $reader.Dispose(); $writer.Dispose() }
    } finally { $pipe.Dispose() }
}
function Assert($condition,[string]$name) { if(-not $condition){throw "FAIL: $name"}; $checks.Add($name) }
function Find-Name([string]$name) {
    return $root.FindFirst([Windows.Automation.TreeScope]::Descendants,
        [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$name))
}
function Capture([string]$name) {
    $null=Command activate
    Start-Sleep -Milliseconds 150
    if(-not [AcceptanceWindow]::Foreground($process.MainWindowHandle)){throw 'Acceptance window does not own foreground.'}
    Start-Sleep -Milliseconds 250
    $rect=[AcceptanceWindow+Rect]::new()
    if([AcceptanceWindow]::DwmGetWindowAttribute($process.MainWindowHandle,9,[ref]$rect,16) -ne 0){throw 'Cannot resolve visible owned window bounds.'}
    $bitmap=[Drawing.Bitmap]::new($rect.Right-$rect.Left,$rect.Bottom-$rect.Top)
    $graphics=[Drawing.Graphics]::FromImage($bitmap)
    try {
        if([AcceptanceWindow]::GetForegroundWindow() -ne $process.MainWindowHandle){throw 'Foreground lost before capture.'}
        $graphics.CopyFromScreen($rect.Left,$rect.Top,0,0,$bitmap.Size)
        $bitmap.Save((Join-Path $OutputRoot "$name.png"))
        $reference=$bitmap.GetPixel([int]($bitmap.Width/2),[int]($bitmap.Height/2))
        $varied=0
        for($x=15;$x -lt 90;$x+=2){for($y=15;$y -lt 90;$y+=2){
            $pixel=$bitmap.GetPixel([int]($bitmap.Width*$x/100),[int]($bitmap.Height*$y/100))
            if([Math]::Abs([int]$pixel.R-$reference.R)+[Math]::Abs([int]$pixel.G-$reference.G)+[Math]::Abs([int]$pixel.B-$reference.B) -gt 60){$varied++}
        }}
        Assert ($varied -ge 5) "Captured body contains rendered content: $name"
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}
function Navigate([string]$name) {
    $element=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,
        [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$name))
    if(-not $element){throw "Navigation missing: $name"}
    for($depth=0;$depth -lt 4 -and $element;$depth++){
        $pattern=$null
        if($element.TryGetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern,[ref]$pattern)){
            $pattern.Select(); Start-Sleep -Milliseconds 500; return
        }
        if($element.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern,[ref]$pattern)){
            $pattern.Invoke(); Start-Sleep -Milliseconds 500; return
        }
        if($element.TryGetCurrentPattern([Windows.Automation.TogglePattern]::Pattern,[ref]$pattern)){
            $pattern.Toggle(); Start-Sleep -Milliseconds 500; return
        }
        $element=[Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($element)
    }
    throw "No navigation pattern: $name"
}
try {
    $process=Start-Process -FilePath (Join-Path $app 'SteamWatch.exe') -WorkingDirectory (Split-Path $app -Parent) -PassThru
    for($attempt=0;$attempt -lt 100;$attempt++){
        $process.Refresh()
        if($process.HasExited){throw 'Acceptance host exited during startup.'}
        if($process.MainWindowHandle -ne 0){break}
        Start-Sleep -Milliseconds 200
    }
    $root=[Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    for($attempt=0;$attempt -lt 100;$attempt++){
        $initial=Command snapshot
        if($initial.initialized -and $initial.runtimeLoaded -and $initial.timerEnabled){break}
        Start-Sleep -Milliseconds 200
    }
    Assert ($initial.initialized -and $initial.runtimeLoaded) 'Production MainPage initialization completed'
    Assert ($initial.windowHandle -eq $process.MainWindowHandle.ToInt64()) 'Captured HWND matches actual MainWindow'
    Assert ($initial.notificationSubscribers -eq 1 -and $initial.monitorIdentity -ne 0) 'One notification subscriber and active runtime monitor'
    $dpi=[AcceptanceWindow]::GetDpiForWindow($process.MainWindowHandle)
    Capture 'default-theme'
    foreach($page in @('游戏','统计','设置','帮助与关于')){
        Navigate $page
        if(-not [AcceptanceWindow]::Foreground($process.MainWindowHandle)){throw 'Keyboard window does not own foreground.'}
        for($tab=0;$tab -lt 8;$tab++){
            if([AcceptanceWindow]::GetForegroundWindow() -ne $process.MainWindowHandle){throw 'Foreground lost before keyboard input.'}
            [Windows.Forms.SendKeys]::SendWait('{TAB}')
            Start-Sleep -Milliseconds 80
            $focus=[Windows.Automation.AutomationElement]::FocusedElement
            Assert ($focus.Current.ProcessId -eq $process.Id) "$page Tab $tab stays in owned application"
            $keyboard.Add(@{page=$page;tab=$tab;name=$focus.Current.Name;id=$focus.Current.AutomationId;type=$focus.Current.ControlType.ProgrammaticName})
        }
    }
    foreach($theme in @('light','dark')){
        $null=Command $theme
        foreach($size in @(@(800,600),@(1100,750))){
            [void][AcceptanceWindow]::MoveWindow($process.MainWindowHandle,40,40,[int]($size[0]*$dpi/96),[int]($size[1]*$dpi/96),$true)
            foreach($page in @('游戏','统计','设置','帮助与关于')){
                Navigate $page
                $state=Command snapshot
                Assert ($state.theme -ieq $theme) "$theme $($size[0]) $page uses requested application theme"
                $snapshots.Add(@{requestedDip=$size;requestedTheme=$theme;state=$state})
                $name="$theme-$($size[0])-$page"
                Capture $name
                $elements=$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition)
                @($elements | ForEach-Object { [ordered]@{name=$_.Current.Name;id=$_.Current.AutomationId;type=$_.Current.ControlType.ProgrammaticName;keyboardFocusable=$_.Current.IsKeyboardFocusable;offscreen=$_.Current.IsOffscreen} }) |
                    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputRoot "$name-uia.json")
                if($page -eq '统计'){
                    Navigate '每周汇总'
                    Capture "$theme-$($size[0])-每周汇总"
                    Navigate '每日明细'
                }
            }
        }
    }
    Navigate '游戏'
    $null=Command notification-burst; Start-Sleep -Milliseconds 400
    Assert ($null -ne (Find-Name 'QA 最新超限')) 'Reminder burst displays latest exceeded message'
    Assert ($null -eq (Find-Name 'QA 早期提醒') -and $null -eq (Find-Name 'QA 后期提醒')) 'Earlier threshold reminders are replaced'
    Navigate '知道了'; Start-Sleep -Milliseconds 300
    Assert ($null -eq (Find-Name 'QA 最新超限') -and $null -eq (Find-Name 'QA 早期提醒')) 'One acknowledgement drains reminder burst'
    $null=Command blocking-dialog; Start-Sleep -Milliseconds 200
    $null=Command notification-burst; Start-Sleep -Milliseconds 300
    Assert ($null -ne (Find-Name 'QA 编辑占用') -and $null -eq (Find-Name 'QA 最新超限')) 'Reminder waits without replacing an editing dialog'
    Navigate '关闭'; Start-Sleep -Milliseconds 300
    Assert ($null -ne (Find-Name 'QA 最新超限')) 'Only latest reminder follows editing dialog'
    $null=Command notification-update; Start-Sleep -Milliseconds 300
    Assert ($null -ne (Find-Name 'QA 最新强退') -and $null -eq (Find-Name 'QA 最新超限')) 'Visible reminder updates in place'
    Capture 'latest-reminder'
    Navigate '知道了'; Start-Sleep -Milliseconds 300
    Assert ($null -eq (Find-Name 'QA 最新强退') -and $null -eq (Find-Name 'QA 后期提醒')) 'Updated reminder closes once without old queued dialogs'
    for($cycle=1;$cycle -le 3;$cycle++){
        $null=Command detach; Start-Sleep -Milliseconds 300; $detached=Command snapshot
        Assert (-not $detached.attached -and -not $detached.timerEnabled -and $detached.notificationSubscribers -eq 0) "Remount $cycle detaches timer and subscription"
        $null=Command attach; Start-Sleep -Milliseconds 500; $attached=Command snapshot
        Assert ($attached.attached -and $attached.timerEnabled -and $attached.notificationSubscribers -eq 1) "Remount $cycle restores exactly one subscription and timer"
        Assert ($attached.initializationIdentity -eq $initial.initializationIdentity -and $attached.monitorIdentity -eq $initial.monitorIdentity -and $attached.generation -eq $initial.generation+$cycle) "Remount $cycle preserves initialization and runtime monitor, advances generation"
        $null=Command stale-notification; Start-Sleep -Milliseconds 300
        $stale=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'STALE ACCEPTANCE NOTIFICATION'))
        Assert ($null -eq $stale) "Remount $cycle rejects stale notification callback"
        $snapshots.Add(@{cycle=$cycle;detached=$detached;attached=$attached})
    }
    Capture 'remounted'
    $completed=$true
} catch {
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $OutputRoot 'failure.txt')
    if($root -and $process -and -not $process.HasExited){
        for($closeAttempt=0;$closeAttempt -lt 3;$closeAttempt++){
            $close=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,
                [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'CloseButton'))
            if(-not $close -or $close.Current.ProcessId -ne $process.Id){break}
            $close.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
            Start-Sleep -Milliseconds 350
        }
    }
    throw
} finally {
    if($process -and -not $process.HasExited){
        & $ProbeExecutable upgrade-client $data *> (Join-Path $OutputRoot 'normal-exit.log')
        if($LASTEXITCODE -ne 0){throw 'Acceptance host normal exit was not prepared; process retained.'}
        if(-not $process.WaitForExit(15000)){throw 'Acceptance host did not complete normal exit; process retained.'}
    }
    [ordered]@{completed=$completed;scope='Same production UI sources, explicitly versioned test-only host; not shipping binary or human acceptance';dpi=$dpi;highContrast=[Windows.Forms.SystemInformation]::HighContrast;checks=$checks;keyboard=$keyboard;snapshots=$snapshots;uncovered=@('Actual 100/150/200 percent displays','System high contrast','Full keyboard and screen-reader journeys','Long names and complete fault matrix')} |
        ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'evidence.json')
}
Write-Host "PASS: $($checks.Count) source-host checks. Evidence: $OutputRoot"
