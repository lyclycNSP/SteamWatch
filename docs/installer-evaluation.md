# 安装交付设计与兼容约束

正式入口采用 Inno Setup 向导和自包含 C# 事务助手，旧 PowerShell 卸载仅保留为历史兼容源码，不再随新安装包部署。本文维护安装合同与选型依据；进度、样包及实际结果只在 本地 `STATUS.md` 和对应 artifacts 记录。

## 决策边界

2026-10-11 的“本轮暂不迁移”是历史决定，不能解释为当前禁止 Inno；原评估逐字节保留于 [重组快照索引](history/doc-migration-2026-10-12.md)。正式切换前必须完成 DELIVERY-004 验证，之后 DELIVERY-005 接入安装/卸载和旧版兼容，DELIVERY-006 交付/签名。用户数据保留与升级协议不得弱化。

## 当前安装与绿色载荷

程序位于当前用户 LocalAppData/Programs/SteamWatch，数据独立 LocalAppData/SteamWatch，旧内置 data 复制迁移不删来源。安装器在校验及同父目录解包后分阶段切换目录/注册；前置正常退出按数据目录请求认证、草稿确认和保存，并等待实际进程退出。系统注册/快捷方式恢复和程序目录切换分别验证。

公开绿色 ZIP 的根启动器/app/data 布局与安装器平铺载荷不同，不能混用。安装器消费 installer-payload ZIP；版本统一 Directory.Build.props，构建/压缩暂存成功后切换并生成 SHA256。基础路径与字段见 [数据模型](architecture/data-model.md)，应用退出见 [IPC 合同](architecture/upgrade-protocol.md)。

## 正式安装、卸载与旧版交接

正式入口为 `installer/SteamWatch.iss`，构建入口为 `scripts/build-installer.ps1`；CI 沿用此入口。助手不携带随机验证身份、故障注入或可覆盖用户目录的运行参数，正式 Main 只使用当前 Windows 用户的 Known Folders。向导为简体中文、最低当前用户权限；不调用 Restart Manager 结束应用。

| 对象 | 固定合同 |
| --- | --- |
| 产品身份 | AppId `SteamWatch`；HKCU Uninstall/SteamWatch_is1 |
| 旧身份 | HKCU Uninstall/SteamWatch；InstallLocation 必须是当前固定程序目录，异常归属拒绝覆盖 |
| 程序 / 数据 | LocalAppData/Programs/SteamWatch；LocalAppData/SteamWatch |
| 卸载管理 | LocalAppData/Programs/SteamWatch.Installers/每次运行独立的 32 位十六进制身份；与被切换的程序目录分离 |
| 快捷方式 | 当前用户桌面 SteamWatch、开始菜单 SteamWatch/应用、操作指南、卸载；卸载目标为当前管理目录 unins000.exe |
| 自启 | HKCU Run/SteamWatch；首次不创建，升级仅更新已有启用值，其他 Run 值不改 |

PrepareToInstall 在任何 Inno 注册及程序替换前完成正常退出、未完成事务恢复、旧项归属检查、载荷 SHA256 和路径检查以及完整暂存。拒绝、保存失败、无管道旧版和超时均停止；无管道旧版须手动正常退出后重试，不强杀。平铺载荷禁止 portable.marker、旧 Uninstall.ps1/SteamWatchSetup.exe，拒绝 Windows 歧义名称、链接、逃逸与重复条目。

Inno 仅管理当前卸载助手、日志及卸载注册，程序文件由 DirectoryUpgradeTransaction 整目录切换。提交前快照固定两个卸载键的类型和值、四个快捷方式字节及自启值。先校验新卸载归属，再非覆盖复制旧 data、切换目录、重建快捷方式、保留自启偏好、移除旧键并提交。失败补偿目录与注册；已与快照相同的值跳过写入，避免 ACL 拒写阻断无变化项的恢复。准备失败返回 7，提交失败返回 20；发生二次恢复失败保留 journal 并提示重跑。

