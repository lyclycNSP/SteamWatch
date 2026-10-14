# 全项目文档重组记录（2026-10-12）

任务 DOC-202；这是迁移与来源报告，当前状态只见 [STATUS](../../STATUS.md)。本地桌面、多层计量/持久化/执行/升级边界，采用中型 generic 套餐，保留既有路径。不启用 DCPM、不改应用行为，不执行提交/推送/安装或发布。

## 发现与处理原则

旧 SPEC/交互前文仍有独立规则标签而末尾已取消；重设计混合目标与后加实现；架构夹杂 UI/字段/测试流水；PLAN 的单一 RUNTIME 范围难以交接。依已有用户决定消除冲突，无需新增产品决定。

产品由 PRD、行为由 SPEC、实现由架构、字段/IPC 由专题、阶段/依赖由 PLAN、实施由 WU、动态进度由 STATUS 负责。领域/安全/部署不为空模板拆文件。新 PRD 承接原 SPEC 1/2 和设计范围，未增加产品功能。

## 分批实施与回滚

先保存来源快照/源码哈希，再分产品合同、架构交互、任务、入口整理，最后核对链接和覆盖。源文件未批量移动或删除；17 份原文按字节保留在 local 目录，JSON 清单有长度/SHA256。需回看使用快照，内部原链接按原根目录解释；回滚也必须先保存此后新改动，不盲目覆盖。

## 文件清单与事实源

| 来源 | 读者/职责 | 动作 | 当前落点 |
| --- | --- | --- | --- |
| README.md | 开发/使用入口 | 合并 | [README.md](../../README.md) |
| SPEC.md | 可观察行为 | 拆分 | [SPEC.md](../../SPEC.md) |
| PLAN.md | 阶段/任务定义 | 拆分 | [PLAN.md](../../PLAN.md) |
| STATUS.md | 动态状态 | 保留 | [STATUS.md](../../STATUS.md) |
| PROGRESS.md | 经验 | 保留 | [PROGRESS.md](../../PROGRESS.md) |
| design-qa.md | 历史视觉证据 | 保留/历史标记 | [design-qa.md](../../design-qa.md) |
| .gitignore | 本地/公开边界 | 保留 | [.gitignore](../../.gitignore) |
| docs/architecture.md | 当前实现 | 拆分 | [docs/architecture.md](../architecture.md) |
| docs/baseline-v2.md | 历史能力/样本 | 保留/历史标记 | [docs/baseline-v2.md](../baseline-v2.md) |
| docs/installer-evaluation.md | 交付约束/历史选型 | 合并 | [docs/installer-evaluation.md](../installer-evaluation.md) |
| docs/manual-acceptance.md | 验收步骤 | 合并 | [docs/manual-acceptance.md](../manual-acceptance.md) |
| docs/manual-zh.txt | 用户操作 | 保留 | [docs/manual-zh.txt](../manual-zh.txt) |
| docs/performance-v2.md | 有日期的测量证据 | 保留 | [docs/performance-v2.md](../performance-v2.md) |
| docs/README.md | 文档事实源导航 | 合并 | [docs/README.md](../README.md) |
| docs/redesign-v2.md | 已确认设计决策 | 合并 | [docs/redesign-v2.md](../redesign-v2.md) |
| docs/design/interaction-contract.md | 现行交互合同 | 合并 | [docs/design/interaction-contract.md](../design/interaction-contract.md) |
| tests/SteamWatch.Tests/Fixtures/V2Baseline/README.md | 合成夹具来源 | 保留 | [tests/SteamWatch.Tests/Fixtures/V2Baseline/README.md](../../tests/SteamWatch.Tests/Fixtures/V2Baseline/README.md) |


## 来源章节到目标事实源

所有源章节均保留原样快照；本表给出当前责任落点，旧方案仅进历史，不继续执行。验证方式为逐字节快照核对、章节清单覆盖和当前链接/任务/合同检查。没有标题的文件按全文登记。

