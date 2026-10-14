param(
    [Parameter(Mandatory=$true)][string]$PublishDirectory,
    [Parameter(Mandatory=$true)][string]$ProbeExecutable,
    [string]$OutputRoot
)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class DesktopQaNative {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h,int x,int y,int width,int height,bool repaint);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h,int command);
 [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr h);
 [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h,IntPtr process);
 [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
 [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
 [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint a,uint b,bool attach);
 public static bool ForegroundQaWindow(IntPtr h) {
  ShowWindow(h,9);
  var current=GetCurrentThreadId(); var foreground=GetWindowThreadProcessId(GetForegroundWindow(),IntPtr.Zero);
  bool attached=current!=foreground && AttachThreadInput(current,foreground,true);
  try { BringWindowToTop(h); SetForegroundWindow(h); }
  finally { if(attached) AttachThreadInput(current,foreground,false); }
  return GetForegroundWindow()==h;
 }
 [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr h,uint flags);
 [DllImport("user32.dll")] private static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] private static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 [StructLayout(LayoutKind.Sequential)] private struct Point { public int X,Y; }
 [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
 public static bool ClickOwnedControl(IntPtr h,int x,int y) {
  var root=GetAncestor(h,2); SetForegroundWindow(root); System.Threading.Thread.Sleep(100);
  if(GetForegroundWindow()!=root || GetAncestor(WindowFromPoint(new Point { X=x,Y=y }),2)!=root) return false;
  SetCursorPos(x,y); mouse_event(2,0,0,0,UIntPtr.Zero); mouse_event(4,0,0,0,UIntPtr.Zero); return true;
 }
 public static bool ClickGameRow(IntPtr h,int x,int y,bool right,bool twice) {
  if(!ForegroundQaWindow(h) || GetAncestor(WindowFromPoint(new Point { X=x,Y=y }),2)!=GetAncestor(h,2)) return false;
  SetCursorPos(x,y);
  for(int i=0;i<(twice?2:1);i++) {
   mouse_event(right?8u:2u,0,0,0,UIntPtr.Zero); mouse_event(right?16u:4u,0,0,0,UIntPtr.Zero);
   if(twice && i==0) System.Threading.Thread.Sleep(75);
  }
  return true;
 }
 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,string l);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,System.Text.StringBuilder l);
 private delegate bool EnumProc(IntPtr h,IntPtr p);
 [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr h,EnumProc p,IntPtr l);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr h,System.Text.StringBuilder b,int n);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr h,System.Text.StringBuilder b,int n);
 public static bool SetFileName(IntPtr host,string path) {
  IntPtr edit=IntPtr.Zero;
  EnumChildWindows(host,(h,p)=>{ var name=new System.Text.StringBuilder(128); GetClassName(h,name,128); if(name.ToString()=="Edit") { edit=h; return false; } return true; },IntPtr.Zero);
  if(edit==IntPtr.Zero) return false;
  SendMessage(edit,0xC,IntPtr.Zero,path);
  var text=new System.Text.StringBuilder(32768); SendMessage(edit,0xD,(IntPtr)text.Capacity,text);
  return text.ToString()==path;
 }
}
"@
# Always clone into a new directory; never seed or replace an existing user's data.
if (-not $OutputRoot) { $OutputRoot = Join-Path (Split-Path -Parent $PSScriptRoot) ('artifacts\desktop-' + [guid]::NewGuid().ToString('N')) }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) { throw 'QA output must be a new directory.' }
if (-not (Test-Path -LiteralPath (Join-Path $PublishDirectory 'portable.marker'))) { throw 'QA requires a portable publish directory.' }
$startupKey = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if ($startupKey.PSObject.Properties['SteamWatch']) { throw 'Backup restore QA requires an isolated user without an existing SteamWatch startup entry.' }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$app = Join-Path $OutputRoot '便携版 空格'
Copy-Item -LiteralPath $PublishDirectory -Destination $app -Recurse
$data = Join-Path $app 'data'
if (Test-Path -LiteralPath $data) { throw 'Published directory unexpectedly contains runtime data.' }
New-Item -ItemType Directory -Path $data | Out-Null
$settings = @{closeWindowAction=1;startWithWindows=$false;playReminderSounds=$false;forceCloseCountdownSeconds=60;firstReminderThresholdPercent=70;secondReminderThresholdPercent=85;finalReminderThresholdPercent=95;requireAuthenticationForSensitiveActions=$false}
$settings | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $data 'settings.json') -Encoding UTF8
@{schemaVersion=1;days=@(@{date=(Get-Date -Format 'yyyy-MM-dd');measuredTicks=@{'730'=600000000};estimatedMinutes=@{'730'=15}});steamBaselines=@{};gameNames=@{'730'='历史测试记录'}} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $data 'playtime-ledger.json') -Encoding UTF8
Set-Content -LiteralPath (Join-Path $data 'first-run-guide-shown.txt') -Value 'isolated QA' -Encoding UTF8
$script:qaProcess = $null
$script:root = $null
$checks = New-Object Collections.Generic.List[string]
function Find-Property($scope,$property,$value) { $scope.FindFirst([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition($property,$value))) }
function Find-Name($name) { Find-Property $script:root ([Windows.Automation.AutomationElement]::NameProperty) $name }
function Find-Id($scope,$id) { Find-Property $scope ([Windows.Automation.AutomationElement]::AutomationIdProperty) $id }
function Activate($element) {
 if (-not $element) { throw 'Required UI element missing.' }
 for($depth=0;$depth -lt 4;$depth++) {
  $pattern=$null
  if($element.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern,[ref]$pattern)) { $pattern.Invoke(); return }
  if($element.TryGetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern,[ref]$pattern)) { $pattern.Select(); return }
  if($element.TryGetCurrentPattern([Windows.Automation.TogglePattern]::Pattern,[ref]$pattern)) { $pattern.Toggle(); return }
  if($element.Current.NativeWindowHandle -ne 0 -and $element.Current.AutomationId -in @('1','2')) {
   $bounds=$element.Current.BoundingRectangle
   if(-not [DesktopQaNative]::ClickOwnedControl([IntPtr]$element.Current.NativeWindowHandle,[int]($bounds.X+$bounds.Width/2),[int]($bounds.Y+$bounds.Height/2))) { throw 'Native QA control does not own the click target.' }
   return
  }
  $element=[Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($element)
 }
 throw 'UI element has no supported activation method.'
}
function Click($name) {
 for($attempt=0;$attempt -lt 100;$attempt++) {
  $element=Find-Name $name
  if($element -and $element.Current.IsEnabled) { Activate $element; Start-Sleep -Milliseconds 350; return }
  Start-Sleep -Milliseconds 100
 }
 throw "UI control did not become available: $name"
}
function Wait-SettingsIdle {
 for($attempt=0;$attempt -lt 50;$attempt++) {
  if((Find-Id $script:root 'SettingsView').Current.IsEnabled) { return }
  Start-Sleep -Milliseconds 100
 }
 throw 'Settings save did not finish.'
}
function Wait-RuleIdle {
 for($attempt=0;$attempt -lt 100;$attempt++) {
  $button=Find-Id $script:root 'SetLimitForSelectedButton'
  if($button -and $button.Current.IsEnabled) { return }
  Start-Sleep -Milliseconds 100
 }
 throw 'Rule operation did not finish; inspect the retained editor or authentication dialog.'
}
function Set-SettingsNumber($id,$value) {
 $number=Find-Id $script:root $id
 $input=Find-Id $number 'InputBox'
 $input.SetFocus()
 $input.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($value)
 (Find-Id $script:root 'SaveReminderSettingsButton').SetFocus()
 Start-Sleep -Milliseconds 200
}
function SettingsNumber($id) {
 (Find-Id (Find-Id $script:root $id) 'InputBox').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value
}
function Assert-SettingsBounds {
 $viewport=(Find-Id $script:root 'SettingsView').Current.BoundingRectangle
 # Compact NumberBox arrows have no separate UIA peers; review full control screenshots after resize.
 if([Math]::Abs((Find-Id $script:root 'FirstReminderThresholdNumberBox').Current.BoundingRectangle.Left-$viewport.Left) -gt 2) { throw 'Settings content shifted away from its left anchor.' }
 foreach($id in @('ForceCloseCountdownNumberBox','FirstReminderThresholdNumberBox','SecondReminderThresholdNumberBox','FinalReminderThresholdNumberBox','SaveReminderSettingsButton')) {
  $bounds=(Find-Id $script:root $id).Current.BoundingRectangle
  if($bounds.Left -lt ($viewport.Left-1) -or $bounds.Right -gt ($viewport.Right+1) -or $bounds.Width -lt 50) { throw "Settings control exceeds horizontal viewport: $id" }
 }
}
function Set-Value($element,$text) {
 $element.SetFocus(); $element.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($text)
 # NumberBox commits its inner edit on focus loss, as a user's click on Save would do.
 if($element.Current.AutomationId -eq 'InputBox') { (Find-Id $script:root 'PrimaryButton').SetFocus(); Start-Sleep -Milliseconds 200 }
}
function Shift-CalendarDate($id,[int]$days) {
 if(-not [DesktopQaNative]::ForegroundQaWindow($script:qaProcess.MainWindowHandle)) { throw 'QA calendar does not own keyboard focus.' }
 $direction=if($days -lt 0) { 'LEFT' } else { 'RIGHT' }
 $step=if($days -lt 0) { -1 } else { 1 }
 # WinUI calendar focus can lag repeated SendKeys input. Commit and verify each day
 # before reopening, so a missed input cannot masquerade as refresh losing state.
 for($offset=0;$offset -lt [Math]::Abs($days);$offset++) {
  $dateText=$null
  for($attempt=0;$attempt -lt 50;$attempt++) {
   $picker=Find-Id $script:root $id
   if($picker -and $picker.Current.IsEnabled -and -not $picker.Current.IsOffscreen) {
    $dateText=Find-Id $picker 'DateText'
    if($dateText) { break }
   }
   Start-Sleep -Milliseconds 100
  }
  if(-not $dateText) { throw "QA calendar picker did not become available: $id" }
  $before=$dateText.Current.Name
  $expected=[DateTime]::ParseExact($before,'yyyy-MM-dd',[Globalization.CultureInfo]::InvariantCulture).AddDays($step).ToString('yyyy-MM-dd')
  Activate $picker
  Start-Sleep -Milliseconds 350
  $calendar=$null
  for($attempt=0;$attempt -lt 50;$attempt++) {
   $calendar=Find-Id $script:root 'CalendarView'
   if($calendar) { break }
   Start-Sleep -Milliseconds 100
  }
  if(-not $calendar) { throw "QA calendar popup did not open: $id" }
  $selected=$calendar.GetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()
  if($selected.Count -ne 1) { throw 'QA calendar has no single selected day.' }
  $selected[0].SetFocus()
  [Windows.Forms.SendKeys]::SendWait(('{'+$direction+'}{ENTER}'))
  for($attempt=0;$attempt -lt 50;$attempt++) {
   if((Find-Id $picker 'DateText').Current.Name -eq $expected) { break }
   Start-Sleep -Milliseconds 100
  }
  $actual=(Find-Id $picker 'DateText').Current.Name
  if($actual -ne $expected) { throw "Calendar input did not commit expected date: $before -> $expected, actual $actual" }
 }
}
function Select-Option($combo,$name) {
 $combo.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
 Start-Sleep -Milliseconds 200
 Activate (Find-Name $name)
 Start-Sleep -Milliseconds 350
}
function Select-CloseAction($name,$expected) {
 Select-Option (Find-Id $script:root 'CloseWindowActionComboBox') $name
 Wait-SettingsIdle
 $combo=Find-Id $script:root 'CloseWindowActionComboBox'
 $selected=$combo.GetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()
 $persisted=Get-Content -LiteralPath (Join-Path $data 'settings.json') -Raw | ConvertFrom-Json
 if($selected.Count -ne 1 -or $selected[0].Current.Name -ne $name -or $persisted.closeWindowAction -ne $expected) { throw 'Close action selection did not persist.' }
}
function Pointer-GameRow($row,[bool]$right=$false,[bool]$twice=$false) {
 if(-not [DesktopQaNative]::ForegroundQaWindow($script:qaProcess.MainWindowHandle)) { throw 'Game row window does not own foreground.' }
 # Refresh the peer by AppID after list projections; do not reuse old coordinates.
 $identity=$row.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition) |
  Where-Object { $_.Current.Name -match '^AppID \d+$' } | Select-Object -First 1
 if(-not $identity) { throw 'Game row identity missing.' }
 $identityName=$identity.Current.Name
 $rows=(Find-Id $script:root 'GamesListView').FindAll([Windows.Automation.TreeScope]::Children,
  (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem)))
 $row=$rows | Where-Object { Find-Property $_ ([Windows.Automation.AutomationElement]::NameProperty) $identityName } | Select-Object -First 1
 if(-not $row) { throw 'Current game row missing.' }
 $stable=$false
 for($attempt=0;$attempt -lt 100;$attempt++) {
  $previousBounds=$row.Current.BoundingRectangle
  Start-Sleep -Milliseconds 100
  if($row.Current.BoundingRectangle.Equals($previousBounds)) { $stable=$true; break }
 }
 if(-not $stable) { throw 'Game row layout did not stabilize.' }
 $bounds=$row.Current.BoundingRectangle
 if($row.Current.IsOffscreen -or -not [DesktopQaNative]::ClickGameRow($script:qaProcess.MainWindowHandle,[int]($bounds.Left+50),[int]($bounds.Top+$bounds.Height/2),$right,$twice)) { throw 'Game row does not own the pointer target.' }
 Start-Sleep -Milliseconds 500
 if($twice) {
  for($attempt=0;$attempt -lt 100;$attempt++) {
   if(Find-Id $script:root 'InputBox') { return }
   Start-Sleep -Milliseconds 100
  }
  throw 'Real double click did not open the limit editor.'
 }
}
function Click-GameMenu($text) {
 $items=$script:root.FindAll([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty,$text)))
 $item=$items | Where-Object { $_.Current.ControlType -eq [Windows.Automation.ControlType]::MenuItem -and -not $_.Current.IsOffscreen -and $_.Current.IsEnabled } | Select-Object -First 1
 if(-not $item) { throw "Game context menu item absent: $text" }
 Activate $item; Start-Sleep -Milliseconds 350
}
function Start-QaApp {
 $entry = Start-Process -FilePath (Join-Path $app 'SteamWatch.exe') -WorkingDirectory $OutputRoot -WindowStyle Hidden -PassThru
 $null=$entry.Handle
 $payloadExecutable=Join-Path $app 'app\SteamWatch.exe'
 if(Test-Path -LiteralPath $payloadExecutable) {
  if(-not $entry.WaitForExit(5000) -or $entry.ExitCode -ne 0) { throw 'Portable launcher failed.' }
  $script:qaProcess=$null
  for($attempt=0;$attempt -lt 40;$attempt++) {
   $script:qaProcess=Get-Process -Name SteamWatch -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $payloadExecutable } | Select-Object -First 1
   if($script:qaProcess) { break }; Start-Sleep -Milliseconds 250
  }
 } else { $script:qaProcess=$entry }
 if(-not $script:qaProcess) { throw 'Launched application process missing.' }
 $null=$script:qaProcess.Handle # Attached child needs a retained handle to read ExitCode in Windows PowerShell.
 for($attempt=0;$attempt -lt 30;$attempt++) { $script:qaProcess.Refresh(); if($script:qaProcess.MainWindowHandle -ne 0) { break }; Start-Sleep -Milliseconds 300 }
 if($script:qaProcess.HasExited -or $script:qaProcess.MainWindowHandle -eq 0) { throw 'QA window failed to start.' }
 $script:root=[Windows.Automation.AutomationElement]::FromHandle($script:qaProcess.MainWindowHandle)
 Start-Sleep -Seconds 2
}
function Assert-Estimate([int]$minutes) {
 $ledger=Get-Content -LiteralPath (Join-Path $data 'playtime-ledger.json') -Raw | ConvertFrom-Json
 if($ledger.schemaVersion -ne 2 -or [int]$ledger.days[0].estimatedMinutes.'730' -ne $minutes -or [long]$ledger.days[0].measuredTicks.'730' -ne 600000000) { throw "Ledger estimate/measured mismatch: expected $minutes" }
}
function Correct-Estimate([int]$minutes) {
 Click '统计'; Click '检查／修正估算'
 Set-Value (Find-Id $script:root 'InputBox') ([string]$minutes)
 Set-Value (Find-Name '修正原因（必填）') '桌面验收修正'
 Click '修正估算'
 $reasonLine=Find-Name '原因：桌面验收修正'
 $backupLine=Find-Name '保存前会备份当前数据。'
 if(-not $reasonLine -or -not $backupLine -or ($backupLine.Current.BoundingRectangle.Top-$reasonLine.Current.BoundingRectangle.Bottom) -lt (10*$dpiScale)) { throw 'Correction confirmation paragraphs are crowded.' }
 Screenshot 'estimate-confirmation.png'
 Click '确认修正'
 Start-Sleep -Milliseconds 500
 Assert-Estimate $minutes
 $label=Find-Id $script:root 'DailyStatsTotalText'
 if($label.Current.Name -notlike "*总时长 $($minutes + 1) 分钟*") { throw 'Statistics UI did not reflect correction.' }
}
function Maintenance { Click '设置'; Click '应用维护' }
function Wait-Picker {
 $dialog=$null
 for($attempt=0;$attempt -lt 30;$attempt++) {
  foreach($title in @('另存为','打开','Save As','Open')) { $dialog=Find-Name $title; if($dialog) { break } }
  if($dialog) { break }; Start-Sleep -Milliseconds 200
 }
 if(-not $dialog -or $dialog.Current.NativeWindowHandle -eq 0) { throw 'Owned system file picker missing.' }
 return $dialog
}
function Picker($path,[bool]$cancel=$false) {
 $dialog=Wait-Picker
 if($cancel) { Activate (Find-Id $dialog '2'); Start-Sleep -Milliseconds 400; return }
 $fileNameHost=Find-Id $dialog 'FileNameControlHost'
 if(-not $fileNameHost) { $fileNameHost=Find-Id $dialog '1148' }
 if(-not $fileNameHost) {
  $dialog.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition) | ForEach-Object { '{0} | {1} | {2} | {3}' -f $_.Current.ControlType.ProgrammaticName,$_.Current.AutomationId,$_.Current.Name,$_.Current.NativeWindowHandle } | Set-Content -LiteralPath (Join-Path $OutputRoot 'picker-debug.txt') -Encoding UTF8
  throw 'File-name host missing.'
 }
 if($fileNameHost.Current.NativeWindowHandle -eq 0) { throw 'Native file-name host missing.' }
 if(-not [DesktopQaNative]::SetFileName([IntPtr]$fileNameHost.Current.NativeWindowHandle,$path)) { throw 'Could not set and verify native file-name edit.' }
 [DesktopQaNative]::SetForegroundWindow([IntPtr]$dialog.Current.NativeWindowHandle) | Out-Null
 Activate (Find-Id $dialog '1')
 Start-Sleep -Seconds 2
}
function Screenshot($name) {
 if(-not [DesktopQaNative]::ForegroundQaWindow($script:qaProcess.MainWindowHandle)) { throw 'QA screenshot window does not own foreground; refusing unrelated desktop capture.' }
 Start-Sleep -Milliseconds 200
 $rect=New-Object DesktopQaNative+Rect
 [DesktopQaNative]::GetWindowRect($script:qaProcess.MainWindowHandle,[ref]$rect) | Out-Null
 $bitmap=New-Object Drawing.Bitmap(($rect.Right-$rect.Left),($rect.Bottom-$rect.Top))
 $graphics=[Drawing.Graphics]::FromImage($bitmap)
 try {
  $graphics.CopyFromScreen($rect.Left,$rect.Top,0,0,$bitmap.Size)
  $bitmap.Save((Join-Path $OutputRoot $name),[Drawing.Imaging.ImageFormat]::Png)
  if($name -ne 'failure.png') {
   $reference=$bitmap.GetPixel([int]($bitmap.Width/2),[int]($bitmap.Height/2)); $varied=0
   for($x=15;$x -lt 90;$x+=2){for($y=15;$y -lt 90;$y+=2){
    $pixel=$bitmap.GetPixel([int]($bitmap.Width*$x/100),[int]($bitmap.Height*$y/100))
    if([Math]::Abs([int]$pixel.R-$reference.R)+[Math]::Abs([int]$pixel.G-$reference.G)+[Math]::Abs([int]$pixel.B-$reference.B) -gt 60){$varied++}
   }}
   if($varied -lt 5){throw "QA screenshot body did not render: $name"}
  }
 }
 finally { $graphics.Dispose(); $bitmap.Dispose() }
}
function Begin-UpgradeClient($name) {
 $clientProcess=Start-Process -FilePath $ProbeExecutable -ArgumentList @('upgrade-client',('"' + $data + '"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $OutputRoot "$name.txt")
 $null=$clientProcess.Handle # Keep the handle so Windows PowerShell can read the exit code after fast exit.
 return $clientProcess
}
function Enter-FixturePassword($text) {
 $password=$null
 for($attempt=0;$attempt -lt 50 -and -not $password;$attempt++) {
  $password=$script:root.FindAll([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty,'认证密码'))) | Where-Object { $_.Current.IsEnabled -and -not $_.Current.IsOffscreen } | Select-Object -First 1
  if(-not $password) { Start-Sleep -Milliseconds 100 }
 }
 if(-not $password) { throw 'Authentication field missing.' }
 try { Set-Value $password $text }
 catch {
  [DesktopQaNative]::SetForegroundWindow($script:qaProcess.MainWindowHandle) | Out-Null
  $password.SetFocus()
  if([DesktopQaNative]::GetForegroundWindow() -ne $script:qaProcess.MainWindowHandle) { throw 'QA window does not own keyboard focus.' }
  [Windows.Forms.SendKeys]::SendWait($text) # Fixed fixture text only, never user credentials.
 }
 Click '确认'
}
try {
 if([DesktopQaNative]::SetThreadExecutionState([uint32]2147483651) -eq 0){throw 'Cannot keep display awake for desktop capture.'}
 $nestedPortable=Test-Path -LiteralPath (Join-Path $app 'app\SteamWatch.exe')
 if($nestedPortable) {
  $missing=Join-Path $OutputRoot 'missing-payload'
  New-Item -ItemType Directory -Path $missing | Out-Null
  Copy-Item -LiteralPath (Join-Path $app 'SteamWatch.exe') -Destination (Join-Path $missing 'SteamWatch.exe')
  $script:qaProcess=Start-Process -FilePath (Join-Path $missing 'SteamWatch.exe') -WindowStyle Hidden -PassThru
  $null=$script:qaProcess.Handle
  for($attempt=0;$attempt -lt 30;$attempt++) { $script:qaProcess.Refresh(); if($script:qaProcess.MainWindowHandle -ne 0) { break }; Start-Sleep -Milliseconds 200 }
  $script:root=[Windows.Automation.AutomationElement]::FromHandle($script:qaProcess.MainWindowHandle)
  $errorText=($script:root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition) | ForEach-Object { $_.Current.Name }) -join ' '
  if($errorText -notlike '*完整解压*') { throw 'Launcher missing-payload guidance absent.' }
  Screenshot 'launcher-missing-payload.png'
  $script:qaProcess.CloseMainWindow() | Out-Null
  if(-not $script:qaProcess.WaitForExit(3000) -or $script:qaProcess.ExitCode -eq 0) { throw 'Launcher missing-payload failure was not reported.' }
  $checks.Add('portable-launcher-missing-payload-guidance')
 }
 Start-QaApp
 if($nestedPortable) {
  $existingId=$script:qaProcess.Id
  $repeat=Start-Process -FilePath (Join-Path $app 'SteamWatch.exe') -WorkingDirectory $OutputRoot -WindowStyle Hidden -PassThru
  $null=$repeat.Handle
  if(-not $repeat.WaitForExit(5000) -or $repeat.ExitCode -ne 0) { throw 'Repeated launcher failed.' }
  Start-Sleep -Seconds 1
  $instances=@(Get-Process -Name SteamWatch -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $app 'app\SteamWatch.exe') })
  if($instances.Count -ne 1 -or $instances[0].Id -ne $existingId) { throw 'Repeated launcher started a second persistent application.' }
  $checks.Add('portable-launcher-chinese-spaces-foreign-cwd-and-single-instance')
 }
 $dpiScale=[DesktopQaNative]::GetDpiForWindow($script:qaProcess.MainWindowHandle)/96.0
 [DesktopQaNative]::MoveWindow($script:qaProcess.MainWindowHandle,10,10,[int](800*$dpiScale),[int](600*$dpiScale),$true) | Out-Null
 Start-Sleep -Milliseconds 500
 $compactList=Find-Id $script:root 'GamesListView'
 if($compactList.Current.BoundingRectangle.Height -lt 60 -or (Find-Id $script:root 'GameSearchTextBox').Current.BoundingRectangle.Width -lt 100) { throw 'Compact layout hides the game list or search.' }
 Screenshot 'games-compact.png'
 [DesktopQaNative]::MoveWindow($script:qaProcess.MainWindowHandle,10,10,1440,800,$true) | Out-Null
 Start-Sleep -Milliseconds 500
 $checks.Add('native-800x600-dip-layout')
 Screenshot 'games-initial.png'
 $budget=Find-Id $script:root 'DailyBudgetUsage'
 if($budget.Current.Name -ne '1 分钟 · 未设置') { throw 'Global budget includes estimates or invents a configured limit.' }
 Click '设置总限额'
 $null=$script:qaProcess.CloseMainWindow(); Start-Sleep -Milliseconds 400
 if($script:qaProcess.HasExited -or -not (Find-Name '保存规则')) { throw 'Exit interrupted the active rule editor.' }
 $checks.Add('rule-editor-busy-rejects-window-exit')
 Click '取消'
 if(Test-Path -LiteralPath (Join-Path $data 'limits.json')) { throw 'Cancelled global rule persisted.' }
 Click '设置总限额'
 Set-Value (Find-Id $script:root 'InputBox') '120'
 Click '保存规则'; Wait-RuleIdle
 $rules=Get-Content -LiteralPath (Join-Path $data 'limits.json') -Raw | ConvertFrom-Json
 if($rules.Count -ne 1 -or $rules[0].scope -ne 1 -or $rules[0].period -ne 0 -or $rules[0].maxMinutes -ne 120) { throw 'Global daily rule was not saved with the bound target.' }
 if((Find-Id $script:root 'DailyBudgetUsage').Current.Name -ne '1 / 120 分钟') { throw 'Measured budget did not update.' }
 Click '编辑每周限额'
 Set-Value (Find-Id $script:root 'InputBox') '600'
 Click '保存规则'; Wait-RuleIdle
 if((Find-Name '限额规则') -or (Find-Id $script:root 'LibraryTabButton') -or (Find-Id $script:root 'RulesPanel')) { throw 'Removed rules navigation still exists.' }
 $checks.Add('single-library-no-rules-page-or-tabs')
 Screenshot 'library.png'
 Click '设置总限额'
 Click '删除此规则'; Click '取消'
 $rules=Get-Content -LiteralPath (Join-Path $data 'limits.json') -Raw | ConvertFrom-Json
 if($rules.Count -ne 2) { throw 'Cancelled deletion changed rules.' }
 Click '设置总限额'; Click '删除此规则'; Click '删除规则'; Wait-RuleIdle
 $rules=Get-Content -LiteralPath (Join-Path $data 'limits.json') -Raw | ConvertFrom-Json
 if($rules.Count -ne 1 -or $rules[0].period -ne 1) { throw 'Global deletion removed the wrong period.' }
 $checks.Add('global-budgets-measured-crud-and-cancel')
 $gamesList=Find-Id $script:root 'GamesListView'
 $firstGame=$gamesList.FindFirst([Windows.Automation.TreeScope]::Children,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem)))
 Activate $firstGame
 Click '设置限额'; Click '保存规则'; Wait-RuleIdle
 Click '设置限额'
 Select-Option (Find-Name '周期') '每周'
 Set-Value (Find-Id $script:root 'InputBox') '300'
 Click '保存规则'; Wait-RuleIdle
 Click '设置限额'
 Select-Option (Find-Name '周期') '每周'
 if((Find-Id $script:root 'InputBox').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value -ne '300') { throw 'Existing weekly rule was not loaded.' }
 Set-Value (Find-Id $script:root 'InputBox') '350'; Click '保存规则'; Wait-RuleIdle
 $rules=Get-Content -LiteralPath (Join-Path $data 'limits.json') -Raw | ConvertFrom-Json
 if($rules.Count -ne 3 -or @($rules | Where-Object { $_.scope -eq 0 -and $_.period -eq 1 -and $_.maxMinutes -eq 350 }).Count -ne 1) { throw 'Library edit duplicated or missed the existing rule.' }
 Click '设置限额'; Select-Option (Find-Name '周期') '每周'
 Click '删除此规则'; Click '取消'
 $rules=Get-Content -LiteralPath (Join-Path $data 'limits.json') -Raw | ConvertFrom-Json
 if($rules.Count -ne 3) { throw 'Cancelled deletion changed library rules.' }
 Click '设置限额'; Select-Option (Find-Name '周期') '每周'
 Click '删除此规则'; Click '删除规则'; Wait-RuleIdle
 Click '设置限额'; Select-Option (Find-Name '周期') '每周'
 Set-Value (Find-Id $script:root 'InputBox') '300'
 Select-Option (Find-Name '超限策略') '强制退出'
 Click '保存规则'; Wait-RuleIdle
 $checks.Add('library-day-week-existing-rule-edit-delete-cancel-and-recreate')
 $policySummary="每日 60 分钟 · 仅提醒`n每周 300 分钟 · 超限强退"
 if(-not (Find-Name $policySummary)) { throw 'Daily and weekly policies are missing or associated with the wrong period.' }
 Screenshot 'game-rule-policies-wide.png'
 [DesktopQaNative]::MoveWindow($script:qaProcess.MainWindowHandle,10,10,[int](800*$dpiScale),[int](600*$dpiScale),$true) | Out-Null
 Start-Sleep -Milliseconds 500
 $policyText=Find-Name $policySummary
 $listBounds=(Find-Id $script:root 'GamesListView').Current.BoundingRectangle
 if(-not $policyText -or $policyText.Current.IsOffscreen -or $policyText.Current.BoundingRectangle.Right -gt $listBounds.Right+2 -or $policyText.Current.BoundingRectangle.Height -lt 44*$dpiScale) { throw 'Compact policy summary is clipped or loses its daily/weekly rows.' }
 Screenshot 'game-rule-policies-compact.png'
 $checks.Add('library-day-week-policies-mixed-modes-readable-wide-and-compact')
 [DesktopQaNative]::MoveWindow($script:qaProcess.MainWindowHandle,10,10,1440,800,$true) | Out-Null
 Start-Sleep -Milliseconds 500
 Pointer-GameRow $firstGame $false $true
 if((Find-Id $script:root 'InputBox').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value -ne '60') { throw 'Double click did not load the game daily limit.' }
 Screenshot 'game-double-click-limit.png'; Click '取消'
 $checks.Add('game-row-real-double-click-opens-limit-editor')
 $gameItems=$gamesList.FindAll([Windows.Automation.TreeScope]::Children,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem)))
 $contextGame=$gameItems[1]
 $contextName=$contextGame.FindFirst([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Text))).Current.Name
 Pointer-GameRow $contextGame $true
 Screenshot 'game-context-menu.png'; Click-GameMenu '强退策略'
 if(-not (Find-Name ($contextName+' · 限额设置'))) { throw 'Context action targeted the previously selected game.' }
 $strategy=(Find-Name '超限策略').GetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()
 if($strategy[0].Current.Name -ne '强制退出') { throw 'Force-close context action did not select the policy.' }
 Click '取消'; $checks.Add('right-click-context-target-and-force-policy')
 Pointer-GameRow $contextGame $true; Click-GameMenu '游戏详情'
 $detailWindows=$script:root.FindAll([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Window)))
 $details=$detailWindows | Where-Object { $_.Current.Name -eq $contextName } | Select-Object -First 1
 if(-not $details) { throw 'Game details did not use the context game.' }
 $detailTexts=$details.FindAll([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Text)))
 $appIdLine=$detailTexts | Where-Object { $_.Current.Name -like 'AppID *' } | Select-Object -First 1
 $categoryLine=$detailTexts | Where-Object { $_.Current.Name -eq '分类未知' } | Select-Object -First 1
 if(-not $appIdLine -or -not $categoryLine -or ($categoryLine.Current.BoundingRectangle.Top-$appIdLine.Current.BoundingRectangle.Top) -lt (28*$dpiScale)) { throw 'Game detail rows are crowded.' }
 Screenshot 'game-details-spaced.png'; Click '关闭'; $checks.Add('context-game-details-readable-row-spacing')
 Pointer-GameRow $contextGame $true; Click-GameMenu '设置选中项分类'
 $null=$script:qaProcess.CloseMainWindow(); Start-Sleep -Milliseconds 350
 if($script:qaProcess.HasExited -or -not (Find-Name '保存分类')) { throw 'Category editor did not reject window exit.' }
 $checks.Add('category-command-rejects-window-exit')
 Select-Option (Find-Name '分类依据：用户明确设置') '运行库／工具'
 Click '保存分类'
 $categories=Get-Content -LiteralPath (Join-Path $data 'game-categories.json') -Raw | ConvertFrom-Json
 if(@($categories.PSObject.Properties | Where-Object { $_.Value -eq 2 }).Count -ne 1) { throw 'User category did not persist.' }
 $checks.Add('user-category-ui-and-persistence')
 $categoryFilter=Find-Id $script:root 'CategoryFilterComboBox'
 Select-Option $categoryFilter '运行库／工具'
 $listItems=$gamesList.FindAll([Windows.Automation.TreeScope]::Children,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem)))
 if($listItems.Count -ne 1) { throw 'Category filter did not isolate the tagged item.' }
 Set-Value (Find-Id $script:root 'GameSearchTextBox') 'no-matching-fixture-7f24a8'
 Start-Sleep -Milliseconds 350
 $listItems=$gamesList.FindAll([Windows.Automation.TreeScope]::Children,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem)))
 if($listItems.Count -ne 0) { throw 'Category/search filters did not compose.' }
 Set-Value (Find-Id $script:root 'GameSearchTextBox') ''
 Select-Option $categoryFilter '全部类别'
 $checks.Add('category-and-search-filters-compose')
 $gameList=Find-Id $script:root 'GamesListView'
 $gameScroll=$gameList.GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern)
 if(-not $gameScroll.Current.VerticallyScrollable) { throw 'Fixture requires enough games to check scroll retention.' }
 $gameScroll.SetScrollPercent([Windows.Automation.ScrollPattern]::NoScroll,100)
 Start-Sleep -Milliseconds 300
 $visibleRows=@($gameList.FindAll([Windows.Automation.TreeScope]::Children,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem))) | Where-Object { -not $_.Current.IsOffscreen })
 $lastRow=$visibleRows[-1]; Activate $lastRow
 $selectedName=$lastRow.Current.Name
 Click '刷新'
 $selectedRows=$gameList.GetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()
 if($selectedRows.Count -ne 1 -or $selectedRows[0].Current.Name -ne $selectedName -or $gameScroll.Current.VerticalScrollPercent -lt 98) { throw 'Game refresh lost selection or scroll position.' }
 Screenshot 'games-refresh-retained-selection.png'
 $checks.Add('library-refresh-retains-selection-and-scroll')
 Set-Value (Find-Id $script:root 'GameSearchTextBox') '1188930'
 Start-Sleep -Milliseconds 300; Click '刷新'
 if((Find-Id $script:root 'GameSearchTextBox').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value -ne '1188930') { throw 'Game refresh reset search.' }
 $filteredRows=$gameList.FindAll([Windows.Automation.TreeScope]::Children,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem)))
 if($filteredRows.Count -ne 1) { throw 'Game refresh lost search filtering.' }
 $checks.Add('library-refresh-retains-search-filter')
 Set-Value (Find-Id $script:root 'GameSearchTextBox') ''; Start-Sleep -Milliseconds 300
 $gameScroll.SetScrollPercent([Windows.Automation.ScrollPattern]::NoScroll,0)
 Screenshot 'games.png'
 Click '统计'
 $dailyDate=(Get-Date).Date.AddDays(-1).ToString('yyyy-MM-dd')
 $weeklyDate=(Get-Date).Date.AddDays(-8).ToString('yyyy-MM-dd')
 Shift-CalendarDate 'DailyStatsDatePicker' -1
 Click '每周汇总'
 Shift-CalendarDate 'WeeklyStatsDatePicker' -8
 Click '游戏'; Click '刷新'; Click '统计'
 if((Find-Name '每周汇总').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [Windows.Automation.ToggleState]::On) { throw 'Statistics period was reset on navigation.' }
 if((Find-Id $script:root 'WeeklyStatsDatePicker').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value -notlike "*$weeklyDate*") { throw 'Weekly date was reset on refresh.' }
 Click '每日明细'
 if((Find-Id $script:root 'DailyStatsTotalText').Current.Name -notlike "$dailyDate 总时长 0 分钟*") { throw 'Daily date was reset or shared with weekly selection.' }
 Screenshot 'statistics-retained-daily-date.png'
 Shift-CalendarDate 'DailyStatsDatePicker' 1
 Click '每周汇总'
 Shift-CalendarDate 'WeeklyStatsDatePicker' 8
 Click '每日明细'
 $checks.Add('statistics-independent-dates-period-survive-navigation-and-refresh')
 Click '检查／修正估算'
 $entryName=Find-Id $script:root 'EstimateEntryName'
 $entrySummary=Find-Id $script:root 'EstimateEntrySummary'
 $entryExplanation=Find-Id $script:root 'EstimateEntryExplanation'
 if(-not $entryName -or -not $entrySummary -or -not $entryExplanation -or ($entrySummary.Current.BoundingRectangle.Top-$entryName.Current.BoundingRectangle.Bottom) -lt (10*$dpiScale) -or ($entryExplanation.Current.BoundingRectangle.Top-$entrySummary.Current.BoundingRectangle.Bottom) -lt (10*$dpiScale)) { throw 'Estimate detail paragraphs are crowded.' }
 $checks.Add('estimate-detail-readable-paragraph-spacing')
 Screenshot 'estimate-history.png'
 Click '关闭'; Assert-Estimate 15
 $checks.Add('estimate-dialog-cancel-preserves-data')
 Correct-Estimate 5
 if(@(Get-ChildItem -LiteralPath $data -Filter 'workspace-before-correction-*.json').Count -ne 1) { throw 'Correction backup missing.' }
 $checks.Add('estimate-correction-and-statistics')
 $checks.Add('shared-dialog-confirmation-paragraph-spacing')
 Click '每周汇总'
 if ((Find-Name '每周汇总').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [Windows.Automation.ToggleState]::On) { throw 'Weekly tab is not selected' }
 if ((Find-Name '每日明细').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [Windows.Automation.ToggleState]::Off) { throw 'Daily tab remains selected' }
 if((Find-Id $script:root 'WeeklyStatsBreakdown').Current.Name -notlike '实测 1 分钟 · 估算 5 分钟*') { throw 'Weekly measured/estimated totals are inconsistent.' }
 Screenshot 'statistics-weekly.png'
 Click '查看每日数值'
 $daySummary=(Get-Date -Format 'yyyy-MM-dd') + '：实测 1 分钟，估算 5 分钟，合计 6 分钟'
 if(-not (Find-Name $daySummary)) { throw 'Accessible daily table does not match corrected statistics.' }
 $dayRows=@($script:root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition) | Where-Object { $_.Current.AutomationId -like 'DailyValue-*' } | Sort-Object { $_.Current.BoundingRectangle.Top })
 if($dayRows.Count -ne 7) { throw 'Daily values did not expose all seven readable rows.' }
 for($i=1;$i -lt $dayRows.Count;$i++) {
  if(($dayRows[$i].Current.BoundingRectangle.Top-$dayRows[$i-1].Current.BoundingRectangle.Bottom) -lt (10*$dpiScale)) { throw 'Daily value rows are crowded.' }
 }
 $checks.Add('weekly-daily-values-readable-row-spacing')
 for($column=1;$column -le 3;$column++) {
  $header=Find-Id $script:root "DailyValueHeader-$column"
  if(-not $header) { throw 'Daily value column header missing.' }
  $cells=@($script:root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition) | Where-Object { $_.Current.AutomationId -like "DailyValueCell-*-$column" })
  if($cells.Count -ne 7) { throw 'Daily value numeric column is incomplete.' }
  foreach($cell in $cells) {
   if([Math]::Abs($header.Current.BoundingRectangle.Right-$cell.Current.BoundingRectangle.Right) -gt (2*$dpiScale)) { throw 'Daily values do not align with their column headers.' }
  }
 }
 $checks.Add('weekly-daily-values-aligned-header-and-numeric-columns')
 Screenshot 'statistics-weekly-values.png'
 Click '关闭'
 [DesktopQaNative]::MoveWindow($script:qaProcess.MainWindowHandle,10,10,[int](800*$dpiScale),[int](600*$dpiScale),$true) | Out-Null
 Start-Sleep -Milliseconds 500
 if((Find-Id $script:root 'WeeklyStatsList').Current.BoundingRectangle.Height -lt 45) { throw 'Compact weekly statistics hides the game table.' }
 Screenshot 'statistics-weekly-compact.png'
 [DesktopQaNative]::MoveWindow($script:qaProcess.MainWindowHandle,10,10,1440,800,$true) | Out-Null
 Start-Sleep -Milliseconds 500
 Click '每日明细'
 if ((Find-Name '每日明细').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [Windows.Automation.ToggleState]::On) { throw 'Daily tab is not selected' }
 if ((Find-Name '每周汇总').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [Windows.Automation.ToggleState]::Off) { throw 'Weekly tab remains selected' }
 if((Find-Id $script:root 'DailyStatsBreakdown').Current.Name -notlike '实测 1 分钟 · 估算 5 分钟*') { throw 'Daily measured/estimated totals are inconsistent.' }
 Screenshot 'statistics-daily.png'
 $checks.Add('statistics-day-week-chart-and-accessible-values')
 Click '设置'; Click '提醒和强退'
 Set-SettingsNumber 'ForceCloseCountdownNumberBox' '90'
 Click '返回设置'; Screenshot 'settings-unsaved.png'; Click '继续编辑'
 if((SettingsNumber 'ForceCloseCountdownNumberBox') -ne '90') { throw 'Continue editing lost reminder draft.' }
 Click '统计'; Click '继续编辑'
 if((Find-Id $script:root 'ForceCloseCountdownNumberBox').Current.IsOffscreen) { throw 'Cancelled navigation left settings.' }
 Click '返回设置'; Click '放弃修改'
 Click '常规'
 Activate (Find-Id $script:root 'PlayReminderSoundsSwitch'); Start-Sleep -Milliseconds 350; Wait-SettingsIdle
 $savedSettings=Get-Content -LiteralPath (Join-Path $data 'settings.json') -Raw | ConvertFrom-Json
 if(-not $savedSettings.playReminderSounds -or $savedSettings.forceCloseCountdownSeconds -ne 60) { throw 'General save changed reminder settings.' }
 Screenshot 'settings-general.png'
 Click '返回设置'; Click '提醒和强退'
 Set-SettingsNumber 'ForceCloseCountdownNumberBox' '120'
 Click '返回设置'; Click '保存并离开'; Wait-SettingsIdle
 $savedSettings=Get-Content -LiteralPath (Join-Path $data 'settings.json') -Raw | ConvertFrom-Json
 if($savedSettings.forceCloseCountdownSeconds -ne 120 -or -not $savedSettings.playReminderSounds) { throw 'Reminder save lost general settings.' }
 $checks.Add('settings-navigation-continue-discard-save-and-section-isolation')
 Click '提醒和强退'
 Set-SettingsNumber 'FirstReminderThresholdNumberBox' '90'
 Click '保存设置'; Wait-SettingsIdle
 if((SettingsNumber 'FirstReminderThresholdNumberBox') -ne '90' -or -not (Find-Name '有未保存的修改 · 保存后生效')) { throw 'Invalid reminder draft was lost.' }
 $savedSettings=Get-Content -LiteralPath (Join-Path $data 'settings.json') -Raw | ConvertFrom-Json
 if($savedSettings.firstReminderThresholdPercent -ne 70) { throw 'Invalid reminder settings persisted.' }
 Click '放弃本次修改'
 Set-SettingsNumber 'ForceCloseCountdownNumberBox' '150'
 $settingsLock=[IO.File]::Open((Join-Path $data 'settings.json'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
 try {
  Click '保存设置'; Wait-SettingsIdle
  if((SettingsNumber 'ForceCloseCountdownNumberBox') -ne '150') { throw 'Failed settings save lost draft.' }
  $feedback=(Find-Id (Find-Id $script:root 'SettingsInfoBar') 'Title').Current.Name
  if($feedback -notlike '*保存设置失败*') { throw 'Locked-file save did not report failure.' }
  Screenshot 'settings-save-failure.png'
 } finally { $settingsLock.Dispose() }
 $savedSettings=Get-Content -LiteralPath (Join-Path $data 'settings.json') -Raw | ConvertFrom-Json
 if($savedSettings.forceCloseCountdownSeconds -ne 120) { throw 'Failed save changed persisted settings.' }
 Click '保存设置'; Wait-SettingsIdle
 $savedSettings=Get-Content -LiteralPath (Join-Path $data 'settings.json') -Raw | ConvertFrom-Json
 if($savedSettings.forceCloseCountdownSeconds -ne 150) { throw 'Retry did not save retained draft.' }
 Assert-SettingsBounds
 Screenshot 'settings-reminder.png'
 $checks.Add('settings-validation-and-save-failure-retains-draft-retry')
 Click '返回设置'; Click '认证保护'
 Activate (Find-Id $script:root 'RequireAuthenticationSwitch')
 Set-Value (Find-Id $script:root 'AuthenticationPasswordBox') 'draft-fixture-password'
 Set-Value (Find-Id $script:root 'ConfirmAuthenticationPasswordBox') 'draft-fixture-password'
 Click '统计'; Click '继续编辑'
 if((Find-Id $script:root 'RequireAuthenticationSwitch').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [Windows.Automation.ToggleState]::On) { throw 'Authentication draft was lost.' }
 Click '返回设置'; Click '放弃修改'
 $savedSettings=Get-Content -LiteralPath (Join-Path $data 'settings.json') -Raw | ConvertFrom-Json
 if($savedSettings.requireAuthenticationForSensitiveActions -or $savedSettings.authenticationCredential) { throw 'Discarded authentication draft persisted.' }
 $checks.Add('settings-authentication-draft-discard-without-persisting')
 Click '认证保护'
 Activate (Find-Id $script:root 'RequireAuthenticationSwitch')
 Set-Value (Find-Id $script:root 'AuthenticationPasswordBox') 'fixture-password'
 Set-Value (Find-Id $script:root 'ConfirmAuthenticationPasswordBox') 'fixture-password'
 Click '保存设置'; Wait-SettingsIdle
 $savedSettings=Get-Content -LiteralPath (Join-Path $data 'settings.json') -Raw | ConvertFrom-Json
 if(-not $savedSettings.requireAuthenticationForSensitiveActions -or -not $savedSettings.authenticationCredential -or $savedSettings.forceCloseCountdownSeconds -ne 150 -or -not $savedSettings.playReminderSounds) { throw 'Authentication save changed other sections or failed to create credential.' }
 Activate (Find-Id $script:root 'RequireAuthenticationSwitch')
 Click '保存设置'; Enter-FixturePassword 'fixture-password'; Wait-SettingsIdle
 $savedSettings=Get-Content -LiteralPath (Join-Path $data 'settings.json') -Raw | ConvertFrom-Json
 if($savedSettings.requireAuthenticationForSensitiveActions -or $savedSettings.authenticationCredential) { throw 'Authenticated protection disable did not persist.' }
 Screenshot 'settings-authentication.png'
 Click '返回设置'
 $checks.Add('settings-authentication-enable-and-authenticated-disable')
 [DesktopQaNative]::MoveWindow($script:qaProcess.MainWindowHandle,10,10,[int](800*$dpiScale),[int](600*$dpiScale),$true) | Out-Null
 Start-Sleep -Milliseconds 500
 Screenshot 'settings-root-compact.png'
 Click '提醒和强退'
 Assert-SettingsBounds
 Screenshot 'settings-reminder-compact.png'
 Click '返回设置'; Click '认证保护'
 Screenshot 'settings-authentication-compact.png'
 Click '返回设置'; Click '帮助与关于'
 if((Find-Id $script:root 'HelpView').Current.IsOffscreen) { throw 'Help page is not accessible.' }
 if(Find-Name '启动程序') { throw 'Redundant startup guide is still displayed.' }
 $guideParagraph=Find-Name '安装版数据位于 %LOCALAPPDATA%\SteamWatch；绿色版数据位于解压根目录 data。搬迁绿色版时复制整个目录，升级前先退出并备份。'
 if(-not $guideParagraph -or $guideParagraph.Current.IsOffscreen) { throw 'Help guide body should be directly readable without expanding cards.' }
 $checks.Add('help-guide-direct-reading-and-no-startup-section')
 Screenshot 'help-compact.png'
 Click '提醒设置'
 $countdown=Find-Id $script:root 'ForceCloseCountdownNumberBox'
 if(-not $countdown -or $countdown.Current.IsOffscreen) { throw 'Help reminder shortcut failed.' }
 Click '返回设置'; Click '帮助与关于'; Click '认证保护'
 $authSwitch=Find-Id $script:root 'RequireAuthenticationSwitch'
 if(-not $authSwitch -or $authSwitch.Current.IsOffscreen) { throw 'Help authentication shortcut failed.' }
 if((Find-Id $script:root 'AuthenticationPasswordBox').Current.IsEnabled) { throw 'Password editor is enabled while protection is off.' }
 Click '返回设置'; Click '帮助与关于'; Click '备份和诊断'
 $export=Find-Name '导出数据备份'
 if(-not $export -or $export.Current.IsOffscreen) { throw 'Help maintenance shortcut failed.' }
 Screenshot 'settings-maintenance-compact.png'
 $settingsScroll=(Find-Id $script:root 'SettingsView').GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern)
 $settingsScroll.SetScrollPercent([Windows.Automation.ScrollPattern]::NoScroll,100)
 Start-Sleep -Milliseconds 300
 $hint=Find-Id $script:root 'PortableRemovalHint'
 $uninstall=Find-Id $script:root 'UninstallButton'
 if(-not $hint -or $hint.Current.IsOffscreen -or ($uninstall -and -not $uninstall.Current.IsOffscreen)) { throw 'Portable removal guidance is absent after scrolling.' }
 Screenshot 'settings-removal-compact.png'
 $settingsScroll.SetScrollPercent([Windows.Automation.ScrollPattern]::NoScroll,0)
 Click '帮助与关于'
 $helpScroll=(Find-Id $script:root 'HelpView').GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern)
 $helpScroll.SetScrollPercent([Windows.Automation.ScrollPattern]::NoScroll,100)
 Start-Sleep -Milliseconds 300
 if((Find-Id $script:root 'VersionText').Current.Name -notlike '版本 *' -or (Find-Id $script:root 'ModeText').Current.Name -notlike '绿色版*' -or (Find-Id $script:root 'ModeText').Current.IsOffscreen) { throw 'Help about information is absent or inaccessible.' }
 Screenshot 'help-about-compact.png'
 $helpScroll.SetScrollPercent([Windows.Automation.ScrollPattern]::NoScroll,0)
 $checks.Add('help-settings-shortcuts-about-and-portable-removal-guidance')
 [DesktopQaNative]::MoveWindow($script:qaProcess.MainWindowHandle,10,10,1440,800,$true) | Out-Null
 Start-Sleep -Milliseconds 500
 $checks.Add('settings-and-help-800x600-dip-layout')
 Maintenance
 Click '导出数据备份'
 # Picker creation is asynchronous; exercise exit only after the tested state exists.
 $null=Wait-Picker
 $null=$script:qaProcess.CloseMainWindow(); Start-Sleep -Milliseconds 350
 if($script:qaProcess.HasExited -or -not ((Find-Name '另存为') -or (Find-Name 'Save As'))) { throw 'Backup picker command did not retain the app and picker during exit.' }
 $checks.Add('backup-picker-command-rejects-window-exit')
 Picker '' $true
 $checks.Add('backup-picker-cancel')
 $backup=Join-Path $OutputRoot 'backup.json'
 Click '导出数据备份'; Picker $backup
 if(-not (Test-Path -LiteralPath $backup)) { throw 'Backup file missing.' }
 $checks.Add('backup-system-picker-save')
 Correct-Estimate 2
 Maintenance
 $invalid=Join-Path $OutputRoot 'invalid.json'
 Set-Content -LiteralPath $invalid -Value '{}' -Encoding UTF8
 Click '恢复数据备份'; Picker $invalid; Click '恢复'; Assert-Estimate 2
 $checks.Add('invalid-backup-refused-without-data-change')
 Click '恢复数据备份'; Picker $backup; Click '恢复'; Start-Sleep -Milliseconds 500; Assert-Estimate 5
 $checks.Add('backup-system-picker-restore')
 $diagnostic=Join-Path $OutputRoot 'diagnostics.zip'
 Click '导出诊断包'; Picker $diagnostic
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $zip=[IO.Compression.ZipFile]::OpenRead($diagnostic)
 try {
  if($zip.Entries.Count -ne 2) { throw 'Unexpected diagnostic files.' }
  foreach($entry in $zip.Entries) {
   $reader=New-Object IO.StreamReader($entry.Open())
   try { $text=$reader.ReadToEnd(); if($text.Contains('历史测试记录') -or $text.Contains('桌面验收修正') -or $text.Contains($OutputRoot)) { throw 'Private fixture fields leaked.' } }
   finally { $reader.Dispose() }
  }
 } finally { $zip.Dispose() }
 $checks.Add('diagnostics-system-picker-save-and-content-check')
 Screenshot 'maintenance.png'
 Click '返回设置'; Click '常规'
 Select-CloseAction '最小化到托盘' 0
 Click '返回设置'; Click '提醒和强退'
 Set-SettingsNumber 'ForceCloseCountdownNumberBox' '171'
 if((Get-Content -LiteralPath (Join-Path $data 'settings.json') -Raw | ConvertFrom-Json).closeWindowAction -ne 0) { throw 'Navigation reverted close action.' }
 $qaWindowHandle=$script:qaProcess.MainWindowHandle
 $null=$script:qaProcess.CloseMainWindow()
 for($attempt=0;$attempt -lt 50 -and [DesktopQaNative]::IsWindowVisible($qaWindowHandle);$attempt++) { Start-Sleep -Milliseconds 100 }
 if([DesktopQaNative]::IsWindowVisible($qaWindowHandle)) { throw 'Close-to-tray did not hide the window.' }
 if($script:qaProcess.HasExited) { throw 'Close-to-tray exited with a settings draft.' }
 $null=[DesktopQaNative]::ForegroundQaWindow($script:qaProcess.MainWindowHandle)
 if((SettingsNumber 'ForceCloseCountdownNumberBox') -ne '171') { throw 'Close-to-tray lost draft.' }
 $checks.Add('close-to-tray-preserves-settings-draft')
 Click '返回设置'; Click '放弃修改'; Click '常规'
 Select-CloseAction '退出应用' 1
 Click '返回设置'; Click '提醒和强退'
 Set-SettingsNumber 'ForceCloseCountdownNumberBox' '171'
 $null=$script:qaProcess.CloseMainWindow(); Start-Sleep -Milliseconds 400
 Screenshot 'exit-settings-draft.png'; Click '继续编辑'
 if($script:qaProcess.HasExited -or (SettingsNumber 'ForceCloseCountdownNumberBox') -ne '171') { throw 'Window exit cancellation lost draft.' }
 $checks.Add('window-exit-continue-editing-preserves-draft')
 Click '刷新'
 if((SettingsNumber 'ForceCloseCountdownNumberBox') -ne '171') { throw 'Refresh after cancelled exit lost draft.' }
 Click '暂停限制'
 if(-not (Find-Name '恢复限制')) { throw 'Cancelled exit did not resume pause commands.' }
 Click '恢复限制'
 if(-not (Find-Name '暂停限制') -or (SettingsNumber 'ForceCloseCountdownNumberBox') -ne '171') { throw 'Resume command or settings draft failed after cancelled exit.' }
 $checks.Add('cancelled-exit-resumes-refresh-and-pause-with-draft')
 $client=Begin-UpgradeClient 'upgrade-draft-cancel'
 Start-Sleep -Milliseconds 600; Click '继续编辑'
 if(-not $client.WaitForExit(10000) -or $client.ExitCode -ne 2 -or $script:qaProcess.HasExited) { throw 'Draft cancellation did not decline upgrade.' }
 $checks.Add('upgrade-draft-continue-editing-declines-without-exit')
 $settingsLock=[IO.File]::Open((Join-Path $data 'settings.json'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
 try {
  $client=Begin-UpgradeClient 'upgrade-draft-save-failed'
  Start-Sleep -Milliseconds 600; Click '保存并退出'
  if(-not $client.WaitForExit(10000) -or $client.ExitCode -ne 2 -or $script:qaProcess.HasExited) { throw 'Failed draft save allowed upgrade exit.' }
  Wait-SettingsIdle
  if((SettingsNumber 'ForceCloseCountdownNumberBox') -ne '171') { throw 'Failed exit save lost draft.' }
 } finally { $settingsLock.Dispose() }
 $checks.Add('upgrade-draft-save-failure-retains-app-and-draft')
 # Refresh marks catalog data dirty before its save. Lock the real ledger to exercise
 # native shutdown failure without starting or terminating any actual game.
 Click '返回设置'; Click '放弃修改'
 Click '游戏'
 $ledgerPath=Join-Path $data 'playtime-ledger.json'
 $ledgerStamp=(Get-Item -LiteralPath $ledgerPath).LastWriteTimeUtc.Ticks
 $ledgerLock=[IO.File]::Open($ledgerPath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
 try {
  Click '刷新'
  for($attempt=0;$attempt -lt 100;$attempt++) {
   if((Find-Id $script:root 'RefreshButton').Current.IsEnabled) { break }
   Start-Sleep -Milliseconds 100
  }
  if((Find-Id $script:root 'StatusFeedback').Current.Name -notmatch '失败') { throw 'Locked ledger did not fail refresh.' }
  $null=$script:qaProcess.CloseMainWindow()
  for($attempt=0;$attempt -lt 100 -and -not (Find-Name '退出前保存失败');$attempt++) { Start-Sleep -Milliseconds 100 }
  if($script:qaProcess.HasExited -or -not (Find-Name '退出前保存失败')) { throw 'Dirty ledger save failure did not retain native app.' }
  Screenshot 'exit-ledger-save-failed.png'
 } finally { $ledgerLock.Dispose() }
 Click '知道了'
 for($attempt=0;$attempt -lt 200;$attempt++) {
  if((Get-Item -LiteralPath $ledgerPath).LastWriteTimeUtc.Ticks -ne $ledgerStamp) { break }
  Start-Sleep -Milliseconds 100
 }
 if((Get-Item -LiteralPath $ledgerPath).LastWriteTimeUtc.Ticks -eq $ledgerStamp) { throw 'Background retry did not persist dirty ledger after failed exit.' }
 Assert-Estimate 5
 $checks.Add('native-dirty-catalog-ledger-exit-failure-and-background-save-recovery')
 Click '设置'; Click '提醒和强退'
 Set-SettingsNumber 'ForceCloseCountdownNumberBox' '171'
 $client=Begin-UpgradeClient 'upgrade-no-auth'
 Start-Sleep -Milliseconds 600; Click '保存并退出'
 if(-not $client.WaitForExit(15000) -or $client.ExitCode -ne 0) { throw 'Upgrade preparation failed.' }
 if(-not $script:qaProcess.WaitForExit(15000) -or $script:qaProcess.ExitCode -ne 0) { throw 'Upgrade did not cause normal exit.' }
 if((Get-Content -LiteralPath (Join-Path $data 'settings.json') -Raw | ConvertFrom-Json).forceCloseCountdownSeconds -ne 171) { throw 'Exit did not save draft.' }
 $checks.Add('upgrade-preparation-and-real-normal-exit')
 $checks.Add('upgrade-draft-save-and-real-normal-exit')
 if($nestedPortable) {
  $moved=Join-Path $OutputRoot '搬迁后的 便携版'
  $workspacePrefix=$OutputRoot.TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
  foreach($candidate in @($app,$moved)) {
   if(-not [IO.Path]::GetFullPath($candidate).StartsWith($workspacePrefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'QA relocation escapes its isolated workspace.' }
  }
  Move-Item -LiteralPath $app -Destination $moved
  $app=$moved; $data=Join-Path $app 'data'
 }
 $existingRules=Get-Content -LiteralPath (Join-Path $data 'limits.json') -Raw | ConvertFrom-Json
 $existingRules += [pscustomobject]@{scope=0;period=0;maxMinutes=45;enforcement=0;appId=2147480000;name='已移除库的测试游戏';isEnabled=$true}
 $existingRules | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $data 'limits.json') -Encoding UTF8
 $salt=[byte[]](1..16)
 $derive=[Security.Cryptography.Rfc2898DeriveBytes]::new('fixture-password',$salt,100000,[Security.Cryptography.HashAlgorithmName]::SHA256)
 try { $hash=$derive.GetBytes(32) } finally { $derive.Dispose() }
 $settings.requireAuthenticationForSensitiveActions=$true
 $settings.authenticationCredential=@{saltBase64=[Convert]::ToBase64String($salt);hashBase64=[Convert]::ToBase64String($hash);iterations=100000;keySize=32}
 $settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $data 'settings.json') -Encoding UTF8
 Start-QaApp
 if($nestedPortable) { Assert-Estimate 5; if(Test-Path -LiteralPath (Join-Path $app 'app\data')) { throw 'Nested payload wrote data beside dependencies.' }; $checks.Add('portable-relocation-retains-root-data-and-upgrade-pipe') }
 Set-Value (Find-Id $script:root 'GameSearchTextBox') '已移除库的测试游戏'
 $orphan=$null
 for($attempt=0;$attempt -lt 20 -and -not $orphan;$attempt++) {
  Start-Sleep -Milliseconds 150
  $orphanList=Find-Id $script:root 'GamesListView'
  $orphan=$orphanList.FindFirst([Windows.Automation.TreeScope]::Children,(New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem)))
 }
 if(-not $orphan) { throw 'Rule-only game became unreachable after removing the rule page.' }
 Activate $orphan; Click '设置限额'
 if((Find-Id $script:root 'InputBox').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value -ne '45' -or -not (Find-Name '删除此规则')) { throw 'Rule-only game editor did not retain its rule/delete entry.' }
 Click '取消'; Set-Value (Find-Id $script:root 'GameSearchTextBox') ''
 $checks.Add('rule-only-game-remains-searchable-and-editable')

 Click '设置'; Click '提醒和强退'
 Set-SettingsNumber 'ForceCloseCountdownNumberBox' '180'
 Click '保存设置'; Click '取消'; Wait-SettingsIdle
 if((SettingsNumber 'ForceCloseCountdownNumberBox') -ne '180') { throw 'Authentication cancellation lost settings draft.' }
 Click '返回设置'; Click '保存并离开'; Click '取消'; Wait-SettingsIdle
 if((Find-Id $script:root 'ForceCloseCountdownNumberBox').Current.IsOffscreen) { throw 'Cancelled authentication navigated away.' }
 Click '返回设置'; Click '放弃修改'
 Click '常规'
 Activate (Find-Id $script:root 'PlayReminderSoundsSwitch'); Start-Sleep -Milliseconds 350
 Click '取消'; Wait-SettingsIdle
 if((Find-Id $script:root 'PlayReminderSoundsSwitch').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [Windows.Automation.ToggleState]::Off) { throw 'Cancelled general setting was not reverted.' }
 Click '游戏'
 $checks.Add('settings-authentication-cancel-retains-draft-and-reverts-immediate-control')
 $beforeRules=Get-Content -LiteralPath (Join-Path $data 'limits.json') -Raw
 Click '设置总限额'; Click '保存规则'; Click '取消'
 if(-not (Find-Name '认证未通过或已取消，规则未保存。草稿已保留。')) { throw 'Authentication cancellation lost the rule draft.' }
 Click '取消'
 if((Get-Content -LiteralPath (Join-Path $data 'limits.json') -Raw) -ne $beforeRules) { throw 'Cancelled rule authentication changed persisted rules.' }
 $checks.Add('rule-authentication-cancel-retains-draft-and-data')
 Click '暂停限制'; Click '取消'
 if(-not (Find-Name '暂停限制')) { throw 'Cancelled authentication changed pause state.' }
 Click '暂停限制'; Enter-FixturePassword 'wrong-fixture-password'
 if(-not (Find-Name '暂停限制')) { throw 'Wrong password changed pause state.' }
 Click '暂停限制'; Enter-FixturePassword 'fixture-password'
 if(-not (Find-Name '恢复限制')) { throw 'Valid authentication did not pause restrictions.' }
 $checks.Add('pause-authentication-cancel-wrong-and-correct')
 Click '设置'; Click '提醒和强退'
 Set-SettingsNumber 'ForceCloseCountdownNumberBox' '181'
 $client=Begin-UpgradeClient 'upgrade-auth-cancel'
 Start-Sleep -Milliseconds 600; Click '不保存并退出'
 Start-Sleep -Seconds 1
 Click '取消'
 if(-not $client.WaitForExit(10000) -or $client.ExitCode -ne 2 -or $script:qaProcess.HasExited) { throw 'Declined upgrade did not retain app.' }
 if((SettingsNumber 'ForceCloseCountdownNumberBox') -ne '181') { throw 'Discard exit followed by auth cancellation lost draft.' }
 $checks.Add('upgrade-authentication-cancel-retains-app')
 $checks.Add('upgrade-no-save-auth-cancel-preserves-draft')
 $disconnectMarker=Join-Path $OutputRoot 'upgrade-disconnect'
 $disconnect=Start-Process -FilePath $ProbeExecutable -ArgumentList @('upgrade-disconnect',('"'+$data+'"'),('"'+$disconnectMarker+'"')) -WindowStyle Hidden -PassThru
 try {
  for($attempt=0;$attempt -lt 100 -and -not (Test-Path -LiteralPath ($disconnectMarker+'.ready'));$attempt++) { Start-Sleep -Milliseconds 100 }
  if(-not (Test-Path -LiteralPath ($disconnectMarker+'.ready'))) { throw 'Disconnect client did not send upgrade request.' }
  Click '不保存并退出'
  for($attempt=0;$attempt -lt 100 -and -not (Find-Name '认证密码');$attempt++) { Start-Sleep -Milliseconds 100 }
  if(-not (Find-Name '认证密码')) { throw 'Upgrade authentication was not pending before disconnect.' }
 } finally { Set-Content -LiteralPath ($disconnectMarker+'.release') -Value 'disconnect' -Encoding UTF8 }
 if(-not $disconnect.WaitForExit(10000) -or $disconnect.ExitCode -ne 0 -or -not (Test-Path -LiteralPath ($disconnectMarker+'.closed'))) { throw 'Upgrade client did not disconnect normally.' }
 Enter-FixturePassword 'fixture-password'
 for($attempt=0;$attempt -lt 100;$attempt++) {
  if((Find-Id $script:root 'RefreshButton').Current.IsEnabled -and (Find-Id $script:root 'RootNavigation').Current.IsEnabled) { break }
  Start-Sleep -Milliseconds 100
 }
 if($script:qaProcess.HasExited -or -not (Find-Id $script:root 'RefreshButton').Current.IsEnabled) { throw 'Disconnected upgrade did not resume commands.' }
 Click '刷新'
 for($attempt=0;$attempt -lt 100 -and -not (Find-Id $script:root 'RefreshButton').Current.IsEnabled;$attempt++) { Start-Sleep -Milliseconds 100 }
 if((SettingsNumber 'ForceCloseCountdownNumberBox') -ne '181') { throw 'Disconnected upgrade lost retained draft.' }
 $checks.Add('native-upgrade-client-disconnect-restores-commands-and-draft')
 $client=Begin-UpgradeClient 'upgrade-auth-accept'
 Start-Sleep -Milliseconds 600; Click '不保存并退出'
 Start-Sleep -Seconds 1
 Enter-FixturePassword 'fixture-password'
 if(-not $client.WaitForExit(15000) -or $client.ExitCode -ne 0 -or -not $script:qaProcess.WaitForExit(15000)) { throw 'Authenticated upgrade did not exit normally.' }
 $checks.Add('upgrade-authentication-accept-and-normal-exit')
 if((Get-Content -LiteralPath (Join-Path $data 'settings.json') -Raw | ConvertFrom-Json).forceCloseCountdownSeconds -eq 181) { throw 'No-save exit persisted draft.' }
 $checks.Add('upgrade-no-save-exit-keeps-persisted-settings')
 @{kind='native-WinUI-and-system-picker-automation';at=(Get-Date -Format o);checks=@($checks);uncovered=@('actual tray-menu click','installed uninstall launch/failure','shutdown flush failure with active game ticks (dirty catalog ledger covered)','native commit callback failure (client disconnect before response covered)','full themes/DPI/keyboard/screen reader','clean-machine installation','overnight/sleep','real-game notifications/anti-cheat')} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputRoot 'desktop-evidence.json') -Encoding UTF8
 Write-Output "PASS: $($checks.Count) desktop checks. Evidence: $OutputRoot"
} catch {
 $_ | Format-List * -Force | Out-String | Set-Content -LiteralPath (Join-Path $OutputRoot 'failure-error.txt') -Encoding UTF8
 if($script:root) {
  try {
   Screenshot 'failure.png'
   $script:root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition) | ForEach-Object { '{0} | {1} | {2}' -f $_.Current.ControlType.ProgrammaticName,$_.Current.AutomationId,$_.Current.Name } | Set-Content -LiteralPath (Join-Path $OutputRoot 'failure-ui.txt') -Encoding UTF8
  } catch { Write-Warning "Could not capture failure UI: $_" }
 }
 Write-Output ('Completed desktop checks before failure: ' + ($checks -join ', '))
 Write-Output "QA evidence directory: $OutputRoot"
 throw
} finally {
 $null=[DesktopQaNative]::SetThreadExecutionState([uint32]2147483648)
 if($script:qaProcess -and -not $script:qaProcess.HasExited) {
  $pickerCancel=Find-Id $script:root '2'
  if($pickerCancel -and $pickerCancel.Current.NativeWindowHandle -ne 0) { try { Activate $pickerCancel } catch { } }
  $close=Find-Id $script:root 'CloseButton'
  if($close) { try { Activate $close } catch { } }
  $script:qaProcess.CloseMainWindow() | Out-Null
  Start-Sleep -Milliseconds 400
  if(-not $script:qaProcess.HasExited -and (Find-Name '认证密码')) { try { Enter-FixturePassword 'fixture-password' } catch { } }
  if(-not $script:qaProcess.WaitForExit(3000)) { Write-Warning "QA window still running: PID $($script:qaProcess.Id). Use normal UI exit." }
 }
}
