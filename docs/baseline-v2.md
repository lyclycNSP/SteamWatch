# 2.0 重写基线与功能迁移对照

> 历史基线：以下“当前入口/新入口”均指建立基线时的记录，旧规则标签目标已撤销。当前合同见 [SPEC](../SPEC.md) 和交互约定，动态结果只见 本地 `STATUS.md`。

日期：2026-10-12。对应 WU-BASE-201。依据为当前未提交工作区，不以 Git HEAD 代表本地完整实现。所有测试数据注明来源，不将受控进程、合成夹具和桌面自动化当作真实游戏/干净系统验收。

基线用途澄清：本文件保存 1.2.0 的历史能力、数据样本和验证起点，不要求新版维持源码不变。各层代码、模型与 API 可重构；合理行为改进以更新后的 SPEC 和对照验收为准，旧数据迁移及安全保护仍须验证。以下“本轮”均指建立基线阶段，后续进度见 STATUS。

## 基线与证据

- 源码仍为 .NET 10 / Windows App SDK 1.8.260416003 / WinUI 3，程序集 1.2.0；本轮没有修改生产代码、版本、安装脚本或用户数据。
- 原始 Release 全套 136 通过；新增兼容回归后全套 138 通过、0 失败、0 跳过。Debug WinUI 构建 0 警告、0 错误。
- 本轮独立绿色包发布成功；日志与 TRX 位于 `artifacts/v2-baseline/`，载荷在其 `artifacts/publish/SteamWatch-win-x64`。这是现有 UI 的基线包，不是 2.0 实现或 Inno 样包。
- 原生 WinUI/系统选择器 13 项通过，证据：`artifacts/desktop-8fd3a07999af45f9997da73207304d1a/desktop-evidence.json`。已直接查看该目录 `games.png`；不是 README 旧截图。
- 受控 Windows 进程三次约 20 秒会话，普通、刷新、结算重启的计时误差分别为 +0.407、+0.463、+0.185 秒，错误身份拒绝/正确身份实际退出通过。证据：`artifacts/runtime-dee55414b5e44a0a88388cb1a618cf4b/evidence/runtime-evidence.json`。不将此误差范围推广为所有游戏精度保证。
- 桌面检查使用新复制的绿色目录和测试密码，执行前确认没有 SteamWatch 自启项；没有操作主安装或真实游戏，正常退出检查中的测试应用已退出。
- 签名、远程 CI、完整安装恢复、真实游戏、午夜/周一/休眠/校时仍无本轮验收结论；性能对比由 RUNTIME-201 在同口径数据下完成，本轮不编造性能数字。

## 功能迁移矩阵

“新入口”是目标，不表示已实现。删除旧 UI 前逐项对照本表，并把实际结果写入 STATUS。

| 能力 | 当前入口 / 生产路径 | 新入口 | 验证与不变量 |
| --- | --- | --- | --- |
| Steam 发现、多库、图标、累计 | MainPage.LoadGames → AppService → Runtime.RefreshGames → SteamCatalogProvider | 游戏库与游戏详情 | 原目录/缓存/多库回归；未知保留，AppID 与 Steam 累计可访问 |
| 计时、短会话、跨日/周、休眠 | AppService.MonitorAsync → Runtime.Poll → 活动时钟 / PlaytimeLedger | 游戏实测摘要与统计 | ticks、余数、日期、暂停继续计时；真实长时/休眠另外验收 |
| 搜索、运行/规则/类别筛选 | MainPage.ApplyGameFilter / UpdateGameRows | 游戏库筛选栏 | 组合筛选、按 AppID 更新、保留选择/滚动；筛选不改变监控 |
| 条目分类 | MainPage.SetGameCategory → Runtime.SetGameCategory | 游戏详情或更多菜单 | 用户明确设置、未知/工具不移出全局范围，备份恢复分类 |
| 全局/单游戏日周规则 | MainPage.Rules → Runtime.SaveRule/DeleteRule | 限额规则标签、统一编辑器 | 自然身份、四种组合、并行约束；冲突/取消不静默覆盖 |
| 规则用量与强退摘要 | Runtime.RefreshLimitDecisions / Publish / EarliestForceCloseSummary | 游戏摘要、规则列表、游戏详情 | 只采用实测判断；无目标/仅提醒/暂停/倒计时各有状态 |
| 暂停/恢复 | MainPage.SetMonitoringPaused、托盘 → Runtime.SetPaused | 页头与托盘 | 继续计时、停止提醒与强退、恢复新宽限；身份验证一致 |
| 通知与进程执行 | Runtime.Notify/Raise、GameProcessTerminator、WindowsAppNotificationService | 系统通知、应用提示、声音 | 先保存、核对身份、温和关闭及确认；失败不得假报成功 |
| 日周统计与估算来源 | MainPage.Statistics → AppService.GetDaily/WeeklyStats / GetEstimatedEntries | 日周切换、周图与等价表格 | 实测/估算/合计分开；历史未知不推测；周一开始 |
| 修正估算 | MainPage.EstimatedDetails → Runtime.CorrectEstimatedMinutes | 估算明细修正对话框 | 期望旧值、原因、认证、确认、前置备份；不回退实测/游标 |
| 常规即时设置 | MainPage.Settings.SaveImmediateGeneralSettings → Runtime.SaveSettings | 统一设置行 | 自启、声音、关闭行为沿用即时保存；失败回退持久化值 |
| 提醒/认证显式设置 | MainPage.Settings.SaveSettings → Runtime.SaveSettings | 提醒/认证分组草稿 | 阈值递增、倒计时、密码校验、未保存离页处理；保护不缩减 |
| 完整备份导出/恢复 | 系统选择器 → Runtime.CreateBackup/RestoreBackup | 数据与维护 | 导出含认证配置须保护；合法性、前置备份、中断重放；取消无写入 |
| 诊断与数据目录 | CreateDiagnostics → DiagnosticPackage；OpenDataFolder | 数据与维护 | 诊断白名单/大小上限；与完整备份区分；打开目录仍可达 |
| 单实例、托盘与窗口退出 | App、MainWindow、SingleInstanceService、TrayIconService | Shell 与生命周期协调 | 隐藏/切页不断监控，不增第二循环；退出失败继续运行 |
| 安装升级/卸载 | 当前自制 Installer、DirectoryUpgrade、UpgradeExitServer/Client、Uninstall.ps1 | Inno 验证通过后迁移 | 正常认证保存退出、旧项兼容、程序/数据分离、默认保留数据 |
| 帮助/关于 | HelpView、操作指南、当前版本 | 帮助与关于底部入口 | 同步真实新 UI 后替换说明；不提前描述未落地操作 |

