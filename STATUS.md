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
- 2026-04-29：实现通知消息模型、Toast XML 生成、Windows Toast 通知服务和系统声音服务。
- 2026-04-29：实现开机自启管理接口、注册表适配器和可测试的启动项管理器。
- 2026-04-29：Infrastructure 和测试项目切换为 Windows 目标框架，以合法使用注册表、Toast 和系统声音 API。
- 2026-04-29：加入通知和开机自启相关单元测试。
- 2026-04-29：`dotnet test SteamWatch.slnx` 通过：59 passed，0 failed。
- 2026-04-29：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。
- 2026-04-29：实现 JSON/CSV 导出快照模型，包含游戏列表、时长记录、限额规则和应用设置。
- 2026-04-29：实现完整 JSON 导出、每日时长 CSV、周汇总 CSV 和限额配置 CSV。
- 2026-04-29：CSV 日期格式固定为 `yyyy-MM-dd`，避免受系统区域设置影响。
- 2026-04-29：里程碑 2 基础设施任务全部完成。
- 2026-04-29：`dotnet test SteamWatch.slnx` 通过：64 passed，0 failed。
- 2026-04-29：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。
- 2026-04-29：实现 WinUI 游戏列表基础版，页面启动和刷新时读取 Steam 缓存并显示游戏名称、AppID、Steam 累计时长和基础状态。
- 2026-04-29：新增 `SteamWatchAppService` 作为应用服务编排入口，先接入 Steam 路径发现和缓存读取。
- 2026-04-29：将游戏行 ViewModel 调整为 WinUI 友好的可设置属性类，避免 XAML 生成器与 `record` init-only 属性冲突。
- 2026-04-29：`dotnet test SteamWatch.slnx` 通过：64 passed，0 failed。
- 2026-04-29：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。

## 进行中

- WinUI 统计页面与设置页面设计。

## 阻塞

- 暂无核心开发阻塞。
- 备注：`dotnet build SteamWatch.slnx -c Debug -p:Platform=x64` 当前因 `.slnx` 缺少 `Debug|x64` 解决方案配置映射失败；应用项目可用 `dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 单独构建。

## 下一步

- 构建统计页面。
- 构建设置页面。
- 将 Steam 进程监控和运行中计时接入应用服务。
- 梳理解决方案 x64 平台配置或固定构建脚本。