| 来源章节 | 处理 | 当前责任位置 | 原文快照 |
| --- | --- | --- | --- |
| README.md · SteamWatch | 合并；旧状态/方案留历史 | [README.md](../../README.md) | [原文](local/2026-10-12-skill/README.md.txt) |
| README.md · 给使用者 | 合并；旧状态/方案留历史 | [README.md](../../README.md) | [原文](local/2026-10-12-skill/README.md.txt) |
| README.md · 主要功能 | 合并；旧状态/方案留历史 | [README.md](../../README.md) | [原文](local/2026-10-12-skill/README.md.txt) |
| README.md · 下载和启动 | 合并；旧状态/方案留历史 | [README.md](../../README.md) | [原文](local/2026-10-12-skill/README.md.txt) |
| README.md · 使用建议 | 合并；旧状态/方案留历史 | [README.md](../../README.md) | [原文](local/2026-10-12-skill/README.md.txt) |
| README.md · 数据保存 | 合并；旧状态/方案留历史 | [README.md](../../README.md) | [原文](local/2026-10-12-skill/README.md.txt) |
| README.md · 给开发者 | 合并；旧状态/方案留历史 | [README.md](../../README.md) | [原文](local/2026-10-12-skill/README.md.txt) |
| README.md · 技术栈 | 合并；旧状态/方案留历史 | [README.md](../../README.md) | [原文](local/2026-10-12-skill/README.md.txt) |
| README.md · 项目结构 | 合并；旧状态/方案留历史 | [README.md](../../README.md) | [原文](local/2026-10-12-skill/README.md.txt) |
| README.md · 本地构建 | 合并；旧状态/方案留历史 | [README.md](../../README.md) | [原文](local/2026-10-12-skill/README.md.txt) |
| README.md · 运行测试 | 合并；旧状态/方案留历史 | [README.md](../../README.md) | [原文](local/2026-10-12-skill/README.md.txt) |
| README.md · 可选的真实 Windows 受控检查 | 合并；旧状态/方案留历史 | [README.md](../../README.md) | [原文](local/2026-10-12-skill/README.md.txt) |
| README.md · 约一分钟，启动并关闭可丢弃的测试进程，不操作真实游戏或开机自启。 | 合并；旧状态/方案留历史 | [README.md](../../README.md) | [原文](local/2026-10-12-skill/README.md.txt) |
| README.md · 使用新目录复制绿色版进行 UI/文件选择器检查；需当前用户没有 SteamWatch 自启项。 | 合并；旧状态/方案留历史 | [README.md](../../README.md) | [原文](local/2026-10-12-skill/README.md.txt) |
| README.md · 生成发布包 | 合并；旧状态/方案留历史 | [README.md](../../README.md) | [原文](local/2026-10-12-skill/README.md.txt) |
| README.md · GitHub 上传 | 合并；旧状态/方案留历史 | [README.md](../../README.md) | [原文](local/2026-10-12-skill/README.md.txt) |
| SPEC.md · SteamWatch 需求规格 | 拆分；旧状态/方案留历史 | [SPEC.md](../../SPEC.md) | [原文](local/2026-10-12-skill/SPEC.md.txt) |
| SPEC.md · 1. 产品目标 | 拆分；旧状态/方案留历史 | [docs/product/PRD.md](../product/PRD.md) | [原文](local/2026-10-12-skill/SPEC.md.txt) |
| SPEC.md · 2. 目标平台 | 拆分；旧状态/方案留历史 | [docs/product/PRD.md](../product/PRD.md) | [原文](local/2026-10-12-skill/SPEC.md.txt) |
| SPEC.md · 3. 核心功能 | 拆分；旧状态/方案留历史 | [SPEC.md](../../SPEC.md) | [原文](local/2026-10-12-skill/SPEC.md.txt) |
| SPEC.md · 4. 数据规则 | 拆分；旧状态/方案留历史 | [docs/architecture/data-model.md](../architecture/data-model.md) | [原文](local/2026-10-12-skill/SPEC.md.txt) |
| SPEC.md · 5. 安全与可靠性 | 拆分；旧状态/方案留历史 | [SPEC.md](../../SPEC.md) | [原文](local/2026-10-12-skill/SPEC.md.txt) |
| SPEC.md · 6. 用户体验 | 拆分；旧状态/方案留历史 | [SPEC.md](../../SPEC.md) | [原文](local/2026-10-12-skill/SPEC.md.txt) |
| SPEC.md · 7. 全局执行与范围边界 | 拆分；旧状态/方案留历史 | [docs/product/PRD.md](../product/PRD.md) | [原文](local/2026-10-12-skill/SPEC.md.txt) |
| SPEC.md · 8. 估算、维护和升级契约 | 拆分；旧状态/方案留历史 | [SPEC.md](../../SPEC.md) | [原文](local/2026-10-12-skill/SPEC.md.txt) |
| SPEC.md · 9. 2.0 待实现目标 | 拆分；旧状态/方案留历史 | [SPEC.md](../../SPEC.md) | [原文](local/2026-10-12-skill/SPEC.md.txt) |
| SPEC.md · 退出与设置草稿协调 | 拆分；旧状态/方案留历史 | [SPEC.md](../../SPEC.md) | [原文](local/2026-10-12-skill/SPEC.md.txt) |
| SPEC.md · 游戏库统一管理限额 | 拆分；旧状态/方案留历史 | [SPEC.md](../../SPEC.md) | [原文](local/2026-10-12-skill/SPEC.md.txt) |
| PLAN.md · SteamWatch 2.0 执行计划 | 拆分；旧状态/方案留历史 | [PLAN.md](../../PLAN.md) | [原文](local/2026-10-12-skill/PLAN.md.txt) |
| PLAN.md · 目标与实施边界 | 拆分；旧状态/方案留历史 | [PLAN.md](../../PLAN.md) | [原文](local/2026-10-12-skill/PLAN.md.txt) |
| PLAN.md · 阶段、里程碑与依赖 | 拆分；旧状态/方案留历史 | [PLAN.md](../../PLAN.md) | [原文](local/2026-10-12-skill/PLAN.md.txt) |
| PLAN.md · WU 索引与任务定义 | 拆分；旧状态/方案留历史 | [docs/planning/work-units.md](../planning/work-units.md) | [原文](local/2026-10-12-skill/PLAN.md.txt) |
| PLAN.md · 准备与基线 | 拆分；旧状态/方案留历史 | [docs/planning/work-units.md](../planning/work-units.md) | [原文](local/2026-10-12-skill/PLAN.md.txt) |
| PLAN.md · 页面与运行边界 | 拆分；旧状态/方案留历史 | [docs/planning/work-units.md](../planning/work-units.md) | [原文](local/2026-10-12-skill/PLAN.md.txt) |
| PLAN.md · 安装交付 | 拆分；旧状态/方案留历史 | [docs/planning/work-units.md](../planning/work-units.md) | [原文](local/2026-10-12-skill/PLAN.md.txt) |
| PLAN.md · 集成验收与候选包 | 拆分；旧状态/方案留历史 | [docs/planning/work-units.md](../planning/work-units.md) | [原文](local/2026-10-12-skill/PLAN.md.txt) |
| PLAN.md · 旧任务与验收承接 | 拆分；旧状态/方案留历史 | [PLAN.md](../../PLAN.md) | [原文](local/2026-10-12-skill/PLAN.md.txt) |
| PLAN.md · 验证命令与证据要求 | 拆分；旧状态/方案留历史 | [PLAN.md](../../PLAN.md) | [原文](local/2026-10-12-skill/PLAN.md.txt) |
| PLAN.md · 发布与收尾条件 | 拆分；旧状态/方案留历史 | [PLAN.md](../../PLAN.md) | [原文](local/2026-10-12-skill/PLAN.md.txt) |
| PLAN.md · 决策与历史说明 | 拆分；旧状态/方案留历史 | [PLAN.md](../../PLAN.md) | [原文](local/2026-10-12-skill/PLAN.md.txt) |
| STATUS.md · SteamWatch 当前进度 | 保留；旧状态/方案留历史 | [STATUS.md](../../STATUS.md) | [原文](local/2026-10-12-skill/STATUS.md.txt) |
| STATUS.md · 当前结论 | 保留；旧状态/方案留历史 | [STATUS.md](../../STATUS.md) | [原文](local/2026-10-12-skill/STATUS.md.txt) |
| STATUS.md · 最近一批实际推进 | 保留；旧状态/方案留历史 | [STATUS.md](../../STATUS.md) | [原文](local/2026-10-12-skill/STATUS.md.txt) |
| STATUS.md · 下一步 | 保留；旧状态/方案留历史 | [STATUS.md](../../STATUS.md) | [原文](local/2026-10-12-skill/STATUS.md.txt) |
| STATUS.md · 阻塞与外部依赖 | 保留；旧状态/方案留历史 | [STATUS.md](../../STATUS.md) | [原文](local/2026-10-12-skill/STATUS.md.txt) |
| STATUS.md · WU 看板 | 保留；旧状态/方案留历史 | [STATUS.md](../../STATUS.md) | [原文](local/2026-10-12-skill/STATUS.md.txt) |
| STATUS.md · 验证证据与交付入口 | 保留；旧状态/方案留历史 | [STATUS.md](../../STATUS.md) | [原文](local/2026-10-12-skill/STATUS.md.txt) |
| STATUS.md · 历史与维护规则 | 保留；旧状态/方案留历史 | [STATUS.md](../../STATUS.md) | [原文](local/2026-10-12-skill/STATUS.md.txt) |
| PROGRESS.md · 开发经验 | 保留；旧状态/方案留历史 | [PROGRESS.md](../../PROGRESS.md) | [原文](local/2026-10-12-skill/PROGRESS.md.txt) |
| design-qa.md · 第一批 WinUI 视觉检查（2026-10-12） | 保留/历史标记；旧状态/方案留历史 | [design-qa.md](../../design-qa.md) | [原文](local/2026-10-12-skill/design-qa.md.txt) |
| design-qa.md · 比较材料 | 保留/历史标记；旧状态/方案留历史 | [design-qa.md](../../design-qa.md) | [原文](local/2026-10-12-skill/design-qa.md.txt) |
| design-qa.md · 已修复 | 保留/历史标记；旧状态/方案留历史 | [design-qa.md](../../design-qa.md) | [原文](local/2026-10-12-skill/design-qa.md.txt) |
| design-qa.md · 仍需收敛 | 保留/历史标记；旧状态/方案留历史 | [design-qa.md](../../design-qa.md) | [原文](local/2026-10-12-skill/design-qa.md.txt) |
| design-qa.md · 五项表面检查 | 保留/历史标记；旧状态/方案留历史 | [design-qa.md](../../design-qa.md) | [原文](local/2026-10-12-skill/design-qa.md.txt) |
| design-qa.md · 验证边界 | 保留/历史标记；旧状态/方案留历史 | [design-qa.md](../../design-qa.md) | [原文](local/2026-10-12-skill/design-qa.md.txt) |
| design-qa.md · 第三批补充：设置（2026-10-12） | 保留/历史标记；旧状态/方案留历史 | [design-qa.md](../../design-qa.md) | [原文](local/2026-10-12-skill/design-qa.md.txt) |
| design-qa.md · 设置组件重构复核（2026-10-12） | 保留/历史标记；旧状态/方案留历史 | [design-qa.md](../../design-qa.md) | [原文](local/2026-10-12-skill/design-qa.md.txt) |
| design-qa.md · 设置/帮助统一复核（2026-10-12） | 保留/历史标记；旧状态/方案留历史 | [design-qa.md](../../design-qa.md) | [原文](local/2026-10-12-skill/design-qa.md.txt) |
| .gitignore · Build outputs | 保留；旧状态/方案留历史 | [.gitignore](../../.gitignore) | [原文](local/2026-10-12-skill/.gitignore.txt) |
| .gitignore · User/runtime data | 保留；旧状态/方案留历史 | [.gitignore](../../.gitignore) | [原文](local/2026-10-12-skill/.gitignore.txt) |
| .gitignore · Packaging output | 保留；旧状态/方案留历史 | [.gitignore](../../.gitignore) | [原文](local/2026-10-12-skill/.gitignore.txt) |
| .gitignore · Local planning board files | 保留；旧状态/方案留历史 | [.gitignore](../../.gitignore) | [原文](local/2026-10-12-skill/.gitignore.txt) |
| .gitignore · Local execution record | 保留；旧状态/方案留历史 | [.gitignore](../../.gitignore) | [原文](local/2026-10-12-skill/.gitignore.txt) |
| .gitignore · History of local planning files (preserve their existing local-only policy) | 保留；旧状态/方案留历史 | [.gitignore](../../.gitignore) | [原文](local/2026-10-12-skill/.gitignore.txt) |
| docs/architecture.md · SteamWatch 运行设计 | 拆分；旧状态/方案留历史 | [docs/architecture.md](../architecture.md) | [原文](local/2026-10-12-skill/docs__architecture.md.txt) |
| docs/architecture.md · 状态边界 | 拆分；旧状态/方案留历史 | [docs/architecture.md](../architecture.md) | [原文](local/2026-10-12-skill/docs__architecture.md.txt) |
| docs/architecture.md · 计量与持久化 | 拆分；旧状态/方案留历史 | [docs/architecture/data-model.md](../architecture/data-model.md) | [原文](local/2026-10-12-skill/docs__architecture.md.txt) |
| docs/architecture.md · 规则与执行 | 拆分；旧状态/方案留历史 | [docs/architecture.md](../architecture.md) | [原文](local/2026-10-12-skill/docs__architecture.md.txt) |
| docs/architecture.md · 迁移与恢复 | 拆分；旧状态/方案留历史 | [docs/architecture/data-model.md](../architecture/data-model.md) | [原文](local/2026-10-12-skill/docs__architecture.md.txt) |
| docs/architecture.md · 交付 | 拆分；旧状态/方案留历史 | [docs/architecture.md](../architecture.md) | [原文](local/2026-10-12-skill/docs__architecture.md.txt) |
| docs/architecture.md · 可复现的受控 Windows 检查 | 拆分；旧状态/方案留历史 | [docs/test-strategy.md](../test-strategy.md) | [原文](local/2026-10-12-skill/docs__architecture.md.txt) |
| docs/architecture.md · 绿色版交付布局 | 拆分；旧状态/方案留历史 | [docs/architecture.md](../architecture.md) | [原文](local/2026-10-12-skill/docs__architecture.md.txt) |
| docs/architecture.md · 设置与帮助的样式统一 | 拆分；旧状态/方案留历史 | [docs/design/interaction-contract.md](../design/interaction-contract.md) | [原文](local/2026-10-12-skill/docs__architecture.md.txt) |
| docs/architecture.md · 同代展示快照与操作生命周期 | 拆分；旧状态/方案留历史 | [docs/architecture.md](../architecture.md) | [原文](local/2026-10-12-skill/docs__architecture.md.txt) |
| docs/architecture.md · 页面挂载与通知订阅 | 拆分；旧状态/方案留历史 | [docs/architecture.md](../architecture.md) | [原文](local/2026-10-12-skill/docs__architecture.md.txt) |
| docs/architecture.md · 规则与周统计的历史数据遍历 | 拆分；旧状态/方案留历史 | [docs/architecture.md](../architecture.md) | [原文](local/2026-10-12-skill/docs__architecture.md.txt) |
| docs/architecture.md · 展示账本与流式保存 | 拆分；旧状态/方案留历史 | [docs/architecture/data-model.md](../architecture/data-model.md) | [原文](local/2026-10-12-skill/docs__architecture.md.txt) |
| docs/architecture.md · 升级退出的恢复异常边界 | 拆分；旧状态/方案留历史 | [docs/architecture/upgrade-protocol.md](../architecture/upgrade-protocol.md) | [原文](local/2026-10-12-skill/docs__architecture.md.txt) |
| docs/architecture.md · 七日图表复用同代实测账本 | 拆分；旧状态/方案留历史 | [docs/architecture.md](../architecture.md) | [原文](local/2026-10-12-skill/docs__architecture.md.txt) |
| docs/baseline-v2.md · 2.0 重写基线与功能迁移对照 | 保留/历史标记；旧状态/方案留历史 | [docs/baseline-v2.md](../baseline-v2.md) | [原文](local/2026-10-12-skill/docs__baseline-v2.md.txt) |
| docs/baseline-v2.md · 基线与证据 | 保留/历史标记；旧状态/方案留历史 | [docs/baseline-v2.md](../baseline-v2.md) | [原文](local/2026-10-12-skill/docs__baseline-v2.md.txt) |
| docs/baseline-v2.md · 功能迁移矩阵 | 保留/历史标记；旧状态/方案留历史 | [docs/baseline-v2.md](../baseline-v2.md) | [原文](local/2026-10-12-skill/docs__baseline-v2.md.txt) |
| docs/baseline-v2.md · 敏感动作矩阵 | 保留/历史标记；旧状态/方案留历史 | [docs/baseline-v2.md](../baseline-v2.md) | [原文](local/2026-10-12-skill/docs__baseline-v2.md.txt) |
| docs/baseline-v2.md · 合成兼容样本 | 保留/历史标记；旧状态/方案留历史 | [docs/baseline-v2.md](../baseline-v2.md) | [原文](local/2026-10-12-skill/docs__baseline-v2.md.txt) |
| docs/baseline-v2.md · 迁移前发现的差异 | 保留/历史标记；旧状态/方案留历史 | [docs/baseline-v2.md](../baseline-v2.md) | [原文](local/2026-10-12-skill/docs__baseline-v2.md.txt) |
| docs/installer-evaluation.md · 安装器选型评估 | 合并；旧状态/方案留历史 | [docs/installer-evaluation.md](../installer-evaluation.md) | [原文](local/2026-10-12-skill/docs__installer-evaluation.md.txt) |
| docs/installer-evaluation.md · 结论 | 合并；旧状态/方案留历史 | [docs/installer-evaluation.md](../installer-evaluation.md) | [原文](local/2026-10-12-skill/docs__installer-evaluation.md.txt) |
| docs/installer-evaluation.md · 对比与维护边界 | 合并；旧状态/方案留历史 | [docs/installer-evaluation.md](../installer-evaluation.md) | [原文](local/2026-10-12-skill/docs__installer-evaluation.md.txt) |
| docs/installer-evaluation.md · 后续采用条件 | 合并；旧状态/方案留历史 | [docs/installer-evaluation.md](../installer-evaluation.md) | [原文](local/2026-10-12-skill/docs__installer-evaluation.md.txt) |
| docs/installer-evaluation.md · 2.0 验证包前置检查（2026-10-12） | 合并；旧状态/方案留历史 | [docs/installer-evaluation.md](../installer-evaluation.md) | [原文](local/2026-10-12-skill/docs__installer-evaluation.md.txt) |
| docs/installer-evaluation.md · 官方依据 | 合并；旧状态/方案留历史 | [docs/installer-evaluation.md](../installer-evaluation.md) | [原文](local/2026-10-12-skill/docs__installer-evaluation.md.txt) |
| docs/manual-acceptance.md · SteamWatch 手工验收 | 合并；旧状态/方案留历史 | [docs/manual-acceptance.md](../manual-acceptance.md) | [原文](local/2026-10-12-skill/docs__manual-acceptance.md.txt) |
| docs/manual-acceptance.md · 启动与目录 | 合并；旧状态/方案留历史 | [docs/manual-acceptance.md](../manual-acceptance.md) | [原文](local/2026-10-12-skill/docs__manual-acceptance.md.txt) |
| docs/manual-acceptance.md · 计时与界面 | 合并；旧状态/方案留历史 | [docs/manual-acceptance.md](../manual-acceptance.md) | [原文](local/2026-10-12-skill/docs__manual-acceptance.md.txt) |
| docs/manual-acceptance.md · 规则、通知和关闭 | 合并；旧状态/方案留历史 | [docs/manual-acceptance.md](../manual-acceptance.md) | [原文](local/2026-10-12-skill/docs__manual-acceptance.md.txt) |
| docs/manual-acceptance.md · 备份与升级 | 合并；旧状态/方案留历史 | [docs/manual-acceptance.md](../manual-acceptance.md) | [原文](local/2026-10-12-skill/docs__manual-acceptance.md.txt) |
| docs/manual-acceptance.md · 基础回归证据边界 | 合并；旧状态/方案留历史 | [docs/manual-acceptance.md](../manual-acceptance.md) | [原文](local/2026-10-12-skill/docs__manual-acceptance.md.txt) |
| docs/manual-acceptance.md · 2.0 界面专项：WU-ACCEPT-201 | 合并；旧状态/方案留历史 | [docs/manual-acceptance.md](../manual-acceptance.md) | [原文](local/2026-10-12-skill/docs__manual-acceptance.md.txt) |
| docs/manual-acceptance.md · 2.0 计时与限制专项：WU-ACCEPT-202 | 合并；旧状态/方案留历史 | [docs/manual-acceptance.md](../manual-acceptance.md) | [原文](local/2026-10-12-skill/docs__manual-acceptance.md.txt) |
| docs/manual-acceptance.md · 2.0 安装专项：WU-ACCEPT-203 | 合并；旧状态/方案留历史 | [docs/manual-acceptance.md](../manual-acceptance.md) | [原文](local/2026-10-12-skill/docs__manual-acceptance.md.txt) |
| docs/manual-acceptance.md · 每次验收的记录要求 | 合并；旧状态/方案留历史 | [docs/manual-acceptance.md](../manual-acceptance.md) | [原文](local/2026-10-12-skill/docs__manual-acceptance.md.txt) |
| docs/manual-acceptance.md · 绿色版入口与搬迁 | 合并；旧状态/方案留历史 | [docs/manual-acceptance.md](../manual-acceptance.md) | [原文](local/2026-10-12-skill/docs__manual-acceptance.md.txt) |
| docs/manual-acceptance.md · 设置草稿退出回归 | 合并；旧状态/方案留历史 | [docs/manual-acceptance.md](../manual-acceptance.md) | [原文](local/2026-10-12-skill/docs__manual-acceptance.md.txt) |
| docs/manual-acceptance.md · 帮助与规则布局 | 合并；旧状态/方案留历史 | [docs/manual-acceptance.md](../manual-acceptance.md) | [原文](local/2026-10-12-skill/docs__manual-acceptance.md.txt) |
| docs/manual-acceptance.md · 同代快照与操作生命周期专项 | 合并；旧状态/方案留历史 | [docs/manual-acceptance.md](../manual-acceptance.md) | [原文](local/2026-10-12-skill/docs__manual-acceptance.md.txt) |
| docs/manual-acceptance.md · 共享命令与页面附着专项 | 合并；旧状态/方案留历史 | [docs/manual-acceptance.md](../manual-acceptance.md) | [原文](local/2026-10-12-skill/docs__manual-acceptance.md.txt) |
| docs/manual-zh.txt · 全文 | 保留；旧状态/方案留历史 | [docs/manual-zh.txt](../manual-zh.txt) | [原文](local/2026-10-12-skill/docs__manual-zh.txt.txt) |
| docs/performance-v2.md · 运行层性能对照 | 保留；旧状态/方案留历史 | [docs/performance-v2.md](../performance-v2.md) | [原文](local/2026-10-12-skill/docs__performance-v2.md.txt) |
| docs/performance-v2.md · 数据与复现 | 保留；旧状态/方案留历史 | [docs/performance-v2.md](../performance-v2.md) | [原文](local/2026-10-12-skill/docs__performance-v2.md.txt) |
| docs/performance-v2.md · 实测结果 | 保留；旧状态/方案留历史 | [docs/performance-v2.md](../performance-v2.md) | [原文](local/2026-10-12-skill/docs__performance-v2.md.txt) |
| docs/performance-v2.md · 解释边界与后续 | 保留；旧状态/方案留历史 | [docs/performance-v2.md](../performance-v2.md) | [原文](local/2026-10-12-skill/docs__performance-v2.md.txt) |
| docs/performance-v2.md · 账本展示投影与保存：后续实施 | 保留；旧状态/方案留历史 | [docs/performance-v2.md](../performance-v2.md) | [原文](local/2026-10-12-skill/docs__performance-v2.md.txt) |
| docs/performance-v2.md · 同代图表复用与原生测量 | 保留；旧状态/方案留历史 | [docs/performance-v2.md](../performance-v2.md) | [原文](local/2026-10-12-skill/docs__performance-v2.md.txt) |
| docs/README.md · SteamWatch 文档入口 | 合并；旧状态/方案留历史 | [docs/README.md](../README.md) | [原文](local/2026-10-12-skill/docs__README.md.txt) |
| docs/README.md · 按问题找文档 | 合并；旧状态/方案留历史 | [docs/README.md](../README.md) | [原文](local/2026-10-12-skill/docs__README.md.txt) |
| docs/README.md · 这次整理了什么 | 合并；旧状态/方案留历史 | [docs/README.md](../README.md) | [原文](local/2026-10-12-skill/docs__README.md.txt) |
| docs/README.md · 后续更新规则 | 合并；旧状态/方案留历史 | [docs/README.md](../README.md) | [原文](local/2026-10-12-skill/docs__README.md.txt) |
| docs/README.md · 文档检查边界 | 合并；旧状态/方案留历史 | [docs/README.md](../README.md) | [原文](local/2026-10-12-skill/docs__README.md.txt) |
| docs/redesign-v2.md · SteamWatch 2.0 重设计方案 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 1. 总体决定 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 2. 设计启动时的依据与问题（1.2.0 对照） | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 3. 基础功能不变的合同 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 4. 信息架构 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 4.1 应用外壳 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 4.2 游戏库：默认首屏 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 4.3 限额规则 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 4.4 统计 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 4.5 设置与维护 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 4.6 托盘、提醒及首次启动 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 5. 统一视觉规范 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 6. 目标代码结构与运行边界 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 7. 安装、升级与卸载 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 7.1 分发选择 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 7.2 安装向导 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 7.3 升级合同 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 7.4 卸载与发布可信度 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 8. 产品完成标准与执行入口 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 9. 依据与参考 | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/redesign-v2.md · 游戏库统一入口修订（2026-10-12） | 合并；旧状态/方案留历史 | [docs/redesign-v2.md](../redesign-v2.md) | [原文](local/2026-10-12-skill/docs__redesign-v2.md.txt) |
| docs/design/interaction-contract.md · 2.0 界面交互与状态约定 | 合并；旧状态/方案留历史 | [docs/design/interaction-contract.md](../design/interaction-contract.md) | [原文](local/2026-10-12-skill/docs__design__interaction-contract.md.txt) |
| docs/design/interaction-contract.md · 视觉稿与选择记录 | 合并；旧状态/方案留历史 | [docs/design/interaction-contract.md](../design/interaction-contract.md) | [原文](local/2026-10-12-skill/docs__design__interaction-contract.md.txt) |
| docs/design/interaction-contract.md · 全部游戏总限额的呈现与编辑 | 合并；旧状态/方案留历史 | [docs/design/interaction-contract.md](../design/interaction-contract.md) | [原文](local/2026-10-12-skill/docs__design__interaction-contract.md.txt) |
| docs/design/interaction-contract.md · 四类页面 | 合并；旧状态/方案留历史 | [docs/design/interaction-contract.md](../design/interaction-contract.md) | [原文](local/2026-10-12-skill/docs__design__interaction-contract.md.txt) |
| docs/design/interaction-contract.md · 通用状态与反馈 | 合并；旧状态/方案留历史 | [docs/design/interaction-contract.md](../design/interaction-contract.md) | [原文](local/2026-10-12-skill/docs__design__interaction-contract.md.txt) |
| docs/design/interaction-contract.md · 样式与适配基准 | 合并；旧状态/方案留历史 | [docs/design/interaction-contract.md](../design/interaction-contract.md) | [原文](local/2026-10-12-skill/docs__design__interaction-contract.md.txt) |
| docs/design/interaction-contract.md · 进入生产 UI 实施前 | 合并；旧状态/方案留历史 | [docs/design/interaction-contract.md](../design/interaction-contract.md) | [原文](local/2026-10-12-skill/docs__design__interaction-contract.md.txt) |
| docs/design/interaction-contract.md · 第一批实现约束 | 合并；旧状态/方案留历史 | [docs/design/interaction-contract.md](../design/interaction-contract.md) | [原文](local/2026-10-12-skill/docs__design__interaction-contract.md.txt) |
| docs/design/interaction-contract.md · 第二批增量：统计与样式 | 合并；旧状态/方案留历史 | [docs/design/interaction-contract.md](../design/interaction-contract.md) | [原文](local/2026-10-12-skill/docs__design__interaction-contract.md.txt) |
| docs/design/interaction-contract.md · 第三批增量：设置草稿与导航 | 合并；旧状态/方案留历史 | [docs/design/interaction-contract.md](../design/interaction-contract.md) | [原文](local/2026-10-12-skill/docs__design__interaction-contract.md.txt) |
| docs/design/interaction-contract.md · 设置与帮助统一样式补充 | 合并；旧状态/方案留历史 | [docs/design/interaction-contract.md](../design/interaction-contract.md) | [原文](local/2026-10-12-skill/docs__design__interaction-contract.md.txt) |
| docs/design/interaction-contract.md · 设置草稿与退出 | 合并；旧状态/方案留历史 | [docs/design/interaction-contract.md](../design/interaction-contract.md) | [原文](local/2026-10-12-skill/docs__design__interaction-contract.md.txt) |
| docs/design/interaction-contract.md · 帮助与规则窄窗修订 | 合并；旧状态/方案留历史 | [docs/design/interaction-contract.md](../design/interaction-contract.md) | [原文](local/2026-10-12-skill/docs__design__interaction-contract.md.txt) |
| docs/design/interaction-contract.md · 弹窗正文排版补充（2026-10-12） | 合并；旧状态/方案留历史 | [docs/design/interaction-contract.md](../design/interaction-contract.md) | [原文](local/2026-10-12-skill/docs__design__interaction-contract.md.txt) |
| docs/design/interaction-contract.md · 游戏页拆分合同（2026-10-12） | 合并；旧状态/方案留历史 | [docs/design/interaction-contract.md](../design/interaction-contract.md) | [原文](local/2026-10-12-skill/docs__design__interaction-contract.md.txt) |
| tests/SteamWatch.Tests/Fixtures/V2Baseline/README.md · 2.0 兼容基线夹具 | 保留；旧状态/方案留历史 | [tests/SteamWatch.Tests/Fixtures/V2Baseline/README.md](../../tests/SteamWatch.Tests/Fixtures/V2Baseline/README.md) | [原文](local/2026-10-12-skill/tests__SteamWatch.Tests__Fixtures__V2Baseline__README.md.txt) |


## WU 拆分与兼容

原 17 个 ID 全保留，不将历史完成重新标为未开始。新增 DOC-202 是本次整理；RUNTIME-201 原超大合同由现有实现/集成承接，活跃 ticks 故障恢复分给 RUNTIME-202，后台/托盘长时成本分给 RUNTIME-203。旧代码/微基准/原生目录 dirty 结果留作输入，不计作新任务完成。合计 20 个 ID 的动态状态只在 STATUS。

当前实现是自制安装器，目标 Inno；历史“暂不迁移”只对历史轮次有效。旧双标签视觉图/基线表仍保留作为历史素材，现行游戏库合同一致。数据字段/协议字节未修改，历史和当前行为不靠文档重组互相替代。

## 验证与限制

审计覆盖当前文档、本地路径/锚点、围栏、任务身份/依赖、FR 对应、历史字节与实现文件未变。旧历史原路径和 artifacts 不作为当前规范重新编写；用户指南/夹具说明复核后保留。检查结果及证据入口只写 STATUS；纯文档整理不重跑应用/桌面测试，不继承为本批通过。
