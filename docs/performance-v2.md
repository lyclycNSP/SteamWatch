# 运行层性能对照

本文按日期保存 WU-RUNTIME-201/203 的规则、账本、展示投影、原生启动/刷新及驻留测量；各节口径不同，不能合并为整个应用的性能验收。

> 下文按测量批次保留当时结果，不维护动态下一步；2026-10-12 的短测及当时覆盖缺口不覆盖后续章节的新测量，实际推进见 本地 `STATUS.md`。

## 数据与复现

探针使用固定日期、1,825 天历史、500 个游戏、每天 20 个游戏记录、1,002 条每日/每周规则。全为合成内存数据，无真实账号和游戏进程。Release、x64、.NET 10.0.7；每项预热 20 次，再测 100 次。每轮用 Stopwatch 测耗时，用 GC.GetAllocatedBytesForCurrentThread 测当前线程分配，保存计算结果校验和。

```powershell
dotnet run --project tools/SteamWatch.RuntimeProbe/SteamWatch.RuntimeProbe.csproj -c Release -- performance artifacts/performance/current.json
```

历史结果保存在 `artifacts/v2-week-query/performance-before.json` 和 `performance-after.json`。before 在本批两处算法修改前执行，after 在修改后执行，使用相同探针与数据。当前源码运行上述命令得到的是优化后的结果。

## 实测结果

| 路径 | 修改前中位 / p95（ms） | 修改后中位 / p95（ms） | 每轮分配，前 → 后（bytes） |
| --- | --- | --- | --- |
| 全部规则评估 | 4.4556 / 13.3513 | 2.4859 / 4.4980 | 671,520 → 671,898 |
| 全部游戏周时长及周总量查询 | 8.5198 / 12.7290 | 0.4948 / 0.9032 | 125,848 → 280 |

规则评估先扫描一次历史，保留本周记录供所有规则复用；每日/每周评估器、提醒状态及强退倒计时保持原有职责。筛选保持输入顺序及重复日期语义。周时长利用已有日期字典查询七天，避免每个游戏遍历全部日期；不增加持久缓存，因此 Add 后立即可见。

两项前后校验和分别均为 294,000 和 273,000。规则评估分配略增，来自共享的本周记录数组；本批没有声称该路径减少分配。新增自动回归覆盖跨年周、范围外日期、每日首记录/每周重复日期、查询后新增记录、整数溢出及每日规则的最大日期。

## 解释边界与后续

这是本机单轮微基准，受 JIT、GC、调度和同时运行的任务影响，不能把比例推广为整个应用速度或所有数据规模的保证。探针的规则阶段包含提醒/倒计时状态维护，周查询阶段复用已建 RecordBook；两阶段均不包含账本加载、建表、磁盘保存、Steam 读取、进程采样或 WinUI 绘制。

后续继续按计划测量并评估运行快照生成、账本投影/保存、冷启动、驻留 CPU/内存及实际刷新成本。保存失败、退出恢复与安装迁移的验证另行记录；此文不能替代真实游戏和干净 Windows 环境验收。

## 账本展示投影与保存：后续实施

2026-10-12，仍属 WU-RUNTIME-201。新增 `data-performance` 模式，固定 1,825 天、500 游戏、每日 20 个实测记录及合成对账基线。每项预热 5 次，展示投影测 100 次、原子保存测 20 次。耗时含结果汇总/文件长度读取；异步阶段用 GC.GetTotalAllocatedBytes 测整个探针进程的分配，不能与前节的当前线程口径混用。

```powershell
dotnet run --project tools/SteamWatch.RuntimeProbe/SteamWatch.RuntimeProbe.csproj -c Release -- data-performance artifacts/data-performance/current
```

修改前实测保存在 `artifacts/v2-ledger-io/before/performance.json`；首轮精简投影/流式保存结果保存在 `after/performance.json`；去除记录投影临时键集合后的最终结果保存在 `after-final/performance.json`。展示阶段前后生成相同界面字段，优化后改用 CreateReadSnapshot；保存阶段均调用真实 PlaytimeLedgerStore.SaveAsync。

| 路径 | 修改前中位 / p95（ms） | 最终中位 / p95（ms） | 进程每轮分配，前 → 后（bytes） |
| --- | --- | --- | --- |
| 账本展示字段投影 | 4.0273 / 12.9395 | 2.2313 / 4.6948 | 7,371,759 → 1,308,291 |
| 完整账本原子保存 | 19.7354 / 22.6865 | 23.0270 / 27.3168 | 4,198,700 → 1,789,146 |

快照不再复制持久化专用的 ticks 和 Steam 基线；记录投影直接填充结果字典，保持实测分钟取整及估算分离。保存通过 SerializeAsync 直接写独占临时文件，关闭后才原子替换主文件并保留 .bak，避免构造整份 JSON 字符串。账本 schema 与磁盘内容不变。

