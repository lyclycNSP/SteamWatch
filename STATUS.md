# SteamWatch WinUI 项目状态

## 当前状态

基础工程已经完成初始化。.NET SDK、Git、WinUI 模板在当前环境中可用。WinUI App 项目已经创建并能构建通过；当前优先推进 Core 和 Infrastructure 层，降低后续 UI 接入风险。

## 已完成

- 2026-04-29：确认 .NET SDK 10.0.203 和 Git 2.52.0 可用。
- 2026-04-29：创建 `D:\000MyWorkSpace\SteamWatch.WinUI`。
- 2026-04-29：初始化独立 git 仓库。
- 2026-04-29：创建 Core、Infrastructure、Tests 项目。
- 2026-04-29：创建 `SPEC.md`、`PLAN.md`、`STATUS.md` 作为项目唯一看板。
- 2026-04-29：加入初始限额评估和周聚合模型。
- 2026-04-29：从旧项目复制应用图标。
- 2026-04-29：加入 4 个 MSTest 用例覆盖日/周/全局限额评估。
- 2026-04-29：`dotnet test SteamWatch.slnx` 通过：16 passed，0 failed。
- 2026-04-29：`dotnet build SteamWatch.slnx` 通过：0 warnings，0 errors。
- 2026-04-29：将三份看板文档统一改为中文。
- 2026-04-29：加入渐强提醒状态机。
- 2026-04-29：加入强制退出策略和倒计时状态模型。
- 2026-04-29：通过 NuGet 安装 Microsoft 官方 WinUI C# 模板 `Microsoft.WindowsAppSDK.WinUI.CSharp.Templates`。
- 2026-04-29：创建 `SteamWatch.App` WinUI 3 应用项目并接入解决方案。
- 2026-04-29：删除模板生成的英文代理说明文件，避免与中文看板和项目规则冲突。
- 2026-04-29：为 WinUI App 建立初始原生界面骨架，包含游戏、统计、日志、设置入口。
- 2026-04-29：实现 Infrastructure 层 JSON 文件存储。
- 2026-04-29：实现 Core 层运行中会话增量计时模型。
- 2026-04-29：`dotnet test SteamWatch.slnx` 通过：27 passed，0 failed。
- 2026-04-29：`dotnet build SteamWatch.slnx` 通过：0 warnings，0 errors。
- 2026-04-29：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。
- 2026-04-29：实现 Steam 安装路径发现、SteamID 64 转 32、`loginusers.vdf` 解析、`localconfig.vdf` 时长解析、`appmanifest_*.acf` 游戏解析和图标路径查找。
- 2026-04-29：加入 Steam 缓存读取相关单元测试。
- 2026-04-29：`dotnet test SteamWatch.slnx` 通过：36 passed，0 failed。
- 2026-04-29：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。
- 2026-04-29：实现 Steam 进程快照模型、AppID 识别规则、运行游戏状态机和 Windows 进程快照读取器。
- 2026-04-29：移除旧版不可解释的 AppID 哈希猜测策略，只接受 `steam_app_数字`、Steam overlay 目标或已知 Steam 游戏路径匹配。
- 2026-04-29：加入 Steam 进程监控相关单元测试。
- 2026-04-29：`dotnet test SteamWatch.slnx` 通过：52 passed，0 failed。
- 2026-04-29：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。

## 进行中

- Windows 通知、声音提醒和开机自启基础设施设计。

## 阻塞

- 暂无核心开发阻塞。
- 备注：`dotnet build SteamWatch.slnx -c Debug -p:Platform=x64` 当前因 `.slnx` 缺少 `Debug|x64` 解决方案配置映射失败；应用项目可用 `dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 单独构建。

## 下一步

- 实现 Windows 通知和系统声音服务。
- 实现开机自启注册表管理。
- 梳理解决方案 x64 平台配置或固定构建脚本。
