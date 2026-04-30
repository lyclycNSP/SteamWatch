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
- 2026-04-29：里程碑 2 基础设施任务完成范围调整：暂不包含手动数据导出，发布版不展示导出入口。
- 2026-04-29：`dotnet test SteamWatch.slnx` 通过：64 passed，0 failed。
- 2026-04-29：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。
- 2026-04-29：实现 WinUI 游戏列表基础版，页面启动和刷新时读取 Steam 缓存并显示游戏名称、AppID、Steam 累计时长和基础状态。
- 2026-04-29：新增 `SteamWatchAppService` 作为应用服务编排入口，先接入 Steam 路径发现和缓存读取。
- 2026-04-29：将游戏行 ViewModel 调整为 WinUI 友好的可设置属性类，避免 XAML 生成器与 `record` init-only 属性冲突。
- 2026-04-29：`dotnet test SteamWatch.slnx` 通过：64 passed，0 failed。
- 2026-04-29：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。
- 2026-04-29：应用服务接入 Steam 进程监控轮询和运行中会话计时。
- 2026-04-29：游戏列表页面每 10 秒刷新运行状态，显示运行中/未运行和本次会话产生的今日/本周分钟数。
- 2026-04-29：当前计时先以内存会话显示，持久化到 JSON 将在后续接入。
- 2026-04-29：`dotnet test SteamWatch.slnx` 通过：64 passed，0 failed。
- 2026-04-29：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。
- 2026-04-30：新增 `PlaytimeRecordBook`，负责每日游戏分钟数合并、单日查询和周汇总查询。
- 2026-04-30：新增 `PlaytimeRecordStore`，通过现有 `JsonFileStore` 持久化 `playtime.json`。
- 2026-04-30：应用服务加载历史时长记录，运行中会话产生分钟增量后立即合并并保存到 JSON。
- 2026-04-30：游戏列表今日/本周分钟数改为基于持久化记录展示，页面轮询增加防重入保护。
- 2026-04-30：`dotnet test SteamWatch.slnx` 通过：70 passed，0 failed。
- 2026-04-30：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。
- 2026-04-30：新增 `RuntimeLimitCoordinator`，串联限额评估、渐强提醒状态和强退倒计时状态。
- 2026-04-30：新增 `LimitRuleStore`，通过现有 `JsonFileStore` 持久化 `limits.json`。
- 2026-04-30：应用服务轮询时加载限额规则并基于持久化时长评估运行中游戏；达到阈值时触发 Toast/声音提醒。
- 2026-04-30：应用服务状态栏可显示超限、强退倒计时和达到强退条件；实际终止进程尚未接入。
- 2026-04-30：`dotnet test SteamWatch.slnx` 通过：76 passed，0 failed。
- 2026-04-30：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。
- 2026-04-30：新增强制退出执行适配器，先尝试关闭主窗口，失败后终止进程树。
- 2026-04-30：强退倒计时到期后，应用服务会先完成并保存目标游戏当前会话时长，再执行关闭。
- 2026-04-30：单游戏强退会关闭对应 AppID 的运行进程；全局强退当前按所有运行中的 Steam 游戏处理。
- 2026-04-30：加入强制退出适配器单元测试。
- 2026-04-30：`dotnet test SteamWatch.slnx` 通过：80 passed，0 failed。
- 2026-04-30：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。
- 2026-04-30：游戏页右侧新增限额配置入口，支持全部游戏/所选游戏、每日/每周、仅提醒/强制退出。
- 2026-04-30：限额配置入口保存规则到 `limits.json`，同一范围和周期的规则会被替换。
- 2026-04-30：游戏页右侧新增已保存规则列表，用于查看当前生效的限额规则。
- 2026-04-30：`dotnet test SteamWatch.slnx` 通过：80 passed，0 failed。
- 2026-04-30：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。
- 2026-04-30：新增统计页数据行模型，应用服务可基于 `playtime.json` 生成每日明细和每周汇总。
- 2026-04-30：导航“统计”切换到独立统计视图，显示每日明细和每周汇总两个列表。
- 2026-04-30：统计页随游戏列表加载和运行中计时刷新同步更新。
- 2026-04-30：`dotnet test SteamWatch.slnx` 通过：80 passed，0 failed。
- 2026-04-30：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。
- 2026-04-30：新增 `AppSettings` 和 `AppSettingsStore`，通过现有 `JsonFileStore` 持久化 `settings.json`。
- 2026-04-30：设置页接入关闭窗口行为、开机自启、声音提醒和强退倒计时秒数配置。
- 2026-04-30：保存设置时同步开机自启注册表；声音提醒开关和强退倒计时会影响运行时提醒和倒计时策略。
- 2026-04-30：`dotnet test SteamWatch.slnx` 通过：82 passed，0 failed。
- 2026-04-30：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。
- 2026-04-30：新增纯 Win32 托盘图标服务，托盘菜单包含打开 SteamWatch、暂停/恢复监控、设置、退出。
- 2026-04-30：主窗口关闭行为接入 `settings.json`，配置为最小化到托盘时取消关闭并隐藏窗口，配置为退出应用时正常退出。
- 2026-04-30：托盘“设置”可打开窗口并切换到设置页，托盘“暂停监控”可暂停或恢复运行中轮询。
- 2026-04-30：里程碑 3 WinUI 应用任务全部完成。
- 2026-04-30：`dotnet test SteamWatch.slnx` 通过：82 passed，0 failed。
- 2026-04-30：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。
- 2026-04-30：新增 `scripts\publish-portable.ps1` 绿色版发布脚本，支持 `Release/Debug` 和 `x64/x86/ARM64` 参数。
- 2026-04-30：发布脚本输出目录为 `artifacts\publish\SteamWatch-win-x64`，ZIP 产物为 `dist\SteamWatch-win-x64.zip`。
- 2026-04-30：`powershell -ExecutionPolicy Bypass -File scripts\publish-portable.ps1 -Configuration Release -Platform x64` 通过，生成约 102 MB 的 ZIP。
- 2026-04-30：确认发布目录包含 `SteamWatch.App.exe` 和 `README-portable.txt`。
- 2026-04-30：新增 `docs\manual-acceptance.md`，整理启动、计时、提醒、强退、统计、设置、绿色版产物的手动验收清单。
- 2026-04-30：将 `.slnx` 中 WinUI App 默认项目平台映射从 x86 调整为 x64。
- 2026-04-30：`dotnet build SteamWatch.slnx -c Debug` 通过，App 输出到 x64 Debug 目录。
- 2026-04-30：`dotnet test SteamWatch.slnx` 通过：82 passed，0 failed。
- 2026-04-30：根据手动测试反馈，发布包不再包含 `SPEC.md`、`PLAN.md`、`STATUS.md`。
- 2026-04-30：App 程序集名改为 `SteamWatch`，绿色版入口文件改为 `SteamWatch.exe`。
- 2026-04-30：从 `E:\007Programs\SteamWatch\assets\icon.ico` 更新应用图标，设置为 EXE、窗口、托盘和输出目录资源图标。
- 2026-04-30：游戏监控界面新增暂停/恢复监控按钮，与托盘菜单共享暂停状态。
- 2026-04-30：限额提醒和强退倒计时除系统通知/声音外，新增应用内弹窗提醒。
- 2026-04-30：`dotnet test SteamWatch.slnx` 通过：82 passed，0 failed。
- 2026-04-30：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。
- 2026-04-30：重新运行绿色版发布脚本通过，确认发布目录包含 `SteamWatch.exe` 和 `Assets\AppIcon.ico`，不包含项目看板文档。
- 2026-04-30：提醒阈值从固定 70%/85%/95% 改为设置页可配置，默认值保持不变。
- 2026-04-30：系统通知实现优先使用 Windows App SDK `AppNotificationManager`，失败时回退到旧 Toast 通知。
- 2026-04-30：提醒和强退倒计时开始时同时触发系统通知、声音和应用内弹窗。
- 2026-04-30：游戏列表显示 Steam 缓存中的游戏图标，让选择限额目标更直观。
- 2026-04-30：绿色版 README 新增启动入口和 Windows 勿扰模式检查步骤；为避免编码问题，README 使用 ASCII 英文说明。
- 2026-04-30：`dotnet test SteamWatch.slnx` 通过：83 passed，0 failed。
- 2026-04-30：`dotnet build src\SteamWatch.App\SteamWatch.App.csproj -c Debug -p:Platform=x64` 通过：0 warnings，0 errors。
- 2026-04-30：重新运行绿色版发布脚本通过，确认 `README-portable.txt`、`SteamWatch.exe`、`Assets\AppIcon.ico` 和 ZIP 产物已更新。
- 2026-04-30：发布脚本改为在 `artifacts\publish` 下生成与程序文件夹同级的 `操作指南.txt`，ZIP 根目录也包含该指南。
- 2026-04-30：扩展 Steam 图标匹配，支持新版 `appcache\librarycache\<appid>\logo.png`、`header.jpg`、`library_600x900.jpg` 及嵌套 hash 子目录资源。
- 2026-04-30：`dotnet test SteamWatch.slnx` 通过：84 passed，0 failed。
- 2026-04-30：重新运行绿色版发布脚本通过，确认 ZIP 根目录包含 `操作指南.txt` 和 `SteamWatch-win-x64\SteamWatch.exe`。

## 进行中

- Windows 手动验收测试准备。

## 阻塞

- 暂无核心开发阻塞。

## 下一步

- 完成 Windows 手动验收测试。