每次运行同一个安装 EXE 也创建新的管理目录。旧管理入口只读核对当前注册，不匹配即拒绝卸载；新包不执行旧脚本。旧脚本留在恢复副本中，其目录守卫阻止它操作当前安装；原位置旧注册和快捷方式由事务统一交接，不出现两个有效入口互删。

交互卸载沿用 Inno 的确认窗口，并明确说明保留数据与恢复副本。InitializeUninstall 只核对身份并持生命周期锁；确认后的 `usAppMutexCheck` 才请求正常退出、复制遗留数据和运行可补偿的移除事务。此阶段异常会在 Inno 删除自身文件/注册前中止，允许重试；官方时序见 [Inno 7.1.0 卸载源码](https://github.com/jrsoftware/issrc/blob/is-7_1_0/Projects/Src/Setup.Uninstall.pas)。取消确认不退出应用、不复制 data、不改文件/注册。静默卸载意味着调用者已确认，仍不得绕过应用正常退出协议。

App 设置只接受固定安装目录和管理目录中无链接的 unins000.exe，不执行任意注册表命令。新版从 App 发起卸载时保持运行，确认后由卸载器请求认证和保存；旧脚本兼容入口仍先由 App 准备退出。安装模式独立于卸载入口是否完整；缺少注册或管理文件时提示重装修复，不把安装数据误称为绿色 data。

### 留存与恢复操作

事务 journal、历史 journal、SteamWatch.previous-*、失败暂存和卸载移出的副本保存在 Programs 同级，既不作为活动程序也不作为有效卸载入口。保留副本可处理注册失败和中断，不自动递归删除含历史 data 的目录；重复安装会增加磁盘占用。**卸载只移除活动程序、快捷方式、自启和当前管理注册，不承诺擦除恢复副本或用户数据。**

恢复时正常退出应用，重新运行安装包；未提交事务先按日志恢复，Committed 不自动降级。恢复报冲突/权限错误时保留现场并检查 Programs/SteamWatch.installation.log，不手动编辑 journal。确认重装成功、记录/规则/认证备份完整且无需回退之后，可手动移除明确属于 SteamWatch 的 previous/staging/removed 副本及过期管理目录；不要删除当前注册指向的管理目录或独立数据。签名与最终交付说明归 DELIVERY-006，干净 Windows/重启矩阵归 ACCEPT-203。

## Inno 验证输入与故障矩阵（选型阶段）

输入记录实际编译器路径/版本、官方来源、SHA256、签名核验和许可证；不填写推测值。使用隔离输出/用户，不全局安装工具或修改主安装来迁就验证。编译失败保留旧正式产物，验证包不直接切换发布脚本。

| 路径 | 原型要求 | 必须保留的失败证据 |
| --- | --- | --- |
| 用户上下文 | `PrivilegesRequired=lowest`，不切换到管理员账号读取另一份数据 | 当前用户安装目录、数据目录和 HKCU 注册项的实际位置 |
| 升级退出 | 显式 `CloseApplications=no`，由当前用户的升级管道处理认证、草稿、保存、退出；不依赖 Restart Manager 自动关闭 | 用户拒绝、保存失败、超时、旧版无管道时未替换文件 |
| 替换事务 | 在同父目录准备完整新载荷，原目录切换与注册分阶段检查 | 占用/解压/目录切换/注册失败时旧程序可运行，新旧数据均保留 |
| 旧卸载入口 | 先识别自制安装器 `SteamWatch` 项及旧脚本，验证新 `_is1` 项兼容后才清理 | 取消安装、失败回滚时不能留下两个可互删的入口 |
| 数据保留 | 旧安装内 data 复制迁移不覆盖独立数据，卸载默认保留 | 新装、同版重装、旧版升级、卸载各自的文件清单与哈希 |

配置候选为最低当前用户权限、显式关闭 Restart Manager 自动关闭，应用退出交给自定义协议；准确配置须按取得的编译器版本核对。旧卸载 SteamWatch 项与 Inno 派生项差异要单独处理，避免两个入口互删；无协议旧版要求手动退出，不强杀。

程序回滚不能直接降级已升级数据；校验旧 EXE 读格式能力。磁盘不足、占用、目录切换、注册失败、机器中断后应保留可用程序或给出恢复步骤。若 Inno 文件事务不能满足目录恢复，使用受控更新助手，依故障证据再定，不以功能列表推断。

## Inno 隔离实验与实现决定（2026-10-13）

工具链固定 **Inno Setup 7.1.0 x64**，来源为[官方下载页](https://jrsoftware.org/isdl.php)所链接的 GitHub release `is-7_1_0`。下载 EXE 的固定 SHA256 为 `0362a383ed217d4c4239b5933866dd96d3eb2102737da92f80f6057a4b40df2f`；与官方 `.issig` 中的值核对，并通过 Windows Authenticode 验证，签名主体为 Pyrsys B.V.。这不是执行 ISSigTool ECDSA 验证的声明。

`scripts/prepare-inno-toolchain.ps1` 使用该版本[官方便携模式源码](https://github.com/jrsoftware/issrc/blob/is-7_1_0/isportable.iss)定义的 `/PORTABLE=1`，附加 `/CURRENTUSER /NOICONS`，限制输出在仓库 artifacts；不全局安装、不修改 PATH。执行前验证 ISCC 和目录内 DLL 签名，随后用 `ISCC --version` 核对引擎版本。ISCC 的 PE 文件版本实际为 `0.0.0.0`，不能用该字段判断版本。原始 [Inno Setup License](https://github.com/jrsoftware/issrc/blob/is-7_1_0/license.txt)留在工具链目录，工具清单记录许可证、编译器和 DLL 哈希；不另命名为 MIT/BSD。网络只用于首次获取工具链，编译器为原生程序；实验协调器需 .NET 10。

实验模板 `installer/validation/baseline.iss` 固定当前用户权限、关闭 Restart Manager 自动关闭，使用每次随机的独立 AppId/HKCU 卸载项。目标目录在编译时锁定，`/DIR` 改到其他位置会在复制前拒绝。模板及协调器均仅供验证，不能作为正式发布入口。

两文件合成载荷的实测说明了原地覆盖的恢复边界：

| 故障位置 | 实际结果 | 对方案的影响 |
| --- | --- | --- |
| 第一个文件被占用 | 安装退出 5，原载荷保留 | 覆盖前失败可以中止，但不能推断后续失败也完整恢复 |
| 已替换第一个文件，第二个文件被占用 | 安装退出 5；日志出现 rollback，但第一个已替换、第二个保留原值，卸载项仍为原版本 | 原地逐文件覆盖不能满足本项目整目录恢复合同 |
| `ssPostInstall` 回调抛异常 | 日志记录异常，静默安装仍退出 0，文件和卸载项保持目标版本 | 不能以安装后回调异常作为注册失败的事务回滚机制；这是回调故障注入，尚非注册表拒写实验 |

**实现决定：Inno 负责安装向导、当前用户注册和卸载管理，继续保留受控更新助手负责载荷暂存、目录切换和失败恢复。** 正式入口现有 `DirectoryUpgrade` 只有进程内目录回滚；新增事务原型见下节，尚未替换正式入口。不得把该实验结论解释为所有 Inno 配置都不支持回滚。

`tools/SteamWatch.InstallationProbe` 直接链接生产 `UpgradeExitProtocol.cs`，`test-inno-exit.ps1` 通过 Inno 的 `PrepareToInstall` 调用协调器：按 PID、启动时间及精确可执行路径核对自身受控进程；回复 Prepared 后还等待实际进程退出。实验用两秒超时以缩短故障路径，生产协议超时不变。接受、拒绝、保存失败、请求超时、Prepared 后仍驻留分别验证；不强杀。其目标仅限探针自身，不是尚未实现的正式 SteamWatch 进程协调助手。

证据入口与当前通过情况只在 本地 `STATUS.md` 维护。上述 Inno 原地覆盖/退出实验不覆盖完整 WinUI 载荷、原生认证/草稿、正式旧 `SteamWatch` 卸载项迁移、断电或干净 Windows 环境；隔离注册拒写与事务恢复另见下节。

## 目录事务与注册补偿原型

`DirectoryUpgradeTransaction` 由正式助手、InstallationProbe 与单测共用。事务持有独占文件锁，准备时写入同父目录 journal 和新载荷身份标记；日志使用 WriteThrough/Flush(true)、临时文件及替换，记录版本、事务 ID、安装/暂存目录名、是否有旧程序、注册快照和阶段。日志限 1 MiB，不接受未知版本/阶段、逃逸路径或链接/重解析目录；不执行日志提供的命令。

阶段为 Prepared → OldMoved → Switched → Committed。持久化意图早于目录移动；恢复同时检查日志与文件实际位置，因此能处理“目录已移走，下一阶段尚未写入”的间隙。新目录必须带本事务标记，出现外来目录或相互冲突的副本就拒绝覆盖，保留日志和现场。失败恢复把新载荷移回暂存、旧目录移回安装位置，再调用幂等注册补偿，成功后写 RolledBack；注册补偿失败保留未完成阶段，允许下次重试。

Committed 不自动降级。Committed/RolledBack 日志可显式 ArchiveResolved 后再开启下一次事务；归档日志、旧目录和失败载荷均保留，不递归删除。该原型不负责用户数据格式降级、载荷清单验证或应用退出协调；完整安装流程必须在前置退出与载荷校验成功后才能切换目录。

注册快照原型在探针中实现，路径限本次 GUID 标识的两项隔离 HKCU 卸载键。保存键是否存在、值名、值类型和原值；Binary、MultiString、QWord 等未知扩展值按原类型恢复。快照路径必须恰好匹配本次两个键，拒绝子键结构；恢复先比较，未变的键不重新写入，可在 SetValue 被拒绝时恢复文件而保留未变注册。成功路径先写新入口再去除旧入口，失败补偿还原两者；正式旧键路径与卸载脚本兼容仍须独立验证。

`test-installation-transaction.ps1` 使用合成载荷和真实隔离注册键。在准备完成、旧目录改名后、新目录改名后、注册变更完成四处令子进程自行退出，再用新进程恢复并重复恢复；另验证部分注册写入后失败、真实 ACL SetValue 拒写、成功提交后拒绝自动降级。ACL 的原始 SDDL 先保存，恢复时只请求 ReadPermissions/ChangePermissions，不额外请求已被拒绝的 SetValue。恢复核对每个 ACE 的二进制内容及控制标记，只允许 Windows 自动补充的 SE_DACL_AUTO_INHERITED 位变化；原始/恢复 SDDL 均保留，不称描述符逐字一致。探针捕获实际拒写可能抛出的 SecurityException，不仅处理 UnauthorizedAccessException。

这些是本机进程中断与注册故障证据；WriteThrough 和进程重开结果不证明断电后元数据一定持久化。

## 完整载荷隔离安装原型

`installer/validation/transactional.iss` 与 BundleFixture 串联完整平铺载荷、退出协议及 DirectoryUpgradeTransaction。管理工具和 Inno 卸载文件放在每次构建独立的 installers/BuildId，程序放在 app，根放 portable.marker，独立数据放在 data，避免切换程序目录时移动正在运行的安装/卸载文件。此前沙箱的 program 名称不属于 AppDataDirectory 支持的绿色子目录，会把数据放到 program/data；当前布局沿用既有 app/marker 解析路径，真实启动和助手共用根 data。此布局仅为实验输入，不宣称正式安装模式，正式目录与身份由 DELIVERY-005 确定。

1. PrepareToInstall 校验固定沙箱、请求正常退出并等待进程结束；验证内嵌 ZIP 哈希，拒绝路径逃逸、Windows 歧义文件名、重复及链接条目；解包至同父目录暂存，保存注册、固定四个快捷方式和自启镜像快照。
2. Inno 写入独立管理目录与新卸载项后，助手核对该项确实属于本次构建；旧内置数据仅复制不存在的文件并保留来源，再切换完整程序目录、更新快捷方式和已启用的自启镜像、清理隔离旧项并提交。
3. 提交失败恢复旧目录、原注册及快捷方式字节；通过 [GetCustomSetupExitCode](https://jrsoftware.org/ishelp/topic_scriptevents.htm) 明确返回 20，不能只抛 ssPostInstall 异常。退出拒绝发生在准备阶段，不替换程序。
4. 卸载先核对当前注册的管理目录，过期入口拒绝执行；请求正常退出后，将程序及快捷方式移至沙箱留存目录，移除自启镜像值，随后由 Inno 清理自身注册/文件，独立数据保留。原型只接受静默卸载，交互确认与失败恢复属于正式迁移范围。

实验 AppId 使用短随机身份 `SWV-<GUID>`。首轮本机日志显示较长标识被 Inno 转为带 `~` 的缩写键名，造成精确身份核对失败；短身份与实际 `_is1` 键名对应后再验证。清理意外键前必须同时检查验证前缀与 InstallLocation 属于本批沙箱，不能按产品名称批量删除。

`test-inno-bundle.ps1` 核对完整文件哈希、四个真实 LNK 目标、旧载荷/链接回滚、非覆盖数据复制及来源留存、有效/过期卸载入口和注册清理。自启写入独立 HKCU 镜像，另只读核对实际 SteamWatch Run 值未变；模拟旧载荷携带当前旧卸载脚本。native-launch 从此包安装的 app 启动真实 WinUI，等待刷新按钮可用、根 data 创建账本且 app/data 不存在，随后在第二次安装中请求正常退出并等待实际进程结束，最后核对卸载数据保留。该检查只证明本机隔离布局启动与退出，不继承完整原生交互验收。实际桌面/开始菜单/自启、交互卸载、正式旧版完整卸载和系统重启/断电仍需独立验证。

### 完整 Inno 进程中断与重开

验证钩子覆盖 prepared、old-renamed、new-renamed、registered 和 committed。助手在指定位置调用 [Environment.Exit(73)](https://learn.microsoft.com/en-us/dotnet/api/system.environment.exit?view=net-10.0)，其 finally 不执行；Inno 收到 73 后通过 [ExitProcess](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-exitprocess) 自行结束，跳过脚本补偿和 Deinitialize。不是杀死用户进程，也不模拟系统掉电；Windows 仍执行进程句柄清理。

随后以同一隔离身份、新管理目录重新运行完整安装包。PrepareToInstall 先读取未完成日志并恢复，保存恢复后的程序/快捷方式哈希、注册快照和自启镜像观察值，再归档已解决日志并准备本次载荷。测试先核对这个观察点与原状态一致，再核对重装成功；Committed 则保留已提交状态，禁止自动降级。恢复阶段读取范围限于已验证沙箱，观察文件仅为测试证据。实际占用旧 EXE 的场景则要求非零安装退出并恢复旧程序、注册和链接。

Inno 的系统 Temp 不属于产品安装目录；进程自行退出会跳过临时清理。实验根据各次日志、内嵌配置的沙箱位置及载荷哈希登记自身残留，不扫描删除其他临时目录，保留中断现场。编译器工具链为 x64，当前生成的 Setup 引擎仍为 32 位，载荷与安装注册模式为 64 位；三者分别核对，不混称全部二进制均为 x64。

### 正式迁移沿用的约束

DELIVERY-005 应沿用“前置校验/正常退出 → 同父暂存 → 目录事务 → 注册/快捷方式补偿 → 提交”的顺序，并补固定产品身份、当前用户正式目录与旧 SteamWatch 项识别。交互卸载必须先确认，再执行退出和写操作，不能直接复用本原型 InitializeUninstall 中的静默操作顺序。

验证助手依赖本机 .NET 10 运行时，构建需 .NET 10 SDK；正式助手应自包含，并移除随机验证身份、可配置故障/持锁钩子和测试记录文件，包中不带探针。生产卸载的程序清理、留存目录管理、正式 App 卸载入口、实际桌面/开始菜单/Run 自启、无管道旧版和同版重装都归入 DELIVERY-005；签名/版本/发布清单归 DELIVERY-006；干净 Windows 和重启矩阵归 ACCEPT-203。不能把隔离验证完成解释为上述发布工作完成。

### 连续升级与共同生命周期锁

模板在 InitializeSetup / InitializeUninstall 原子创建同一命名互斥对象，名称由固定沙箱路径的小写 SHA256 派生，跨 BuildId/AppId 共用；已存在或创建失败即拒绝，不触及载荷。成功创建的句柄一直持有至 Deinitialize，包括两次助手调用之间及失败补偿；进程退出由 Windows 释放句柄。该对象以存在性作排他门，不获取线程所有权或等待，不依赖 AppMutex 自动关闭应用。正式迁移还需核对规范路径、每用户边界及跨会话条件。

读取外部 CreateMutexW 的状态使用 [DLLGetLastError](https://jrsoftware.org/ishelp/topic_isxfunc_dllgetlasterror.htm)，避免 Inno 自身 API 调用覆盖最后错误；[Windows CreateMutexW 合同](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-createmutexw)定义对象已存在的返回行为。测试专用 HoldMillis 编译选项只用于初始化持锁后暂留；默认关闭，不作为发布功能。

连续升级沿用同一个隔离卸载身份，每次使用新管理目录。第二次成功后，注册及卸载 LNK 指向新管理目录，旧入口拒绝；第二次失败则恢复上次注册/链接和程序事务标识，失败构建的入口拒绝，上次入口仍可卸载。测试核对完整载荷与数据，并在持锁期间启动另一安装和当前卸载，要求非零退出且程序、链接、注册归属和数据均未变；主安装继续完成后当前卸载应成功。

### 归档旧脚本的退出边界

旧 Uninstall.ps1 把自身位置限定为当前用户 Programs/SteamWatch；完整目录留存之后，旧脚本也留在归档路径。`test-legacy-uninstall-guard.ps1` 复制未修改源码并设置子进程独有 LOCALAPPDATA，实际运行归档脚本，观察目录校验失败的错误文本与非零退出，核对测试程序、数据和脚本哈希未变。只关闭已核对文本且属于该子进程的错误窗口；本机原生控件被 UIAutomation 暴露为 Pane，无按钮 Invoke 模式，使用 WindowPattern.Close，不能称鼠标点击。

这证明该源码在受控归档布局中拒绝执行；没有执行原位置确认卸载，不证明所有历史 1.2.0 发布包相同或正式旧项已兼容。正式迁移不得调用归档旧脚本清理目录；旧注册、旧快捷方式与新入口的交接仍按补偿事务验证。

## 卸载、签名与系统验收

卸载移除程序/快捷方式/自启/卸载项，默认保留数据，不配置通配递归数据删除。新装不开自启、升级保留偏好。版本、签名模式、清单及发布门统一见 [发布检查单](release-checklist.md)，本文件不复制签名配置。

在干净 Windows 10/11 分别验新装、旧安装升级、同版重装、拒绝/超时/保存失败、注册中断、卸载保留数据与绿色包。证据格式见 [测试策略](test-strategy.md)，操作见 [验收单](manual-acceptance.md)。

## 历史查阅的官方资料

下列链接沿用原评估来源，本次文档整理未重新进行产品版本核实，不能代替取得工具后的验证。

- 当前用户权限与目录：[PrivilegesRequired](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm)、[DefaultDirName](https://jrsoftware.org/ishelp/topic_setup_defaultdirname.htm)。
- 应用关闭机制与强制关闭选项：[CloseApplications](https://jrsoftware.org/ishelp/topic_setup_closeapplications.htm)。
- 快捷方式及卸载身份：[Icons](https://jrsoftware.org/ishelp/topic_iconssection.htm)、[AppId](https://jrsoftware.org/ishelp/topic_setup_appid.htm)。
- 数据删除边界：[UninstallDelete](https://jrsoftware.org/ishelp/topic_uninstalldeletesection.htm)。
- 签名接入：[SignTool](https://jrsoftware.org/ishelp/topic_setup_signtool.htm)。
