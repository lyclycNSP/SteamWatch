param(
    [Parameter(Mandatory=$true)][string]$PublishDirectory,
    [Parameter(Mandatory=$true)][string]$ProbeExecutable,
    [Parameter(Mandatory=$true)][string]$OutputRoot,
    [ValidateRange(1,10)][int]$RepeatCount=3,
    [ValidateSet('IfMinimized','Always')][string]$RestoreMode='IfMinimized',
    [ValidateSet('AfterInitialization','BeforeInitialization')][string]$ActivationTiming='AfterInitialization',
    [switch]$WakeDisplay
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class StartupWindow {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int command);
 [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr h);
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,IntPtr process);
 [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
 [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
 [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a,uint b,bool attach);
 [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h,int attribute,out Rect r,int bytes);
 public static bool Foreground(IntPtr h,bool restoreAlways) {
  if(restoreAlways || IsIconic(h)) ShowWindow(h,9);
  if(GetForegroundWindow()==h) return true;
  uint a=GetCurrentThreadId(), b=GetWindowThreadProcessId(GetForegroundWindow(),IntPtr.Zero);
  bool attached=a!=b && AttachThreadInput(a,b,true);
  try { BringWindowToTop(h); SetForegroundWindow(h); }
  finally { if(attached) AttachThreadInput(a,b,false); }
  return GetForegroundWindow()==h;
 }
}
'@
$PublishDirectory=[IO.Path]::GetFullPath($PublishDirectory)
$ProbeExecutable=[IO.Path]::GetFullPath($ProbeExecutable)
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
if(Test-Path -LiteralPath $OutputRoot){throw 'Startup output must be new.'}
if(-not(Test-Path -LiteralPath (Join-Path $PublishDirectory 'portable.marker'))){throw 'Portable publish required.'}
if(Test-Path -LiteralPath (Join-Path $PublishDirectory 'data')){throw 'Do not test a published directory containing runtime data.'}
$startup=Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if($startup.PSObject.Properties['SteamWatch']){throw 'Use an isolated user without a SteamWatch startup entry.'}
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$cases=[Collections.Generic.List[object]]::new()
function Capture([IntPtr]$handle,[string]$file) {
    if([StartupWindow]::GetForegroundWindow() -ne $handle){throw 'Foreground lost; refusing unrelated capture.'}
    $rect=[StartupWindow+Rect]::new()
    if([StartupWindow]::DwmGetWindowAttribute($handle,9,[ref]$rect,16) -ne 0){throw 'Owned visible bounds unavailable.'}
    $bitmap=[Drawing.Bitmap]::new($rect.Right-$rect.Left,$rect.Bottom-$rect.Top)
    $graphics=[Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left,$rect.Top,0,0,$bitmap.Size)
        $bitmap.Save($file,[Drawing.Imaging.ImageFormat]::Png)
        $reference=$bitmap.GetPixel([int]($bitmap.Width/2),[int]($bitmap.Height/2)); $varied=0
        for($x=15;$x -lt 90;$x+=2){for($y=15;$y -lt 90;$y+=2){
            $pixel=$bitmap.GetPixel([int]($bitmap.Width*$x/100),[int]($bitmap.Height*$y/100))
            if([Math]::Abs([int]$pixel.R-$reference.R)+[Math]::Abs([int]$pixel.G-$reference.G)+[Math]::Abs([int]$pixel.B-$reference.B) -gt 60){$varied++}
        }}
        return @{rendered=($varied -ge 5);variedSamples=$varied;image=[IO.Path]::GetFileName($file)}
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}
try {
if($WakeDisplay -and [StartupWindow]::SetThreadExecutionState([uint32]2147483651) -eq 0){throw 'Cannot keep display awake for startup capture.'}
foreach($mode in @('LauncherForeignCwd','PayloadOwnCwd','PayloadForeignCwd')) {
    $caseRoot=Join-Path $OutputRoot $mode
    Copy-Item -LiteralPath $PublishDirectory -Destination $caseRoot -Recurse
    $data=Join-Path $caseRoot 'data'
    New-Item -ItemType Directory -Path $data | Out-Null
    @{closeWindowAction=1;startWithWindows=$false;playReminderSounds=$false;requireAuthenticationForSensitiveActions=$false} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $data 'settings.json') -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $data 'first-run-guide-shown.txt') -Value 'Isolated startup QA'
    $payload=Join-Path $caseRoot 'app/SteamWatch.exe'
    for($iteration=1;$iteration -le $RepeatCount;$iteration++) {
        $appProcess=$null
        $result=[ordered]@{mode=$mode;iteration=$iteration;restoreMode=$RestoreMode;activationTiming=$ActivationTiming;wakeDisplay=[bool]$WakeDisplay;passed=$false;samples=@();normalExit=$false}
        $watch=[Diagnostics.Stopwatch]::StartNew()
        try {
            if($mode -eq 'LauncherForeignCwd') {
                $launcher=Start-Process -FilePath (Join-Path $caseRoot 'SteamWatch.exe') -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru
                $null=$launcher.Handle
                if(-not $launcher.WaitForExit(5000) -or $launcher.ExitCode -ne 0){throw 'Root launcher failed.'}
                for($attempt=0;$attempt -lt 100;$attempt++) {
                    $appProcess=Get-Process -Name SteamWatch -ErrorAction SilentlyContinue | Where-Object Path -eq $payload | Select-Object -First 1
                    if($appProcess){break}; Start-Sleep -Milliseconds 100
                }
            } else {
                $cwd=if($mode -eq 'PayloadOwnCwd'){Split-Path $payload -Parent}else{$PSScriptRoot}
                $appProcess=Start-Process -FilePath $payload -WorkingDirectory $cwd -PassThru
            }
            if(-not $appProcess){throw 'Owned application missing.'}
            $null=$appProcess.Handle
            for($attempt=0;$attempt -lt 100;$attempt++) {
                $appProcess.Refresh()
                if($appProcess.HasExited){throw 'Application exited during startup.'}
                if($appProcess.MainWindowHandle -ne 0){break}; Start-Sleep -Milliseconds 100
            }
            $handle=$appProcess.MainWindowHandle
            if($handle -eq [IntPtr]::Zero){throw 'Window did not appear.'}
            $root=[Windows.Automation.AutomationElement]::FromHandle($handle)
            $result.foregroundAtWindowCreation=[StartupWindow]::GetForegroundWindow() -eq $handle
            if($ActivationTiming -eq 'BeforeInitialization' -and -not [StartupWindow]::Foreground($handle,($RestoreMode -eq 'Always'))){throw 'Cannot own foreground before initialization.'}
            for($attempt=0;$attempt -lt 100;$attempt++) {
                $refresh=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,
                    [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'RefreshButton'))
                if($refresh -and $refresh.Current.IsEnabled){break}; Start-Sleep -Milliseconds 100
            }
            if(-not $refresh -or -not $refresh.Current.IsEnabled){throw 'Initialization did not enable refresh.'}
            $result.initializationMs=$watch.ElapsedMilliseconds
            $result.dpi=[StartupWindow]::GetDpiForWindow($handle)
            $result.wasForeground=[StartupWindow]::GetForegroundWindow() -eq $handle
            $result.wasMinimized=[StartupWindow]::IsIconic($handle)
            if(-not [StartupWindow]::Foreground($handle,($RestoreMode -eq 'Always'))){throw 'Cannot own foreground.'}
            foreach($delay in @(250,1500)) {
                Start-Sleep -Milliseconds $delay
                $sample=Capture $handle (Join-Path $OutputRoot "$mode-$iteration-$delay.png")
                $sample.elapsedMs=$watch.ElapsedMilliseconds
                $result.samples+=,$sample
            }
            $result.passed=$result.samples[-1].rendered
            if(-not $result.passed){$result.error='Blank body remained after both captures.'}
        } catch {
            $result.error=$_.Exception.Message
        } finally {
            if($appProcess -and -not $appProcess.HasExited) {
                & $ProbeExecutable upgrade-client $data *> (Join-Path $OutputRoot "$mode-$iteration-exit.log")
                $result.normalExit=($LASTEXITCODE -eq 0 -and $appProcess.WaitForExit(15000))
            }
            if(-not $result.normalExit){$result.passed=$false}
            $cases.Add($result)
            @{scope='Isolated real packaged WinUI startup; direct-payload modes are diagnostic comparisons, not recommended entry points';cases=$cases} |
                ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputRoot 'evidence.json')
        }
        if(-not $result.normalExit){throw 'Normal exit failed; owned process retained. No force termination.'}
    }
}
$failed=@($cases | Where-Object {-not $_.passed})
if($failed.Count){throw "$($failed.Count)/$($cases.Count) startup cases failed; all evidence retained."}
Write-Output "PASS: $($cases.Count) startup cases. Evidence: $OutputRoot"
} finally {
    if($WakeDisplay){$null=[StartupWindow]::SetThreadExecutionState([uint32]2147483648)}
}
