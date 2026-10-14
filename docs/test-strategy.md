# 验证策略与证据边界

本文件维护验证层级、质量门和证据要求，不保存动态通过数量。逐项操作见 [手工验收](manual-acceptance.md)，当前结果见 本地 `STATUS.md`。

## 层级与责任

| 层级 | 入口 / 验证对象 | 不能证明 |
| --- | --- | --- |
| 确定性单测 | MSTest：账本/规则/存储、纯展示/草稿/命令状态、命名管道 | 真实窗口可用、真实游戏和 OS 休眠 |
| 构建与包 | Debug x64、发布脚本、ZIP/载荷/指南/哈希/程序集一致性 | 安装成功、签名完成或干净机可运行 |
| 交付回归 | test-release / verify-delivery；清单/ZIP 全文件、版本、无证书拒绝、真实文件占用后的逐文件补偿及篡改拒绝；实际 build-installer 编译失败检查 | 无证书时不得声称真实签名或时间戳完成；逐文件补偿不证明断电批次原子性 |
| 受控 Windows 进程 | scripts/test-runtime.ps1，生产 WMI/活动时钟，独立 Stopwatch | Steam 游戏、反作弊、全屏及跨夜 |
| 原生桌面 | scripts/test-desktop.ps1，WinUI/UIA、真实鼠标、系统选择器、账本锁/管道断开 | 所有主题/DPI/读屏、活跃游戏恢复、安装系统闭环 |
| 同源 UI 验收宿主 | scripts/test-ui-acceptance.ps1；固定编译开关、独立产品版本和目录；应用内浅深主题、四页两种尺寸、真实 Tab 焦点抽样、同一 MainPage 重挂载/过期回调；空白截图拒绝 | 最终交付二进制、系统深色/原生标题栏、高对比度、实际目标 DPI、完整键盘旅程与真人读屏；复现见 [宿主说明](../tools/SteamWatch.UiAcceptanceHost/README.md) |
| 安装实验 | test-inno-baseline / test-inno-exit / test-installation-transaction / test-inno-bundle；合成与完整载荷分开，受控退出、真实隔离 HKCU/LNK、Run 镜像、注册拒写、Inno/助手进程中断、重开恢复；native-launch 检查隔离 app/marker 布局的真实 WinUI 启动与正常退出升级 | 正式安装目录的数据模式、原生认证/草稿全矩阵、正式旧版完整卸载、实际桌面/开始菜单/自启、交互卸载、断电/重启和干净系统 |
| 旧脚本守卫 | test-legacy-uninstall-guard；归档未修改脚本、子进程独立 LOCALAPPDATA、实际错误文本/退出码及文件哈希 | 原位置确认卸载、所有历史包兼容、真实鼠标操作；UIA 关闭错误窗口不等于点击卸载按钮 |
| 正式安装回归 | test-formal-installer；同一正式 Inno 脚本和助手源码，编译期隔离 Known Folder/HKCU 身份、完整载荷、实际 LNK、自启镜像、真实 ACL 拒写、交互取消、同包重装及旧入口拒绝 | 当前主用户真实安装项/桌面/自启写入、所有历史二进制、原生 App 认证卸载全流程、干净 Windows/重启；受控协议进程不冒充 WinUI |
| 性能 | RuntimeProbe performance/data-performance/presentation-performance/residency；measure-desktop.ps1 / measure-residency.ps1 | 跨机器保证、跨夜稳定性；受控活动服务不代替原生游戏，窗口隐藏不等于托盘菜单点击；新进程不等于冷缓存 |
| 真实与隔离系统 | ACCEPT-201/202/203 的主题/游戏/Windows 10/11 安装矩阵 | 未执行的环境不得继承本机结果 |

执行者负责记录预期/实际及范围；交付者核对受测包和源码身份。没有真人验收时不得把脚本称为用户验收。

## 功能到验证映射

| SPEC ID | 必要覆盖 |
| --- | --- |
| FR-LIB-001 / FR-LIMIT-001 | 发现/筛选/历史条目、四类规则 CRUD、冲突认证、原生双击右键与目标 |
| FR-TIME-001 / FR-ENFORCE-001 | ticks/余数/周边界、保存先于强退、进程身份、受控时钟和真实游戏 |
| FR-EST-001 / FR-STAT-001 | 账号/缓存/对账、审计/过期编辑、日期保留、图表/预算同代一致 |
| FR-SET-001 / FR-EXIT-001 | 分区草稿、最新认证、重复命令、停/恢复、目录 dirty 与活跃 ticks 分开 |
| FR-MAINT-001 / FR-DELIVERY-001 | 旧格式/恢复日志、自启/数据保留、协议故障、隔离安装回滚 |
| FR-UI-001 | 多行正文/窄窗、浅深高对比、键盘/读屏、100/150/200% |

## 质量门与复现

局部补丁先最相关检查；共享运行/数据/安全边界扩大回归。按用户偏好，相邻任务可合并测试，最终受影响包仍需核对，不反复运行没有新问题的同一检查。

开发命令见 [README](../README.md)。桌面检查需交互会话、新复制绿色目录、新输出、无 SteamWatch 自启项；不修改用户主安装或使用真实密码，不强杀正在游玩的游戏。条件不满足使用隔离用户/虚拟机，不能关闭用户自启来迁就测试。

每批证据包含：WU/FR、源码/未提交标识、构建配置、版本/包哈希、输入规模与来源、步骤、预期/实际、失败恢复、未覆盖范围。TRX、截图、JSON 和 verification.json 留 artifacts 独立批次；首次失败保留，重跑不能抹除。UI 输入先确认提交，异步操作等可观察完成信号，真实鼠标不得以按钮调用冒充。

## 发布门

代码、包和最终受测程序集相符；功能/认证/数据迁移没有遗漏；目标主题/系统/真实运行有独立结果。签名证书、远程 CI 和正式发布分别记录；有本地构建不代表远程 CI，有签名不保证 SmartScreen 无警告。未满足项列入 STATUS，不使用总通过数量代替覆盖矩阵。