## 敏感动作矩阵

“要求认证”均以当前持久化设置已启用保护且存在凭据为前提；不保护的读取功能保持可用。归并入口时不新增默认免认证期。

| 动作 | 当前授权调用点 | 取消/失败要求 |
| --- | --- | --- |
| 保存/更新限额 | MainPage.Rules.SaveLimitButton → AuthorizeSensitiveActionAsync | 不保存、不替换其他身份规则 |
| 删除限额 | MainPage.Rules.DeleteLimitRuleButton → AuthorizeSensitiveActionAsync | 原规则保留 |
| 暂停/恢复 | MainPage.SetMonitoringPausedAsync；托盘调用同一路径 | 暂停状态不变，计时持续 |
| 修改分类 | MainPage.SetGameCategoryButton → AuthorizeSensitiveActionAsync | 类别不变 |
| 常规即时保存 | MainPage.Settings.SaveImmediateGeneralSettings → AuthorizeSensitiveActionAsync | 恢复控件持久化值，不污染其他草稿 |
| 提醒/认证保存、改密/关闭保护 | MainPage.Settings.SaveSettingsButton 按旧设置校验当前密码 | 不用新草稿关闭旧保护，保持旧设置 |
| 修正估算 | MainPage.Statistics.EstimatedDetails → AuthorizeSensitiveActionAsync，再确认 | 无修正；冲突/失败不发布候选 |
| 完整备份导出 | MainPage.Settings.ExportBackup → AuthorizeSensitiveActionAsync | 不导出认证配置；诊断包另行处理 |
| 恢复备份 | MainPage.Settings.RestoreBackup → AuthorizeSensitiveActionAsync，再确认 | 不替换数据或设置 |
| 窗口/托盘退出 | MainWindow.AuthorizeExit → MainPage.AuthorizeSensitiveActionAsync | 应用继续运行；保存失败恢复循环 |
| 升级协调退出 | MainWindow.PrepareForUpgrade → AuthorizeExit | 拒绝/超时/保存失败不替换程序；安装器不接触密码 |
| 应用内卸载 | MainPage.Settings.Uninstall → AuthorizeSensitiveActionAsync | 不启动卸载；启动失败恢复监控 |
| 查看/搜索/筛选、查看来源、导出诊断、打开目录、导航 | 当前无认证调用 | 不在重写中无依据新增密码步骤；诊断仍遵守隐私白名单 |

## 合成兼容样本

[夹具说明](../tests/SteamWatch.Tests/Fixtures/V2Baseline/README.md) 定义 7 份 JSON，包含账本 v1/v2、修正审计、分类、规则/设置及缺分类字段的旧备份。固定日期为 2026-10-11，账号为合成标识；不含真实历史/凭据。

新增 `BaselineCompatibilityTests` 通过实际 Runtime 恢复/重启路径验证旧备份分类复位、90 秒精度、v1 迁移、规则策略、v2 审计和游标不回退。既有回归覆盖保存失败、损坏恢复、跨日/休眠模拟、串行命令和恢复日志重放；实际 OS 场景仍按验收计划执行。

## 迁移前发现的差异

1. **展示口径**：Runtime.Publish 调用默认包含估算的 `PlaytimeLedger.GetRecords()`，AppService.TotalToday/Week 与 RuntimeGame 用它显示总量；限额评估明确调用 `includeEstimates: false`。当前桌面夹具显示总量 16 分钟 = 1 分钟实测 + 15 分钟估算。新版实测首页必须在投影层显式分开口径，不能把改变统计返回值当作修复，以免丢失估算。归 UI-202/204，增加口径回归。
2. **规则冲突**：Runtime.SaveRuleAsync 的 upsert 按新身份替换已有项；新版统一编辑器需在改变身份时明确冲突，不能靠“后端允许 upsert”证明用户意图。归 UI-203，不在基线阶段改变 API。
3. **设置草稿**：当前 ApplySettings 会重设提醒/认证控件；常规保存调用它可能覆盖其他分组的未保存编辑。新版即时设置和显式草稿需分离。归 UI-205。
4. **生命周期与弹窗**：背景循环在 AppService，页面计时器用于刷新；此边界继续保留。MainWindow 的退出错误直接 ShowAsync 绕过页面对话框门，归 UI-201/RUNTIME-201 统一调度。
5. **首启与布局**：当前首启欢迎弹窗和固定双栏属于待迁移交互，新版替换为页内非阻塞说明、按需详情；源截图仅作现状参考。

上述属于代码/当前界面可确认的差异，不等于本轮已修复。后续新代码与当前基线逐项比较，再更新 STATUS。
