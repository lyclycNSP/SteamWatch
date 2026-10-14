# 开发与构建

SteamWatch 使用 .NET 10、WinUI 3 / Windows App SDK，主要构建目标为 Windows x64。需要 Windows、.NET 10 SDK，以及用于原生绿色启动器的 Visual Studio C++ Build Tools / Windows SDK。运行发布包不要求安装这些开发工具。

## 本地验证

在仓库根目录使用 PowerShell 7：

```powershell
dotnet build src/SteamWatch.App/SteamWatch.App.csproj -c Debug -p:Platform=x64
dotnet test tests/SteamWatch.Tests/SteamWatch.Tests.csproj -c Release
```

## 构建预览包

```powershell
pwsh -NoProfile -File scripts/build-installer.ps1 -Configuration Release -Platform x64 -SigningMode Unsigned
pwsh -NoProfile -File scripts/verify-delivery.ps1 -Directory dist
```

生成绿色 ZIP、安装 EXE、校验文件与发布清单。可指定 `-OutputRoot artifacts/my-preview` 隔离输出；旧产物在失败时保留。首次打包会下载并校验固定的 Inno Setup 便携工具链，不进行全局安装。版本只在 Directory.Build.props 中维护。

项目采用 MIT；打包从实际还原的 NuGet 依赖收集许可，放入 app/licenses（绿色版）或 licenses（安装版），并纳入逐文件校验。不删除依赖声明，不把第三方代码改标为项目 MIT。详见 [第三方说明](../THIRD-PARTY-NOTICES.md)。

## 模块

| 目录 | 职责 |
| --- | --- |
| src/SteamWatch.App | Shell、页面、展示状态、托盘与 Windows 生命周期 |
| src/SteamWatch.Core | 时长账本、规则、提醒与统计 |
| src/SteamWatch.Infrastructure | Steam、存储、执行与运行编排 |
| src/SteamWatch.Installer / installer | 安装事务助手与 Inno 向导 |
| src/SteamWatch.Launcher | 绿色版根启动器 |
| tests / tools / scripts | 单测、隔离探针、构建与验证 |

## 可选专项工具

以下工具需交互桌面、隔离数据和新的输出目录，不与其他桌面自动化并行。使用已编译的 RuntimeProbe 正常退出测试应用，不强杀用户进程：

```powershell
dotnet publish tools/SteamWatch.RuntimeProbe/SteamWatch.RuntimeProbe.csproj -c Release -r win-x64 --self-contained false -o artifacts/runtime-probe
pwsh -NoProfile -File scripts/test-startup.ps1 -PublishDirectory <绿色发布目录> -ProbeExecutable artifacts/runtime-probe/SteamWatch.RuntimeProbe.exe -OutputRoot <新的启动证据目录> -WakeDisplay
pwsh -NoProfile -File scripts/test-desktop.ps1 -PublishDirectory <绿色发布目录> -ProbeExecutable artifacts/runtime-probe/SteamWatch.RuntimeProbe.exe -OutputRoot <新的桌面证据目录>
pwsh -NoProfile -File scripts/test-runtime.ps1 -Mode active-exit-recovery
```

启动脚本比较根启动器与直接载荷、不同工作目录，检查真实窗口内容与正常退出。直接载荷模式只用于排查，日常仍从根启动器启动。桌面脚本与启动脚本的 `-WakeDisplay` 使用临时线程执行请求保持显示唤醒，在 finally 中释放；不修改系统电源方案，也不进入应用运行逻辑。接口语义见 [Microsoft 文档](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate)。完整主题/重挂载宿主见 [验收宿主说明](../tools/SteamWatch.UiAcceptanceHost/README.md)。安装事务、驻留与性能工具见 [测试策略](test-strategy.md)，并非首次开源预览的全部必跑项。

GitHub Actions 配置只做构建/验证与保存产物，不自动发布。是否已在远程执行，以具体运行记录为准。源码与二进制发布应对应同一提交/标签；准备阶段可以记录 dirty 工作区快照，但不能把它冒充已创建的发布标签。

根 SPEC.md 是公开行为规格。PLAN.md、STATUS.md、PROGRESS.md 是维护者本地工程文件，不随 Git 发布；公开使用说明不依赖这些本地文件，本地看板记录真实进度，不在 README 复制测试计数。
