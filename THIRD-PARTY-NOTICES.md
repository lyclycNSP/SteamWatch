# 第三方组件说明

SteamWatch 自身源码按根目录 LICENSE 中的 MIT 许可证提供。第三方组件、运行库及工具沿用各自许可，不因项目采用 MIT 而改用 MIT。

| 组件 | 用途 | 许可来源 |
| --- | --- | --- |
| .NET 运行库及 System.* 包 | 自包含运行环境、Windows 适配 | 实际 NuGet 包内 LICENSE / THIRD-PARTY-NOTICES 及包元数据 |
| Windows App SDK / WinUI 及其运行组件 | 原生界面、通知与 Windows 集成 | 实际 NuGet 包内 license / NOTICE 文件 |
| Windows SDK .NET 投影、WebView2 投影等传递依赖 | Windows API 与 SDK 依赖 | 实际 NuGet 包内许可文件或 nuspec 许可字段 |
| Inno Setup | 生成安装器、卸载器 | [Inno Setup License](https://jrsoftware.org/files/is/license.txt)；构建工具保留原始许可 |

构建脚本按发布的 deps.json 收集依赖，不在本文件手工维护完整版本清单。安装版的 licenses 目录、绿色版的 app/licenses 目录包含：

- SteamWatch-LICENSE.txt：项目 MIT 许可证。
- THIRD-PARTY-NOTICES.md：本说明。
- nuget：从实际还原包原样复制的顶层许可与声明文件。
- license-index.json：实际包 ID、版本、nuspec 许可字段、复制文件名及 SHA256；没有独立许可文件的包如实记录元数据，不伪造完整许可。

游戏名称、图标来自用户本机 Steam 数据，不随发布包预置，相关商标与素材属于各自权利人。SteamWatch 是独立项目，与 Valve / Steam 无隶属或官方支持关系。
