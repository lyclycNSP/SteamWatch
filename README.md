# SteamWatch

Windows 原生、本地离线的 Steam 游戏时长管理工具。查看实测时长，设置每日/每周预算，并按自己的选择提醒或关闭游戏。

面向个人自用与开源反馈，当前提供 Windows x64 预览候选。公开下载以 [GitHub Releases](https://github.com/lyclycNSP/SteamWatch/releases) 实际附件为准；2.0 候选的变化见 [更新记录](CHANGELOG.md)。

## 主要功能

- 自动发现本机 Steam 游戏，支持搜索、分类与运行状态筛选。
- 全部游戏和单游戏可分别设置每日/每周限额，列表直接显示各周期的提醒或强退策略。
- 日周统计区分本地实测与 Steam 差值估算；估算可查看来源并带原因修正，不参与限额。
- 双击游戏编辑限额，右键打开详情、策略与分类；应用内提醒只保留最新一条待确认消息。
- 托盘常驻、可选认证保护、完整备份恢复及独立诊断导出。

## 下载与启动

| 文件 | 使用方式 |
| --- | --- |
| SteamWatch-win-x64.zip | 完整解压，双击根 SteamWatch.exe；保留 app 和 portable.marker |
| SteamWatchSetup-win-x64.exe | 安装到当前用户目录，从桌面或开始菜单启动 |
| .sha256 与 manifest 文件 | 校验下载完整性、版本和包内文件 |

发布包随附 .NET 与 Windows App SDK 运行组件，无需安装开发工具。预览包未签名，系统可能警告或按策略拦截；已测与未测环境见 [已知问题](docs/known-issues.md)。

## 日常使用

1. 在游戏首页设置全部游戏每日/每周总限额。
2. 按需双击游戏设置单游戏规则，选择仅提醒或超限强退。
3. 在统计页查看每日明细和每周汇总，必要时检查或修正估算。
4. 在设置中调整提醒、关闭窗口行为、认证与备份。

暂停限制仍继续计时。强退可能结束未保存进度的游戏，应主动选择并先体验其行为；应用内认证是自我约束辅助，不构成系统级管控。

## 数据与升级

- 绿色版数据在根启动器旁的 data；搬迁时复制整个解压目录。
- 安装版数据在 `%LOCALAPPDATA%\SteamWatch`，程序在 `%LOCALAPPDATA%\Programs\SteamWatch`。
- 从 1.2.0 升级前导出备份并正常退出旧版；新版账本不能直接交给旧版写入，回退应使用升级前备份。
- 完整备份可能包含认证配置，应自行妥善保管；公开反馈优先使用独立诊断，不上传完整 data。

详细操作见 [中文指南](docs/manual-zh.txt)，升级说明见 [2.0 预览说明](docs/releases/2.0.0-preview.2.md)。

## 开发、反馈与许可

- [开发与构建](docs/development.md)：依赖、模块、单测、打包与可选专项工具。
- [文档导航](docs/README.md)：使用、发布与维护资料。
- [GitHub Issues](https://github.com/lyclycNSP/SteamWatch/issues)：提交版本、环境、复现步骤和相关窗口截图。
- [MIT License](LICENSE)：项目自身源码许可；依赖沿用各自许可，见 [第三方说明](THIRD-PARTY-NOTICES.md)。

SteamWatch 是独立项目，与 Valve / Steam 无隶属或官方支持关系。
