# API-UPGRADE-001 当前用户正常退出协议

这是安装器与桌面应用的本地 IPC 合同，无 HTTP API。源码：[UpgradeExitProtocol.cs](../../src/SteamWatch.Infrastructure/Runtime/UpgradeExitProtocol.cs)。用户行为见 FR-EXIT-001/FR-DELIVERY-001；Inno 仅接入此协议，不读取密码。

## 地址与安全边界

管道名称为 SteamWatch-Upgrade- 加数据目录绝对路径 ToUpperInvariant 后 UTF-8 的 SHA256 十六进制。只允许 CurrentUserOnly，按目录隔离，双向字节传输，服务端最大实例 1。路径由调用方选择预期实例，不接受通过管道执行任意命令或路径；不将管道可达性当成授权成功。

## 输入输出

| 方向 | 字节 | 意义 |
| --- | --- | --- |
| 请求 | 1 | 请求正常升级退出；其他命令不处理 |
| 响应 | 0 | Declined，用户取消或当前不能退出 |
| 响应 | 1 | Prepared，UI 已完成准备；仍须等进程实际退出 |
| 响应 | 2 | Failed，准备异常，应用不得被强杀 |

连接等待最多 3000 ms，客户端另有调用方传入的整体超时/取消；服务端每个请求最多 90 秒。客户端精确读取一字节并拒绝未定义响应；连接失败/EOF/取消不视为成功。没有协议的旧版需用户手动关闭。

## 准备、提交与恢复

服务端调用 UI：草稿决定→最新认证→停监控并 Flush；准备成功后写/Flush 响应，再调用 commit。响应不是进程已退出的证明，安装器必须等待原进程退出后才切目录。准备失败/取消不提交；准备成功但发送或提交失败调用 abort 恢复。abort 异常在请求内记录，不能结束监听，之后可再次请求。

重复请求仍受 UI 退出/命令门约束；重试不能绕过新一轮认证。诊断仅记录分类/异常类型。应用实现的 Dispatcher 取消入队、安装器系统注册失败和断电场景需分别验证，不能从管道单测推断。

## 验证

[UpgradeExitTests](../../tests/SteamWatch.Tests/UpgradeExitTests.cs) 覆盖成功、拒绝/准备失败、客户端超时、目录隔离、提交/恢复双重异常后续请求。RuntimeProbe 的 upgrade-client 使用正常客户端，upgrade-disconnect 以文件门控在认证等待期间断开；原生脚本随后验证命令/草稿恢复及下一请求正常退出。实际结果只写 本地 `STATUS.md`。
