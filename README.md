# SteamWatch.WinUI

SteamWatch 是一个 Windows 桌面工具，用于监控 Steam 游戏运行时间，并根据每日或每周限额进行提醒或强制退出。

项目使用 WinUI 3 和 .NET 10 构建，支持绿色版发布和单文件安装器发布。

## 功能

- 自动识别 Steam 已安装游戏和正在运行的游戏进程。
- 记录每个游戏的今日时长和本周时长。
- 支持为全部游戏或单个游戏设置每日/每周限额。
- 支持两种超限策略：
  - 仅提醒：系统通知、程序内弹窗和声音提醒。
  - 强制退出：倒计时提醒后尝试关闭对应游戏进程。
- 支持自定义提醒阈值和强退倒计时。
- 支持托盘图标、暂停/恢复监控、关闭到托盘。
- 读取 Steam `appinfo.vdf` 缓存和本地库缓存，尽量显示真实 Steam 游戏图标。
- 提供中文操作指南，并在发布包和安装目录中附带。

## 环境要求

- Windows 10 2004 或更高版本。
- .NET 10 SDK。
- Steam 客户端。
- Visual Studio 2022 或安装了 WinUI / Windows App SDK 开发所需组件的构建环境。

项目目标框架：

- 应用：`net10.0-windows10.0.26100.0`
- 最低 Windows 平台：`10.0.19041.0`
- 主要发布平台：`win-x64`

## 项目结构

```text
SteamWatch.WinUI/
  src/
    SteamWatch.App/             WinUI 3 桌面应用
    SteamWatch.Core/            领域逻辑、限额、提醒、统计
    SteamWatch.Infrastructure/  Steam 读取、存储、进程关闭等基础设施
    SteamWatch.Installer/       自包含安装器
  tests/
    SteamWatch.Tests/           MSTest 单元测试
  scripts/
    publish-portable.ps1        生成绿色版 ZIP
    build-installer.ps1         生成绿色版 ZIP 和安装器 EXE
  docs/
    manual-zh.txt               中文操作指南
    manual-acceptance.md        手工验收说明
  assets/                       应用图标源文件
```

## 构建

还原并构建应用：

```powershell
dotnet build .\src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64
```

运行测试：

```powershell
dotnet test .\SteamWatch.slnx
```

生成绿色版 ZIP：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-portable.ps1 -Configuration Release -Platform x64
```

生成绿色版 ZIP 和安装器：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Configuration Release -Platform x64
```

构建产物会生成到：

- `dist/SteamWatch-win-x64.zip`
- `dist/SteamWatchSetup-win-x64.exe`

`dist/` 和 `artifacts/` 是发布产物目录，不提交到仓库。

## 使用

### 安装器版

运行 `SteamWatchSetup-win-x64.exe`。安装器会安装到当前用户目录，并创建桌面快捷方式、开始菜单快捷方式和卸载入口，不需要管理员权限。

### 绿色版

解压 `SteamWatch-win-x64.zip`，进入 `SteamWatch-win-x64` 文件夹，运行 `SteamWatch.exe`。

不要删除绿色版目录中的 `.dll`、`.json`、`Assets`、`runtimes` 等文件，它们是程序运行所需文件。

### 通知设置

全屏游戏时，Windows 可能自动启用勿扰模式，导致系统通知无法显示。建议按以下路径关闭相关自动规则：

1. 按 `Win + I` 打开 Windows 设置。
2. 进入 `系统` -> `通知`。
3. 确保顶部 `勿扰模式` 处于关闭状态。
4. 展开 `自动打开勿扰模式`。
5. 取消勾选 `玩游戏时`。
6. 取消勾选 `在全屏模式下使用应用时`。

更完整的使用说明见 [docs/manual-zh.txt](docs/manual-zh.txt)。

## 数据存储

SteamWatch 会在运行目录下的 `data/` 文件夹保存：

- 应用设置。
- 游戏限额规则。
- 本地游玩时长统计。

迁移绿色版时，可以一并复制 `data/` 文件夹。安装器版的运行数据位于安装目录下的 `data/`。