首轮 after 的展示中位 5.3519 ms、保存中位 22.3740 ms，没有改善耗时，因此继续优化每日字典构建。最终投影分配约降 82%，保存分配约降 57%；**保存耗时本轮约增 17%，不能称为保存提速**。这里选择减少完整 JSON 的临时内存，后续仍需在实际账本规模下评估 I/O 与驻留表现。单机短测包含调度、JIT、GC、文件缓存及存储设备差异，不能推断其他系统的结果。

展示前后累计校验和均 38,375,000，保存文件长度累计均 24,824,240，加载后总分钟均 383,250。自动回归另覆盖快照与后续账本双向隔离、零修正与不足一分钟、估算独有日期不参与实测、序列化中取消保留主文件/备份、失败清理后重试，以及锁定账本导致退出 Flush 失败后精确重试。最后一项是运行服务与实际文件系统检查，未替代真实窗口退出失败的原生交互验收。

## 同代图表复用与原生测量

RuntimeReadModel 已生成 MeasuredBook，七日图表原先仍通过静态投影重新构造完整实测历史。现在复用同代账本查询七天，估算仍独立汇总，不参与预算；不增加跨快照缓存。新增跨年、估算独有日期、逐游戏扣估算取零及预算一致性回归。

合成展示探针包含构造 RuntimeReadModel、每日/每周列表、七日图表和预算，1,825 天/500 游戏/每日 20 条，预热 20 次、测 100 次。中位 6.8596 → 4.6115 ms，p95 14.7579 → 12.7742 ms，当前线程每轮分配 6,134,020 → 4,593,011 bytes，校验和均 147,000。该探针不含 XAML、Steam 读取或磁盘。

```powershell
dotnet run --project tools/SteamWatch.RuntimeProbe/SteamWatch.RuntimeProbe.csproj -c Release -- presentation-performance artifacts/presentation/current.json
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/measure-desktop.ps1 -PublishDirectory artifacts/v2-native-performance/artifacts/publish/SteamWatch-win-x64 -OutputRoot artifacts/native-measurement-new
```

原生脚本使用独立绿色目录，分别生成 1 天与 1,825 天、每日 20 游戏的合成实测账本，读取本机实际 Steam 游戏目录。每组新建进程 3 次，第三次前台空闲 30 秒、手动刷新 3 次，最终正常关闭并核对账本未增长。未清系统文件缓存，不称为系统冷启动。before 使用上一批 v2-ledger-io 包，after 使用本批包；证据均位于 artifacts/v2-native-performance。

| 账本 | 前 3 次启动就绪（ms） | 后 3 次启动就绪（ms） | 刷新中位，前 → 后（ms） | 30 秒空闲整机 CPU，前 → 后 |
| --- | --- | --- | --- | --- |
| 1 天 | 3142.7 / 1199.5 / 1195.3 | 3158.7 / 1396.3 / 1318.6 | 207.2 → 228.2 | 约 0.16% → 0.20% |
| 1,825 天 | 3160.6 / 1451.4 / 1568.0 | 3167.2 / 1359.1 / 1292.0 | 276.6 → 247.7 | 约 0.23% → 0.27% |

启动就绪时工作集：小账本前约 191–193 MiB、后 191–193 MiB；五年账本前约 201–204 MiB、后 202–205 MiB。完整 private bytes、CPU 时间、空闲采样和逐次刷新数值见 measured-before/performance.json、measured-after/performance.json。样本很少且受调度/JIT/文件缓存影响，小账本刷新反增、空闲 CPU 也未下降，不能宣称全应用启动或驻留改善。

启动信号为游戏目录已投影且刷新按钮可用，50 ms 轮询；刷新信号为本次账本替换且按钮恢复可用，20 ms 轮询并包含 UIA 开销，不能解释为精确的纯业务耗时。如果未来刷新不保存账本，必须调整信号。首轮泛型数组转换错误（baseline）与第二轮 UIA 事件不完整（baseline-retry）均拒绝生成结果并保留现场，最终只使用 measured-before/after。尚未覆盖托盘长时驻留、活跃游戏采样、全屏、休眠/跨夜或干净机器。

## 有界驻留与受控活动成本（2026-10-13，RUNTIME-203）

固定 1,825 天、每日 20 个游戏的合成历史，在本机 Windows 内核 10.0.26200、12 个逻辑处理器环境顺序运行三组工作负载。先运行 Release 服务级受控活动探针，再用既有 Debug x64 绿色包测可见窗口和正常关闭隐藏到托盘；三组没有重叠运行。应用包 SHA256 为 `f322b48e6cd628623005286b0b1d612da299391db58659303f62fd8a6017624f`，本轮 141 个应用/构建源文件哈希未变，没有进行应用性能补丁。

原生每 5 秒采样进程 CPU、working set、private bytes、句柄/线程、I/O 和窗口可见性，前台在此仅指窗口可见，不保证持续占有键盘焦点。两个阶段结束后通过当前用户升级协议正常退出，核对每个历史游戏 ticks 完全保留。活动组每 10 秒调用实际 Runtime.PollAsync，使用生产 WMI/WindowsRuntimeClock、一次性 steam/game 子进程；独立记录 WMI 和整次 Poll 耗时，结尾 Flush、重开并核对历史及活动 ticks。通知接收器不弹系统通知，无破坏性规则。

