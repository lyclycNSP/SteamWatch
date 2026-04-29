# SteamWatch WinUI 项目状态

## 当前状态

非 UI 基础工程已经完成初始化。.NET SDK 和 Git 在当前环境中可用。WinUI 模板尚未安装，因此 WinUI 应用项目仍然阻塞；当前优先推进可测试的 Core 层领域逻辑。

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

## 进行中

- Core 层运行中会话计时与持久化前置模型。

## 阻塞

- WinUI 应用项目生成被阻塞：`dotnet new list winui` 找不到 WinUI 模板，当前未安装 .NET workload。

## 下一步

- 安装 WinUI 3 / Windows App SDK 模板。
- 增加运行中会话增量计时模型。
- 增加 JSON 存储实现。
