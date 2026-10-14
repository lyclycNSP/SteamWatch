param([int]$CallerProcessId = 0)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
try {
    $expectedRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\SteamWatch'))
    $actualRoot = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
    if (-not [string]::Equals($expectedRoot, $actualRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Uninstall directory validation failed.' }
    if ((Get-Item -LiteralPath $expectedRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Uninstall cannot follow a redirected directory.' }
    if ($CallerProcessId -gt 0) { Wait-Process -Id $CallerProcessId -Timeout 15 -ErrorAction SilentlyContinue }
    $running = @(Get-Process -Name SteamWatch -ErrorAction SilentlyContinue | Where-Object { $_.Path -and [string]::Equals([IO.Path]::GetDirectoryName($_.Path),$expectedRoot,[StringComparison]::OrdinalIgnoreCase) })
    if ($running.Count -gt 0) { throw '请先退出 SteamWatch，再执行卸载。' }
    $dataRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'SteamWatch'))
    $legacyData = Join-Path $expectedRoot 'data'
    if (Test-Path -LiteralPath $legacyData) {
        New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null
        foreach ($file in Get-ChildItem -LiteralPath $legacyData -File) {
            $target = Join-Path $dataRoot $file.Name
            if (-not (Test-Path -LiteralPath $target)) { Copy-Item -LiteralPath $file.FullName -Destination $target }
        }
    }
    $answer = [Windows.Forms.MessageBox]::Show('确认卸载 SteamWatch？设置和游玩记录会保留。','SteamWatch',[Windows.Forms.MessageBoxButtons]::YesNo,[Windows.Forms.MessageBoxIcon]::Question,[Windows.Forms.MessageBoxDefaultButton]::Button2)
    if ($answer -ne [Windows.Forms.DialogResult]::Yes) { exit 0 }
    Remove-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name SteamWatch -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SteamWatch' -Recurse -Force -ErrorAction SilentlyContinue
    $menu = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs\SteamWatch'
    foreach ($name in @('SteamWatch.lnk','卸载 SteamWatch.lnk','操作指南.lnk')) {
        $shortcut = Join-Path $menu $name
        if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut -Force }
    }
    $desktop = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'SteamWatch.lnk'
    if (Test-Path -LiteralPath $desktop) { Remove-Item -LiteralPath $desktop -Force }
    # The absolute target was checked against the one known installation directory above.
    Remove-Item -LiteralPath $expectedRoot -Recurse -Force
    [Windows.Forms.MessageBox]::Show('SteamWatch 已卸载，数据保留在 LocalAppData\SteamWatch。','SteamWatch') | Out-Null
} catch {
    [Windows.Forms.MessageBox]::Show($_.Exception.Message,'SteamWatch 卸载失败') | Out-Null
    exit 1
}
