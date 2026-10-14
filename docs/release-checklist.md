# 发布检查单与签名配置

本文件维护交付合同和可执行步骤；实际版本、通过结果、证书可用性和待验项只见 本地 `STATUS.md`。安装恢复机制见 [安装设计](installer-evaluation.md)。默认使用 PowerShell 7，不自动发布。

## 发布范围

2026-10-14 根据个人自用与早期开源发布目标，采用开源预览档。完整 ACCEPT-201/202/203 合同保留为扩展验证，不把暂缓项记作通过，也不要求全部完成后才能准备公开预览。

| 档位 | 必要门槛 | 可延期 |
| --- | --- | --- |
| 个人自用 | 本机构建、基本使用、数据保存与备份 | 公开许可证、远程 CI、多环境矩阵、签名 |
| 开源预览（本轮） | MIT 与依赖声明、基本自动化/用户反馈、当前 x64 包启动与本机隔离安装/卸载、数据保留、源码/版本/包对应、已知问题与实际测试范围 | 全主题/DPI/高对比度、其他系统/架构、长时与周一真人矩阵、真实签名、远程 CI |
| 扩展兼容性验证 | 对承诺支持的系统/界面/真实游戏场景补 ACCEPT 专项证据 | 未承诺的系统与平台；签名仍按选定渠道和实际需求处理 |

开源预览可采用 Unsigned，不要求购买证书或配置时间戳；Signed 仍必须真实核验且失败不回退。已有数据保护、回滚和退出保护不得因精简验收被删除。偶发问题无法复现时保留现场和明确已知问题，不能宣称根治。

## 生成与检查

```powershell
pwsh -NoProfile -File scripts/build-installer.ps1 -Configuration Release -SigningMode Unsigned -OutputRoot artifacts/my-delivery
pwsh -NoProfile -File scripts/verify-delivery.ps1 -Directory artifacts/my-delivery/dist
```

`Directory.Build.props` 是版本唯一来源：Version 为语义版本，AssemblyVersion/FileVersion 为四段数字。帮助页、应用/启动器产品版本和安装包展示语义版本；PE 数字版本使用 FileVersion。Inno 卸载器保留引擎内部 FileVersion，ProductVersion 表示产品版本；读取 Inno 版本资源时去除填充空格。

交付目录包含安装 EXE、绿色 ZIP、两者 SHA256、绿色包逐文件 manifest、release-manifest.json 及其 SHA256。清单记录版本、构建配置、源提交与 dirty 状态、文件大小/哈希、实际签名状态、自包含证据和验收缺口。SHA256 用于完整性核对，不替代可信发布来源或数字签名。预览清单的 releaseReady 固定为 false；不能仅改清单就宣布验收或发布完成。

脚本先在独立 artifacts 暂存区完成发布、签名、压缩、编译与验证，再逐文件提交对外产物；异常时补偿已替换文件，旧副本保留。相同 OutputRoot 共用文件锁，阻止并发构建互相覆盖。多文件提交不是断电原子事务；消费前运行 verify-delivery，发现不一致保留现场并重跑构建。单独 publish-portable 也有版本/逐文件清单与失败补偿；安装构建的平铺载荷留在该次私有 build 目录。

## 可选的真实签名

Signed 模式要求事先在 Windows 证书存储中配置有效代码签名证书及私钥。构建脚本不接收 PFX 密码、不导出/导入私钥、不自动创建自签名证书；本机或 CI 的证书配置属于受控外部准备。

| 环境变量 | 用途 |
| --- | --- |
| STEAMWATCH_SIGN_CERT_SHA1 | My 存储中指定证书的 40 位 Thumbprint，非私钥 |
| STEAMWATCH_TIMESTAMP_URL | HTTP(S) RFC3161 地址；禁止凭据、查询串及 fragment |
| STEAMWATCH_SIGN_STORE_LOCATION | CurrentUser（默认）或 LocalMachine |
| STEAMWATCH_SIGNTOOL_PATH | 可选 Windows SDK signtool.exe；省略时查找 SDK x64 工具 |

```powershell
pwsh -NoProfile -File scripts/build-installer.ps1 -Configuration Release -SigningMode Signed -OutputRoot artifacts/my-signed-delivery
```

先签主程序、项目 DLL、绿色启动器和自包含助手，再压缩；Inno 使用独立 SignTool 回调签安装器及卸载器。使用 SHA256 与 RFC3161 时间戳，并以 SignTool verify /pa /all /tw、实际签名证书和时间戳证书核验；验证器对 ZIP 内项目二进制也核对实际签名。签名验证会在 Temp/SteamWatch-Verify-* 留存指定二进制作为核查输入，不作为交付文件。

缺证书、私钥、工具、时间戳或核验失败都中止 Signed 构建，不静默转为 Unsigned。Unsigned 必须明确作为未签名预览交付；配置存在不代表真实签名已经执行。Hosted CI 没有自动获取签名私钥；选择 Signed 仍须运行环境预先具备证书。签名不会替代验收，也不保证 SmartScreen 声誉。

官方合同：[SignTool](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool)、[Inno SignTool](https://jrsoftware.org/ishelp/topic_setup_signtool.htm)、[SignedUninstaller](https://jrsoftware.org/ishelp/topic_setup_signeduninstaller.htm)。

## 发布前的必要核对

1. 版本与预览/正式渠道一致，未提交修改有明确来源；候选按 RELEASE-201 核对。项目 MIT 与实际依赖许可随包保留，公开源码与预期标签可对应。
2. 运行 verify-delivery，核对 ZIP 全文件、安装器、checksum、版本与签名。包中无 data/logs/探针/私钥；自包含配置与所需运行文件齐备。
3. 签名模式与实际结果一致；有证书时检查安装后的卸载器签名与时间戳，无证书时保留未签名限制。
4. 按所选档位完成必要验证；开源预览记录本机/用户证据与延期项，扩展验证才要求相应 ACCEPT 全矩阵。自包含文件存在不代替干净系统运行，未检查的平台不写成已支持。
5. 开源预览核对最终说明、已知问题、许可证与批准结果后可发布；远程 CI 和真实签名本轮可延期。本地准备不自动创建提交、标签、推送或 GitHub Release。

预览清单 releaseReady=false 表示完整验证未全部结束，不表示禁止发布如实说明限制的 GitHub Pre-release；本轮不将其改成 true。实际公开发布须另行获得用户明确指令。
