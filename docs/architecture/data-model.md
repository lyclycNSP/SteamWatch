# 数据与兼容合同

维护持久化字段、存储位置和兼容性。产品含义见 [PRD](../product/PRD.md)，行为见 [SPEC](../../SPEC.md)。以源码类型为字段定义依据，以下 JSON 字段采用现有 camelCase；不在此承诺新 schema。

## 存储边界

安装版数据为 LocalAppData/SteamWatch，绿色版为 marker 所在根目录 data。旧平铺数据继续可读，内层遗留数据复制迁移不覆盖根数据、不删除来源。账本、自启与恢复日志由运行服务协调；外部编辑器/旧进程直接改文件不受进程内写入门保护。

## DM-Ledger 时长账本

源：[PlaytimeLedgerState](../../src/SteamWatch.Core/Tracking/PlaytimeLedger.cs)。文件 playtime-ledger.json，当前 schemaVersion=2。

| 字段 | 类型 / 含义 | 约束 |
| --- | --- | --- |
| schemaVersion | int | 支持读 1/2，当前写 2；不支持直接降级 |
| days | DailyPlaytimeDuration[] | 日期唯一，结构完整 |
| days[].date | DateOnly | 每日本地日期 |
| days[].measuredTicks | AppID→long | 正 AppID、非负 100ns ticks；显示才按日/游戏取整分钟 |
| days[].estimatedMinutes | AppID→int | 正 AppID、非负整分钟；与估算链一致 |
| steamBaselines | 账号字符串→AppID→decimal | 按账号隔离，非负且不因缓存回退而降低 |
| gameNames | AppID→string | 历史名称，用于目录外条目 |
| estimatedEntries | EstimatedPlaytimeEntry[] | v2 必须存在；ID 唯一、逐笔前后值与汇总一致 |

估算条目字段由 [EstimatedPlaytimeEntry](../../src/SteamWatch.Core/Tracking/EstimatedPlaytimeEntry.cs) 定义：id、date、appId、source（SteamDifference/LegacyAggregate/ManualCorrection）、previousMinutes、currentMinutes、recordedAt（可空）、reason，及可空 accountId/steamExpectedMinutes/steamObservedMinutes。reason 非空且最长 500 字符，前后分钟非负；未知历史时间/账号保持空，不补造。

## DM-Rule / DM-Settings / DM-Category

| 文件 / 源码 | 字段与约束 |
| --- | --- |
| limits.json / [LimitRule](../../src/SteamWatch.Core/Limits/LimitRule.cs) | scope、period、maxMinutes、enforcement、appId、name；自然身份 Scope/Period/AppId，全局 AppId 归空；分钟非负、0 不启用，游戏 ID 为正；枚举必须合法 |
| settings.json / [AppSettings](../../src/SteamWatch.Core/Settings/AppSettings.cs) | closeWindowAction、startWithWindows、playReminderSounds、forceCloseCountdownSeconds、三项 threshold、requireAuthenticationForSensitiveActions、authenticationCredential；数值与保存行为见 FR-SET-001 |
| authenticationCredential / [PasswordCredential](../../src/SteamWatch.Core/Security/PasswordCredential.cs) | saltBase64、hashBase64、iterations、keySize；仅存哈希与参数，不存密码明文 |
| game-categories.json | AppID→SteamAppCategory，用户明确设置，未知默认；分类不改变计时/限制 |

默认参数与输入边界只在 SPEC/源码定义，不在其他文档复制完整配置样例。

## DM-Backup 完整备份与中断恢复

[SteamWatchBackup](../../src/SteamWatch.Infrastructure/Runtime/RuntimeSnapshot.cs) 的 schemaVersion=1，与账本 schemaVersion=2 独立。字段 ledger、settings、rules 必须存在，categoryOverrides 可空；旧备份缺分类恢复未知。认证配置可能随备份导出，诊断包不使用此结构。

恢复顺序：校验候选→保存不可覆盖的恢复前完整备份→写 workspace-restore.json→应用账本/规则/设置/分类及自启→删除日志。初始化发现日志先重放；失败不启动部分恢复状态下的监控。修正估算同样先保存 workspace-before-correction-*.json，再保存候选并发布。

## 原子写、迁移与展示投影

每路径进程内串行门；SerializeAsync 写独占临时文件，关闭后检查取消，再 File.Replace/Move，保留 .bak，失败清临时文件。运行服务只在保存成功后清 dirty，失败可重试且禁止后续强退。账本与补记游标同份提交，不承诺掉电或系统注册事务。

v1 迁移先保存 playtime-ledger-before-v2-*.json；聚合估算变为 LegacyAggregate 明细，保留原始分钟并标记未知来源时间。旧 Steam 快照缺账号时仅重建基线，不追溯该段差值。旧版读不了 v2，程序回滚不等于数据可降级。兼容夹具见 [基线](../baseline-v2.md)。

CreateReadSnapshot 是独立的统计/名称/估算展示副本，不复制 ticks/账号基线；完整存储和备份使用 Export。它不是持久化格式，也不是跨更新缓存。限额只读实测投影。
