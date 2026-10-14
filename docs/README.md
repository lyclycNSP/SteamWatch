# SteamWatch 文档导航

**查看实际进度：本地 `STATUS.md`。查看任务范围：本地 `PLAN.md`。接手具体工作：[WU 合同](planning/work-units.md)。**

## 公开入口

| 阅读目的 | 入口 |
| --- | --- |
| 下载、使用与数据位置 | [README](../README.md)、[中文指南](manual-zh.txt) |
| 版本变化与预览说明 | [CHANGELOG](../CHANGELOG.md)、[2.0 预览说明](releases/2.0.0-preview.2.md) |
| 已知问题与未检查场景 | [已知问题](known-issues.md) |
| 开发、构建与本地验证 | [开发指南](development.md)、[公开行为规格](../SPEC.md) |
| 开源与依赖许可 | [MIT](../LICENSE)、[第三方说明](../THIRD-PARTY-NOTICES.md) |
| 发布范围与交付步骤 | [发布检查单](release-checklist.md) |

以下工程资料保留具体合同；动态看板 PLAN.md / STATUS.md / PROGRESS.md 是维护者本地文件，不随 Git 发布。公开入口不依赖这些本地看板。

## 工程资料

| 问题 | 权威文档 | 不在这里维护 |
| --- | --- | --- |
| 为谁做、目标与业务词汇 | [PRD](product/PRD.md) | 字段、实时状态 |
| 用户如何操作、失败如何恢复 | [SPEC](../SPEC.md) | 实现日志、完整 schema |
| 页面/状态/排版怎样统一 | [交互约定](design/interaction-contract.md) | 历史双标签方案、验证数量 |
| 为什么选这些设计 | [重设计决策](redesign-v2.md) | 第二份功能合同或执行计划 |
| 模块、状态与数据流 | [当前架构](architecture.md) | 全字段、产品需求全文 |
| 格式、迁移、数据恢复 | [数据模型](architecture/data-model.md) | 动态完成度 |
| 安装器怎样请求退出 | [API-UPGRADE-001](architecture/upgrade-protocol.md) | 密码传输或任意命令 |
| 安装迁移与故障矩阵 | [安装交付设计](installer-evaluation.md) | 历史暂不迁移作为当前指令 |
| 交付清单、签名与发布门 | [发布检查单](release-checklist.md) | 实际证书/签名与验收进度 |
| 各阶段/任务/依赖 | 本地 `PLAN.md`、[WU 索引合同](planning/work-units.md) | 完成勾选和测试数量 |
| 新的独立任务 | [DOC-202](planning/work-units/WU-DOC-202.md)、[RUNTIME-202](planning/work-units/WU-RUNTIME-202.md)、[RUNTIME-203](planning/work-units/WU-RUNTIME-203.md) | 状态副本 |
| 现在做到哪里、证据在哪 | 本地 `STATUS.md` | 长期任务定义、执行流水账 |
| 怎样验证、哪些层级 | [测试策略](test-strategy.md)、[操作验收单](manual-acceptance.md) | 通过数字（只在 STATUS） |
| 实测性能与复现 | [性能报告](performance-v2.md) | 无依据的全应用速度保证 |
| 使用/构建/打包 | [README](../README.md)、[中文指南](manual-zh.txt) | 管理/设计细节 |
| 历史对照与经验 | [基线](baseline-v2.md)、[视觉证据](../design-qa.md)、[旧计划](history/plan-2026-10-11.md)、本地 `PROGRESS.md` | 当下下一步 |

## 文档套餐与项目协议

按中型本地桌面项目裁剪：单用户但有计量不变量、数据迁移、进程执行、升级 IPC 和回滚边界。使用 generic 协议，未检测到 .dev-compass/project.json 或项目级 APMD；保留根 SPEC/PLAN/STATUS 和已有文档路径，不套用 DCPM parser。

产品、行为、架构、计划/WU、状态为核心职责；数据模型、升级协议、界面、安装和测试因实际边界独立。领域词汇较少放 PRD/数据模型，导航放交互约定，安全与部署放相关主题；暂不另建空 DOMAIN/IA/SECURITY/DEPLOYMENT/ADR/Runbook。没有云 API 或 AI 系统，不创建对应文档。

## 维护与追踪策略

| 变化 | 更新事实源 |
| --- | --- |
| 产品范围/规则 | PRD + SPEC，必要时设计 |
| 可见行为/界面状态 | SPEC + 交互 + 验收单 |
| 数据/协议/兼容 | data-model / upgrade-protocol + 迁移验收 |
| 组件/生命周期 | architecture |
| 任务范围/拆分/依赖 | PLAN + WU；稳定 ID 不重编号 |
| 当前成果/失败/证据/下一步 | 只改 STATUS；经验浓缩到 PROGRESS |
| 工具链/安装/签名 | installer-evaluation + README；实际结果回 STATUS |

详细证据放 artifacts 独立批次，首次失败保留；不会把测试步骤写成通过或把目标写成实现。历史报告可以保存当时数字，须明确日期/阶段，不作为当前状态。

SPEC.md 作为行为规格随源码公开；PLAN/STATUS/PROGRESS 及 docs/history/local 快照继续本地保留，协作进度不自动公开。

## 迁移与检查

[本次来源章节、职责和归档映射](history/doc-migration-2026-10-12.md)记录全项目整理范围。检查 skill generic 结构、链接/锚点/围栏、WU 与 FR 对应、原文 SHA256 和实现未变；结果只见 STATUS。原样快照不删除旧证据，未覆盖的历史内部路径仍按原根目录解释。