| 场景 | 实测秒 / 样本数 | 平均进程 CPU（整机口径） | 工作集范围 MiB | Private 范围 MiB | 首末约一分钟 Private 均值 MiB |
| --- | --- | --- | --- | --- | --- |
| 可见窗口，原生 Debug 应用 | 301.12 / 60 | 0.105% | 200.13–211.42 | 88.28–100.44 | 95.18 → 91.60 |
| 隐藏到托盘，同一原生进程 | 301.14 / 60 | 0.077% | 194.80–209.15 | 82.78–97.20 | 88.97 → 93.17 |
| 受控活动，Release 服务探针 | 300.21 / 30 | 0.101% | 68.71–78.62 | 32.04–41.64 | 34.24 → 36.19 |

整机 CPU = 进程累计 CPU 增量 / 实测时长 / 12，含进程自身的运行和测量开销。首末均值按原始时间戳筛选首末约一分钟的采样点；逐点值、句柄/线程和 I/O 在原始 JSON/JSONL 内。可见窗口阶段观测到 1 次、314 bytes 的进程写 I/O，托盘阶段为 0；两个阶段均未观测到账本替换，最终实测 ticks 未变。进程 I/O 不仅来自账本；5 秒文件时间戳采样也不是每次写入的精确计数器。

| 活动服务路径 | 样本数 | 排序中位 / p95（ms） |
| --- | --- | --- |
| 完整 Poll（WMI、checkpoint、保存、规则、发布） | 30 | 199.26 / 303.79 |
| Poll 内实际 WMI provider | 30 | 175.67 / 282.69 |
| 独立原子账本保存，5 次预热后测 10 次 | 10 | 20.16 / 29.92 |

偶数样本的中位沿用探针约定，取排序后较大中位。独立保存发生在活动 CPU 窗口之前，不额外插入每个 Poll；最终 Flush 为 18.52 ms。不能从整次 Poll 中减去两个中位数后声称获得纯保存耗时。账本约 1,292,369 bytes；活动记录 300.2169179 秒，独立 Stopwatch 300.2078474 秒，差 0.0090705 秒，满足 2 秒校验界限。历史 ticks、无额外估算和重开后的实测分钟均通过检查。

### 解释与实现决定

本轮形成可重复的有界驻留基线。WMI 占活动 Poll 耗时的主要部分，当前样本没有给出足以证明应替换采样、缓存身份或调整保存策略的收益，因此保留 10 秒周期和现有计量/保护合同。可见与托盘的差值是固定顺序单轮场景观测，不能写成代码优化收益；原生 Debug 与 Release 服务探针也不是直接对照。

内存有回落也有波动；托盘末段 Private 均值比首段高约 4.2 MiB、活动服务高约 2 MiB，五分钟无法排除缓慢增长或证明无泄漏。没有清系统缓存，不称冷缓存测量；未覆盖真实游戏、原生游戏与托盘联动、全屏通知、休眠/跨夜、干净系统及托盘图标菜单点击。这些边界留给专项验收，不能由本轮约 15 分钟的分场景样本替代。

### 复现与证据

```powershell
powershell -NoProfile -File scripts/test-runtime.ps1 -Mode residency -DurationSeconds 300 -OutputRoot artifacts/residency-active-new
powershell -NoProfile -File scripts/measure-residency.ps1 -PublishDirectory artifacts/v2-active-exit-recovery/artifacts/publish/SteamWatch-win-x64 -ProbeExecutable artifacts/residency-active-new/publish/SteamWatch.RuntimeProbe.exe -StageSeconds 300 -OutputRoot artifacts/residency-native-new
```

两个输出目录必须不存在；原生输入须为无 data 的绿色包，运行用户没有 SteamWatch 自启项，也不运行实际游戏。正常结束后检查输出 residency.json 和原始 samples.jsonl，不把失败目录当作成功结果。

证据：[原生驻留](../artifacts/v2-residency/native/residency.json)、[受控活动](../artifacts/v2-residency/active-final/evidence/residency.json)、[汇总与身份核对](../artifacts/v2-residency/verification.json)。首轮探针缺少 Settings 命名空间导致编译失败；首次原生脚本在 PowerShell 7 AST 通过后，被 Windows PowerShell 5.1 按无 BOM 编码误读中文正则，解析失败发生在启动应用之前。分别补上 using 和 ASCII Unicode 转义后才进行以上实际测量；两个失败未生成成功测量数值。

运行层集成记录见 [同包证据核对](../artifacts/v2-residency/runtime-integration.json)：复用本日该应用包实际通过的 221 项单元测试、53 项完整原生检查，核对源码/程序集身份和两个专项交接。本轮没有重新执行这些功能测试，也没有把复用计为新一轮通过。
